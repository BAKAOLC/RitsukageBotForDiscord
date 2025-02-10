using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Services.Providers;
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
                if (!ChatClientProvider.GetAssistant("Preprocessing", out var prompt, out var chatClient))
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
                var (hasJsonHeader, _, jsonHeader, thinkContent) =
                    ChatClientProviderService.FormatResponse(resultMessage);
                if (!string.IsNullOrWhiteSpace(thinkContent))
                    Logger.LogInformation("Think content: {ThinkContent}", thinkContent);
                if (hasJsonHeader) return await ProgressPreprocessActions(jsonHeader!).ConfigureAwait(false);
                return string.Empty;
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
                                var result = await PreprocessWebSearch(data).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                            case "bilibili_video_info":
                            {
                                var result = await PreprocessBilibiliVideoInfo(data).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                            case "bilibili_user_info":
                            {
                                var result = await PreprocessBilibiliUserInfo(data).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                            case "bilibili_live_info":
                            {
                                var result = await PreprocessBilibiliLiveInfo(data).ConfigureAwait(false);
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

        private async Task<string> PreprocessWebSearch(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for web search action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for web search action");
            var param = paramToken.ToObject<PreprocessActionParam.WebSearchActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for web search action");
            if (string.IsNullOrWhiteSpace(param.Query))
                throw new InvalidDataException("Invalid query for web search action");
            var result = await GoogleSearchProviderService.WebSearch(param.Query).ConfigureAwait(false);
            var resultStrings = result.Take(5).Select(x => $"# {x.Title}\n{x.Snippet}\n\n{x.Link}");
            return $"[Google Search: \"{param.Query}\"]\n{string.Join("\n\n", resultStrings)}";
        }

        private async Task<string> PreprocessBilibiliVideoInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var param = paramToken.ToObject<PreprocessActionParam.BilibiliVideoInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var playerService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await playerService.GetVideoPageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Video Info: {param.Id}]\n{InformationStringBuilder.BuildVideoInfo(info)}";
        }

        private async Task<string> PreprocessBilibiliUserInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var param = paramToken.ToObject<PreprocessActionParam.BilibiliUserInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var userService = BiliKernelProviderService.GetRequiredService<IUserService>();
            var info = await userService.GetUserInformationAsync(param.Id.ToString())
                .ConfigureAwait(false);
            return $"[Bilibili User Info: {param.Id}]\n{InformationStringBuilder.BuildUserInfo(info)}";
        }

        private async Task<string> PreprocessBilibiliLiveInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var param = paramToken.ToObject<PreprocessActionParam.BilibiliLiveInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var liveService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await liveService.GetLivePageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Live Info: {param.Id}]\n{InformationStringBuilder.BuildLiveInfo(info)}";
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