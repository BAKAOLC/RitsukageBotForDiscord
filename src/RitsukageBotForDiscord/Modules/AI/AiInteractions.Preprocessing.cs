using System.Text;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.OpenApi;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task<string> TryPreprocessingMessage(string message,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ChatClientProvider.GetAssistant("Preprocessing", out var prompt, out var temperature, out var chatClient))
                {
                    Logger.LogWarning("Unable to get the assistant for preprocessing");
                    return string.Empty;
                }

                var jsonData = new JObject
                {
                    ["time"] = Context.Interaction.CreatedAt.ConvertToSettingsOffset().ToDateTimeString(),
                    ["message"] = message,
                };

                var messageList = new List<ChatMessage>
                {
                    prompt,
                    new(ChatRole.User, jsonData.ToString()),
                };
                Logger.LogInformation("Preprocessing message: {Message}", jsonData);
                var resultCompletion = await chatClient.CompleteAsync(messageList, new()
                    {
                        Temperature = temperature,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                var resultMessage = resultCompletion.Message.ToString();
                var (hasJsonHeader, _, jsonHeader, thinkContent) =
                    ChatClientProviderService.FormatResponse(resultMessage);
                if (!string.IsNullOrWhiteSpace(thinkContent))
                    Logger.LogInformation("Think content: {ThinkContent}", thinkContent);
                if (hasJsonHeader) return await ProgressPreprocessingActions(jsonHeader!).ConfigureAwait(false);
                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Preprocessing operation has been canceled");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error while preprocessing the message");
            }

            return string.Empty;
        }

        private async Task<string> ProgressPreprocessingActions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            
            var results = await ProcessUnifiedActions(json, ActionPhase.Preprocessing).ConfigureAwait(false);
            var stringResults = results.OfType<string>().Where(s => !string.IsNullOrEmpty(s));
            
            return string.Join("\n\n", stringResults);
        }

        // Note: All preprocessing action methods have been moved to AiInteractions.UnifiedActions.cs
        // and are now accessible through the ProcessUnifiedActions method
    }
}