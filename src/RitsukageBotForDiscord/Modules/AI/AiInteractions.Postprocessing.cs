using System.Text;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task<EmbedBuilder[]> TryPostprocessMessage(string userMessage, string replyMessage,
            JArray actionData,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ChatClientProvider.GetAssistant("Postprocessing", out var prompt, out var chatClient))
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
                        Temperature = 0.1f,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                var resultMessage = resultCompletion.Message.ToString();
                var (hasJsonHeader, _, jsonHeader, thinkContent) =
                    ChatClientProviderService.FormatResponse(resultMessage);
                if (!string.IsNullOrWhiteSpace(thinkContent))
                    Logger.LogInformation("Think content: {ThinkContent}", thinkContent);
                if (hasJsonHeader) return await ProgressPostprocessActions(jsonHeader!).ConfigureAwait(false);
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

        private async Task<EmbedBuilder[]> ProgressPostprocessActions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            var result = new List<EmbedBuilder>();
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Postprocessing actions: {ActionArrayData}", actionArrayData);
                var showMemoryChange = ChatClientProvider.GetConfig<bool>("ShowMemoryChange");
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
                            case "add_short_memory":
                            {
                                var embed = await PostprocessAddShortMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "add_long_memory":
                            {
                                var embed = await PostprocessAddLongMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                            case "remove_long_memory":
                            case "remove_chat_history":
                            {
                                var embed = await PostprocessRemoveLongMemory(data).ConfigureAwait(false);
                                if (embed is not null && showMemoryChange)
                                    result.Add(embed);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while processing the JSON action: {Json}", data.ToString());
                    }
                }
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

        private async Task<EmbedBuilder?> PostprocessAddShortMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            var param = paramToken.ToObject<PostprocessActionParam.ShortMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");

            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.ShortTerm, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added short-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> PostprocessAddLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            var param = paramToken.ToObject<PostprocessActionParam.LongMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");

            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.LongTerm, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added long-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> PostprocessRemoveLongMemory(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            var param = paramToken.ToObject<PostprocessActionParam.RemoveLongMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (param.Keys.Length == 0)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");

            var sb = new StringBuilder();
            foreach (var key in param.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.LongTerm, key)
                    .ConfigureAwait(false);
                sb.Append($"{key}\n");
            }

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed long-term memory: \n{sb}");
            return embed;
        }

        private static class PostprocessActionParam
        {
            internal class ShortMemoryActionParam
            {
                [JsonProperty("data")] public JObject Data { get; set; } = [];
            }

            internal class LongMemoryActionParam
            {
                [JsonProperty("data")] public JObject Data { get; set; } = [];
            }

            internal class RemoveLongMemoryActionParam
            {
                [JsonProperty("keys")] public string[] Keys { get; set; } = [];
            }
        }
    }
}