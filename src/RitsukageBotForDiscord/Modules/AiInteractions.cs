using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
                await ModifyOriginalResponseAsync(x => x.Content = "Please provide a message to chat with the AI")
                    .ConfigureAwait(false);

            var messageList = new List<ChatMessage>
            {
                new(ChatRole.User, message),
            };
            if (GetRoleData() is { } roleData)
                messageList.Insert(0, roleData);
            await BeginChatAsync(messageList).ConfigureAwait(false);
        }

        private ChatMessage? GetRoleData()
        {
            var roleData = Configuration.GetSection("AI:RoleData").Get<string>();
            if (string.IsNullOrWhiteSpace(roleData))
                return null;
            return new(ChatRole.System, roleData);
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
                        content = sb.ToString();
                        isUpdated = false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(content))
                    await ModifyOriginalResponseAsync(x => x.Content = content).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
            }

            if (isUpdated)
            {
                if (isErrored)
                {
                    await DeleteOriginalResponseAsync().ConfigureAwait(false);
                    var embed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = sb.ToString(),
                        Color = Color.Red,
                    };
                    await Context.Channel.SendMessageAsync(embed: embed.Build(), flags: MessageFlags.Ephemeral)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ModifyOriginalResponseAsync(x => x.Content = sb.ToString()).ConfigureAwait(false);
                }
            }

            if (!haveContent)
            {
                await DeleteOriginalResponseAsync().ConfigureAwait(false);
                var embed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "No content was received from the AI",
                    Color = Color.Red,
                };
                await Context.Channel.SendMessageAsync(embed: embed.Build(), flags: MessageFlags.Ephemeral)
                    .ConfigureAwait(false);
            }
        }
    }
}