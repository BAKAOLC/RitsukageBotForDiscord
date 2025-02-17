using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task<EmbedBuilder[]> TryPostprocessingMessage(string userMessage, string replyMessage,
            JArray actionData,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ChatClientProvider.GetAssistant("Postprocessing", out var prompt, out var temperature, out var chatClient))
                {
                    Logger.LogWarning("Unable to get the assistant for postprocessing");
                    return [];
                }

                var message = await ChatClientProvider.BuildUserPostprocessingMessage(Context.User.Id,
                    Context.Interaction.CreatedAt, userMessage, replyMessage, actionData).ConfigureAwait(false);

                if (message == null)
                {
                    Logger.LogWarning("Unable to build the postprocessing message");
                    return [];
                }

                var messageList = new List<ChatMessage>
                {
                    prompt,
                    message,
                };
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
                if (hasJsonHeader) return await ProgressPostprocessingActions(jsonHeader!).ConfigureAwait(false);
                return [];
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Postprocessing operation has been canceled");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error while postprocessing the message");
            }

            return [];
        }

        private async Task<EmbedBuilder[]> ProgressPostprocessingActions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            var result = new List<EmbedBuilder>();
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Postprocessing actions: {ActionArrayData}", actionArrayData);
                /*
                foreach (var actionData in actionArrayData)
                {
                    if (actionData is not JObject data) continue;
                    try
                    {
                        var actionType = data.Value<string>("action");
                        if (string.IsNullOrWhiteSpace(actionType))
                        {
                            Logger.LogWarning("Unable to parse the JSON: {Json}", data.ToString());
                            continue;
                        }

                        switch (actionType)
                        {

                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while processing the JSON action: {Json}", data.ToString());
                    }
                }
                */
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while parsing the JSON header: {JsonHeader}", json);
                var errorEmbed = new EmbedBuilder();
                errorEmbed.WithColor(Color.Red);
                errorEmbed.WithDescription("An error occurred while processing the response");
                return [errorEmbed];
            }

            await ChatClientProvider.RefreshShortMemory(Context.User.Id).ConfigureAwait(false);
            return [.. result];
        }

        private static class PostprocessingActionParam
        {
        }
    }
}