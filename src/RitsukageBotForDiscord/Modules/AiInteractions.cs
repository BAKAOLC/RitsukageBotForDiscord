using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Ai interactions
    /// </summary>
    [Group("ai", "Ai interactions")]
    public class AiInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Custom ID
        /// </summary>
        public const string CustomId = "ai_interaction";

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<AiInteractions> Logger { get; set; }

        /// <summary>
        ///     Chat client provider service
        /// </summary>
        public required ChatClientProviderService ChatClientProviderService { get; set; }

        /// <summary>
        ///     Database provider service
        /// </summary>
        public required DatabaseProviderService DatabaseProviderService { get; set; }

        /// <summary>
        ///     Configuration
        /// </summary>
        public required IConfiguration Configuration { get; set; }

        /// <summary>
        ///     Chat with the AI
        /// </summary>
        [SlashCommand("chat", "Chat with the AI")]
        public async Task ChatAsync(string message)
        {
            await DeferAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x => x.Embeds = new[] { embed.Build() }).ConfigureAwait(false);
            }

            var messageList = new List<ChatMessage>();
            if (GetRoleData() is { } roleData)
                messageList.Add(roleData);
            if (await BuildUserChatMessage(Context.User.Id, Context.User.Username, message).ConfigureAwait(false)
                is not { } userMessage)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "An error occurred while building the user chat message",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x => x.Embeds = new[] { embed.Build() }).ConfigureAwait(false);
                return;
            }

            messageList.Add(userMessage);
            await BeginChatAsync(messageList).ConfigureAwait(false);
        }

        private ChatMessage? GetRoleData()
        {
            var roleData = Configuration.GetSection("AI:RoleData").Get<string>();
            if (string.IsNullOrWhiteSpace(roleData))
                return null;
            if (File.Exists(roleData))
                roleData = File.ReadAllText(roleData);
            return new(ChatRole.System, roleData);
        }

        private async Task<ChatMessage?> BuildUserChatMessage(ulong id, string name, string message)
        {
            var (_, userInfo) = await DatabaseProviderService.GetOrCreateAsync<ChatUserInformation>(id)
                .ConfigureAwait(false);
            var jObject = new JObject
            {
                ["name"] = name,
                ["message"] = message,
                ["good"] = userInfo.Good,
            };
            return new(ChatRole.User, jObject.ToString());
        }

        private async Task BeginChatAsync(IList<ChatMessage> messageList, bool useTools = false)
        {
            string? userInputMessage = null;
            var lastUserMessage = messageList.LastOrDefault(x => x.Role == ChatRole.User);
            if (lastUserMessage is not null)
                userInputMessage = lastUserMessage.ToString();
            if (string.IsNullOrWhiteSpace(userInputMessage))
            {
                await ModifyOriginalResponseAsync(x => x.Content = "Please provide a message to chat with the AI")
                    .ConfigureAwait(false);
                return;
            }

            var sb = new StringBuilder();
            var isCompleted = false;
            var isUpdated = false;
            var isErrored = false;
            var haveContent = false;
            var lockObject = new Lock();
            _ = Task.Run(async () =>
            {
                await foreach (var response in ChatClientProviderService.CompleteStreamingAsync(messageList, useTools))
                    lock (lockObject)
                    {
                        if (string.IsNullOrWhiteSpace(response.ToString()))
                            continue;
                        sb.Append(response);
                        isUpdated = true;
                        haveContent = true;
                    }

                isCompleted = true;
            }).ContinueWith(x =>
            {
                if (!x.IsFaulted) return;
                isCompleted = true;
                isUpdated = true;
                isErrored = true;
                sb = new();
                sb.Append("An error occurred while processing the chat with AI tools");
                Logger.LogError(x.Exception, "Error while processing the chat with AI tools");
            });
            while (!isCompleted)
            {
                string? content = null;
                lock (lockObject)
                {
                    if (isUpdated)
                    {
                        (_, content, _) = FormatResponse(sb.ToString());
                        isUpdated = false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(content))
                    await ModifyOriginalResponseAsync(x => x.Content = content).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
            }

            if (isErrored)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = sb.ToString(),
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embeds = new[] { embed.Build() };
                }).ConfigureAwait(false);
            }
            else if (!haveContent)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "No content was received from the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = null;
                    x.Embeds = new[] { embed.Build() };
                }).ConfigureAwait(false);
            }
            else
            {
                var (hasJsonHeader, content, jsonHeader) = FormatResponse(sb.ToString());
                if (hasJsonHeader)
                {
                    var jObject = JObject.Parse(jsonHeader!);
                    var current = jObject.TryGetValue("good", out var good) ? good.Value<int>() : 0;
                    var change = jObject.TryGetValue("change", out var changeValue) ? changeValue.Value<int>() : 0;
                    var (_, userInfo) = await DatabaseProviderService
                        .GetOrCreateAsync<ChatUserInformation>(Context.User.Id)
                        .ConfigureAwait(false);
                    Logger.LogInformation(
                        "User {UserId} has changed their good value from {OldGood} to {NewGood}, change: {Change}",
                        Context.User.Id, userInfo.Good, current, change);
                    userInfo.Good = current;
                    await DatabaseProviderService.UpdateAsync(userInfo).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(content))
                    await ModifyOriginalResponseAsync(x => x.Content = content)
                        .ConfigureAwait(false);
            }
        }

        private static (bool, string, string?) CheckJsonHeader(string response)
        {
            if (!response.StartsWith('{')) return (false, response, null);
            var firstLineEndIndex = response.IndexOf('\n');
            if (firstLineEndIndex == -1)
                firstLineEndIndex = response.IndexOf('\r');
            if (firstLineEndIndex == -1)
                return (false, string.Empty, response);
            var firstLine = response[..firstLineEndIndex];
            response = response[(firstLineEndIndex + 1)..];
            return (true, response, firstLine);
        }

        private static (bool, string, string?) FormatResponse(string response)
        {
            response = response.Trim();

            if (!response.StartsWith("<think>"))
                return CheckJsonHeader(response);

            string thinkContent;
            var hasJsonHeader = false;
            string? jsonHeader = null;
            var content = string.Empty;

            var thinkEndIndex = response.IndexOf("</think>", StringComparison.Ordinal);
            if (thinkEndIndex != -1)
            {
                thinkContent = response[7..thinkEndIndex];
                content = response[(thinkEndIndex + 8)..];
            }
            else
            {
                thinkContent = response[7..];
            }

            var lines = thinkContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.Append("> ");
                sb.AppendLine(line);
            }

            if (string.IsNullOrWhiteSpace(content)) return (hasJsonHeader, sb.ToString(), jsonHeader);
            (hasJsonHeader, content, jsonHeader) = CheckJsonHeader(content);
            sb.Append(content);
            return (hasJsonHeader, sb.ToString(), jsonHeader);
        }
    }
}