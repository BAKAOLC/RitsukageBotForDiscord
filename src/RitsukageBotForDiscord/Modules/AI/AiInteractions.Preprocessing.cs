using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task<string> TryPreprocessMessage(string message, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ChatClientProviderService.GetAssistant("Preprocessing", out var prompt, out var chatClient))
                {
                    Logger.LogWarning("Unable to get the assistant for preprocessing");
                    return string.Empty;
                }

                var messageList = new List<ChatMessage>
                {
                    prompt,
                    new(ChatRole.User, message),
                };
                var resultCompletion = await chatClient.CompleteAsync(messageList, new()
                    {
                        Temperature = 0.1f,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                var resultMessage = resultCompletion.Message.ToString();
                return await ProgressPreprocessActions(resultMessage).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error while preprocessing the message");
            }

            return string.Empty;
        }

        private async Task<string> ProgressPreprocessActions(string json)
        {
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Preprocessing actions: {ActionArrayData}", actionArrayData);
                var dataList = new List<string>();
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
                            case "web_search":
                            {
                                var result = await WebSearch(data).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                            case "bilibili_video_search":
                            case "bilibili_video_info":
                            case "bilibili_user_info":
                            case "bilibili_user_video":
                            case "bilibili_live_info":
                            case "bilibili_dynamic_info":
                            case "bilibili_article_info":
                            {
                                var result = CurrentlyUnsupported(actionType);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while processing the JSON action: {Json}", data.ToString());
                    }
                }

                return string.Join("\n\n", dataList);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error while processing preprocess actions");
            }

            return string.Empty;
        }

        private async Task<string> WebSearch(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for web search action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for web search action");
            var param = paramToken.ToObject<PreprocessActionParam.WebSearchActionParam>();
            if (param is null) throw new InvalidDataException("Invalid JSON data for web search action");
            if (string.IsNullOrWhiteSpace(param.Query))
                throw new InvalidDataException("Invalid query for web search action");
            var result = await GoogleSearchProviderService.WebSearch(param.Query).ConfigureAwait(false);
            var resultStrings = result.Take(5).Select(x => $"# {x.Title}\n{x.Link}\n{x.HtmlSnippet}");
            return $"[Google Search: \"{param.Query}\"]\n{string.Join("\n\n", resultStrings)}";
        }

        private static string CurrentlyUnsupported(string actionType)
        {
            return $"[{actionType}]\n当前暂不支持";
        }

        private static class PreprocessActionParam
        {
            internal class WebSearchActionParam
            {
                [JsonProperty("query")] public string Query { get; set; } = string.Empty;
            }

            internal class BilibiliVideoSearchActionParam
            {
                [JsonProperty("query")] public string Query { get; set; } = string.Empty;
            }

            internal class BilibiliVideoInfoActionParam
            {
                [JsonProperty("id")] public int Id { get; set; }
            }

            internal class BilibiliUserInfoActionParam
            {
                [JsonProperty("id")] public int Id { get; set; }
            }

            internal class BilibiliUserVideoActionParam
            {
                [JsonProperty("id")] public int Id { get; set; }
            }

            internal class BilibiliLiveInfoActionParam
            {
                [JsonProperty("id")] public int Id { get; set; }
            }

            internal class BilibiliDynamicInfoActionParam
            {
                [JsonProperty("id")] public long Id { get; set; }
            }

            internal class BilibiliArticleInfoActionParam
            {
                [JsonProperty("id")] public int Id { get; set; }
            }
        }
    }
}