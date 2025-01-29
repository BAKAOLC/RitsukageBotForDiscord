using System.Text;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
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
        ///     Chat with the AI
        /// </summary>
        [RequireOwner]
        [SlashCommand("chat", "Chat with the AI")]
        public async Task ChatAsync(string message)
        {
            await DeferAsync(true).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message))
                await ModifyOriginalResponseAsync(x => x.Content = "Please provide a message to chat with the AI")
                    .ConfigureAwait(false);

            var messageList = new List<ChatMessage>
            {
                new(ChatRole.User, message),
            };
            await BeginChatAsync(messageList).ConfigureAwait(false);
        }

        /*
        /// <summary>
        ///     Chat with the AI using AI tools
        /// </summary>
        [RequireOwner]
        [SlashCommand("chat-tools", "Chat with the AI using AI tools")]
        public async Task ChatWithAiToolsAsync(string message)
        {
            await DeferAsync(true).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message))
                await ModifyOriginalResponseAsync(x => x.Content = "Please provide a message to chat with the AI")
                    .ConfigureAwait(false);

            var messageList = new List<ChatMessage>
            {
                new(ChatRole.User, message),
            };
            await BeginChatAsync(messageList, true).ConfigureAwait(false);
        }
        */

        private async Task BeginChatAsync(IList<ChatMessage> messageList, bool useTools = false)
        {
            var sb = new StringBuilder();
            var isCompleted = false;
            var isUpdated = false;
            var lockObject = new Lock();
            _ = Task.Run(async () =>
            {
                await foreach (var response in ChatClientProviderService.CompleteStreamingAsync(messageList, useTools))
                    lock (lockObject)
                    {
                        sb.Append(response);
                        isUpdated = true;
                    }

                isCompleted = true;
            }).ContinueWith(x =>
            {
                if (!x.IsFaulted) return;
                isCompleted = true;
                isUpdated = true;
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
                await ModifyOriginalResponseAsync(x => x.Content = sb.ToString()).ConfigureAwait(false);
        }
    }
}