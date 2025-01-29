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
            if (GetRoleData() is { } roleData)
                messageList.Insert(0, roleData);
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
           if (GetRoleData() is { } roleData)
               messageList.Insert(0, roleData);
            await BeginChatAsync(messageList, true).ConfigureAwait(false);
        }
        */

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
            AppendUserMessage(sb, userInputMessage);
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
                AppendUserMessage(sb, userInputMessage);
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

            if (!haveContent)
                await ModifyOriginalResponseAsync(x => x.Content = "No response from the AI").ConfigureAwait(false);

            if (!isErrored && haveContent)
            {
                var components = new ComponentBuilder();
                components.WithButton("Publish", $"{CustomId}:publish");
                await ModifyOriginalResponseAsync(x => x.Components = components.Build()).ConfigureAwait(false);
            }
        }

        private static StringBuilder AppendUserMessage(StringBuilder sb, string message)
        {
            var lines = message.Split('\n');
            foreach (var line in lines)
            {
                sb.Append("> ");
                sb.AppendLine(line);
            }

            sb.AppendLine();
            return sb;
        }
    }


    /// <summary>
    ///     Interaction button for AI
    /// </summary>
    public class AiInteractionButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<AiInteractionButton> Logger { get; set; }

        /// <summary>
        ///     Publish the chat with AI
        /// </summary>
        [ComponentInteraction($"{AiInteractions.CustomId}:publish")]
        public async Task PublishAsync()
        {
            await Context.Interaction.UpdateAsync(x => x.Components = null).ConfigureAwait(false);
            var embed = new EmbedBuilder();
            var (userMessage, aiMessage) = SplitContent(Context.Interaction.Message.Content);
            embed.AddField("User Message", userMessage);
            embed.AddField("AI Message", aiMessage);
            embed.WithTimestamp(Context.Interaction.Message.Timestamp);
            embed.WithAuthor(Context.User);
            embed.WithFooter("Ritsukage Bot", Context.Client.CurrentUser.GetAvatarUrl());
            await Context.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        private (string, string) SplitContent(string content)
        {
            var lines = content.Split('\n');
            var userMessage = new StringBuilder();
            var aiMessage = new StringBuilder();
            var isUserMessage = true;
            foreach (var line in lines)
                if (line.StartsWith("> "))
                {
                    if (isUserMessage)
                        userMessage.AppendLine(line[2..]);
                    else
                        aiMessage.AppendLine(line[2..]);
                }
                else
                {
                    isUserMessage = false;
                    aiMessage.AppendLine(line);
                }

            return (userMessage.ToString(), aiMessage.ToString());
        }
    }
}