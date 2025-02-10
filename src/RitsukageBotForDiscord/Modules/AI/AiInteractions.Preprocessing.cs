using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.OpenApi;
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

                var jsonData = new JObject
                {
                    ["time"] = Context.Interaction.CreatedAt,
                    ["message"] = message,
                };

                var messageList = new List<ChatMessage>
                {
                    prompt,
                    new(ChatRole.User, jsonData.ToString()),
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
                            case "date_base_info":
                            {
                                var result = await PreprocessDateBaseInfo(data).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(result))
                                    dataList.Add(result);
                                break;
                            }
                            case "range_date_base_info":
                            {
                                var result = await PreprocessRangeDateBaseInfo(data).ConfigureAwait(false);
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

        private static async Task<string> PreprocessDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for date base info action");
            var param = paramToken.ToObject<PreprocessActionParam.DateBaseInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for date base info action");
            var year = param.Date.Year.ToString();
            var month = param.Date.Month.ToString();
            var day = param.Date.Day.ToString();
            var time = new DateTimeOffset(param.Date, TimeSpan.FromHours(8));
            var days = await OpenApi.GetCalendarAsync(time).ConfigureAwait(false);
            var today = days.FirstOrDefault(x => x.Year == year && x.Month == month && x.Day == day);
            var holiday = today?.FestivalInfoList is { Length: > 0 }
                ? string.Join(", ", today.FestivalInfoList.Select(x => x.Name))
                : "今日无节日";
            var workday = today?.Status switch
            {
                BaiduCalendarDayStatus.Holiday => "假期",
                BaiduCalendarDayStatus.Normal when today.CnDay is "六" or "日" => "假期",
                BaiduCalendarDayStatus.Workday => "工作日",
                BaiduCalendarDayStatus.Normal => "工作日",
                _ => "未知",
            };
            var todayString = today is not null
                ? $"""
                   北京时间：{time:yyyy-MM-dd} 星期{today.CnDay}
                   农历：{today.LunarYear}年{today.LMonth}月{today.LDate}日
                   节日：{holiday}
                   今天是{workday}
                   宜：{today.Suit}
                   忌：{today.Avoid}
                   """
                : "未找到相关信息";
            return $"[Date Base Info: {param.Date:yyyy-MM-dd}]\n{todayString}";
        }

        private static async Task<string> PreprocessRangeDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for range date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for range  date base info action");
            var param = paramToken.ToObject<PreprocessActionParam.RangeDateBaseInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for range date base info action");
            var days = new List<BaiduCalendarDay>();
            var from = new DateTimeOffset(param.From, TimeSpan.FromHours(8));
            var end = new DateTimeOffset(param.To, TimeSpan.FromHours(8));
            var query = from;
            while (query <= end)
            {
                var dayInfo = await OpenApi.GetCalendarAsync(query).ConfigureAwait(false);
                days.AddRange(dayInfo);
                query = query.AddMonths(1);
            }

            days = [.. days.Distinct().OrderBy(x => x.ODate).Where(x => x.ODate >= from && x.ODate <= end)];

            var sb = new StringBuilder();
            sb.AppendLine($"[Range Date Base Info: {param.From} - {param.To}]");
            sb.AppendLine("日期 | 星期 | 农历 | 工作日 | 节日");
            sb.AppendLine("--- | --- | --- | --- | ---");
            foreach (var day in days)
            {
                var workday = day.Status switch
                {
                    BaiduCalendarDayStatus.Holiday => "假期",
                    BaiduCalendarDayStatus.Normal when day.CnDay is "六" or "日" => "假期",
                    BaiduCalendarDayStatus.Workday => "工作日",
                    BaiduCalendarDayStatus.Normal => "工作日",
                    _ => "未知",
                };
                if (day.FestivalInfoList is { Length: > 0 })
                    sb.AppendLine(
                        $"{day.ODate:yyyy-MM-dd} | {day.CnDay} | {day.LunarYear}年{day.LMonth}月{day.LDate}日 | {workday} | {string.Join(", ", day.FestivalInfoList.Select(x => x.Name))}");
                else
                    sb.AppendLine(
                        $"{day.ODate:yyyy-MM-dd} | {day.CnDay} | {day.LunarYear}年{day.LMonth}月{day.LDate}日 | {workday} | 无");
            }

            sb.AppendLine("---");
            sb.Append($"共计{days.Count}天");
            return sb.ToString();
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

            internal class DateBaseInfoActionParam
            {
                [JsonProperty("date")] public DateTime Date { get; set; }
            }

            internal class RangeDateBaseInfoActionParam
            {
                [JsonProperty("from")] public DateTime From { get; set; }
                [JsonProperty("to")] public DateTime To { get; set; }
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