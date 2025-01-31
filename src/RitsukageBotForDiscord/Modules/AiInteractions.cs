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

        private static readonly HashSet<ulong> IsProcessing = [];

        private static readonly Lock LockObject = new();

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

            bool isChatting;
            lock (LockObject)
            {
                isChatting = IsProcessing.Contains(Context.User.Id);
            }

            if (isChatting)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "You are already chatting with the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            }

            lock (LockObject)
            {
                IsProcessing.Add(Context.User.Id);
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
                await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }

            messageList.Add(userMessage);
            await BeginChatAsync(messageList, false, 3).ConfigureAwait(false);
            lock (LockObject)
            {
                IsProcessing.Remove(Context.User.Id);
            }
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

        private async Task BeginChatAsync(IList<ChatMessage> messageList, bool useTools = false, int retry = 0)
        {
            if (!CheckUserInputMessage(messageList))
            {
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Please provide a message to chat with the AI",
                    Color = Color.Red,
                };
                await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }

            var (isSuccess, errorMessage) = await TryGettingResponse(messageList, useTools).ConfigureAwait(false);
            if (isSuccess) return;

            if (retry > 0)
                for (var i = 0; i < retry; i++)
                {
                    var retryMessage = $"{errorMessage}\nRetrying... ({i + 1}/{retry})";
                    await ModifyOriginalResponseAsync(x => x.Content = retryMessage).ConfigureAwait(false);
                    (isSuccess, errorMessage) = await TryGettingResponse(messageList, useTools).ConfigureAwait(false);
                    if (isSuccess) return;
                }

            var errorEmbed = new EmbedBuilder
            {
                Title = "Error",
                Description = errorMessage,
                Color = Color.Red,
            };
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Embed = errorEmbed.Build();
            }).ConfigureAwait(false);
        }

        private async Task<(bool, string?)> TryGettingResponse(IList<ChatMessage> messageList, bool useTools = false,
            long timeout = 60000)
        {
            var sb = new StringBuilder();
            var haveContent = false;
            var isCompleted = false;
            var isUpdated = false;
            var isErrored = false;
            var isTimeout = false;
            var lockObject = new Lock();
            {
                var cancellationTokenSource = new CancellationTokenSource();
                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(timeout), cancellationTokenSource.Token)
                    .ContinueWith(x =>
                    {
                        if (x.IsFaulted) return;
                        lock (lockObject)
                        {
                            // ReSharper disable once AccessToModifiedClosure
                            if (haveContent) return;
                            cancellationTokenSource.Cancel();
                            isTimeout = true;
                            Logger.LogWarning("The chat with AI tools took too long to respond");
                        }
                    }, cancellationTokenSource.Token);
                _ = Task.Run(async () =>
                    {
                        await foreach (var response in ChatClientProviderService.CompleteStreamingAsync(messageList,
                                           useTools,
                                           cancellationTokenSource.Token))
                            lock (lockObject)
                            {
                                if (string.IsNullOrWhiteSpace(response.ToString()))
                                    continue;
                                sb.Append(response);
                                isUpdated = true;
                                haveContent = true;
                            }

                        isCompleted = true;
                        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    }, cancellationTokenSource.Token)
                    .ContinueWith(x =>
                    {
                        if (!x.IsFaulted) return;
                        if (isTimeout) return;
                        isErrored = true;
                        Logger.LogError(x.Exception, "Error while processing the chat with AI tools");
                    }, cancellationTokenSource.Token);
            }

            while (!isCompleted)
            {
                string? updatingContent = null;
                lock (lockObject)
                {
                    if (isUpdated)
                    {
                        (_, updatingContent, _) = FormatResponse(sb.ToString());
                        isUpdated = false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(updatingContent))
                    await ModifyOriginalResponseAsync(x => x.Content = updatingContent).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
            }

            if (isErrored) return (false, "An error occurred while processing the chat with AI tools");
            if (isTimeout) return (false, "The chat with AI tools took too long to respond");

            var (hasJsonHeader, content, jsonHeader) = FormatResponse(sb.ToString());
            if (hasJsonHeader)
                try
                {
                    var jObject = JObject.Parse(jsonHeader!);
                    var current = jObject.TryGetValue("good", out var good) ? good.Value<int>() : 0;
                    var before = jObject.TryGetValue("before", out var beforeValue) ? beforeValue.Value<int>() : 0;
                    var change = jObject.TryGetValue("change", out var changeValue) ? changeValue.Value<int>() : 0;
                    var reason = jObject.TryGetValue("reason", out var reasonValue)
                        ? reasonValue.Value<string>()
                        : null;
                    var (_, userInfo) = await DatabaseProviderService
                        .GetOrCreateAsync<ChatUserInformation>(Context.User.Id)
                        .ConfigureAwait(false);
                    if (string.IsNullOrEmpty(reason))
                        Logger.LogInformation("User {UserId} got {Change} points, {Before} -> {Current}",
                            Context.User.Id, change, before, current);
                    else
                        Logger.LogInformation("User {UserId} got {Change} points, {Before} -> {Current} ({Reason})",
                            Context.User.Id, change, before, current, reason);
                    userInfo.Good = current;
                    await DatabaseProviderService.InsertOrUpdateAsync(userInfo).ConfigureAwait(false);
                    if (change != 0)
                    {
                        var embed = new EmbedBuilder();
                        embed.WithColor(change > 0 ? Color.Green : Color.Red);
                        var description = change > 0
                            ? $"Increased by {change} points, current points: {current}"
                            : $"Decreased by {Math.Abs(change)} points, current points: {current}";
                        embed.WithDescription(!string.IsNullOrWhiteSpace(reason)
                            ? $"{description} ({reason})"
                            : description);
                        await ModifyOriginalResponseAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error while parsing the JSON header");
                }

            if (!string.IsNullOrWhiteSpace(content))
                await ModifyOriginalResponseAsync(x => x.Content = content).ConfigureAwait(false);

            return (true, null);
        }

        private static bool CheckUserInputMessage(IList<ChatMessage> messageList)
        {
            string? userInputMessage = null;
            var lastUserMessage = messageList.LastOrDefault(x => x.Role == ChatRole.User);
            if (lastUserMessage is not null)
                userInputMessage = lastUserMessage.ToString();
            return !string.IsNullOrWhiteSpace(userInputMessage);
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