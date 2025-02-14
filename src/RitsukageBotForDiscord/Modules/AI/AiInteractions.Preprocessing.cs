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
                Logger.LogInformation("Preprocessing message: {Message}", jsonData);
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
            var result = new List<string>();
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Preprocessing actions: {ActionArrayData}", actionArrayData);
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

                        var resultMessage = actionType switch
                        {
                            "web_search" => await PreprocessingWebSearch(data).ConfigureAwait(false),
                            "date_base_info" => await PreprocessingDateBaseInfo(data).ConfigureAwait(false),
                            "range_date_base_info" => await PreprocessingRangeDateBaseInfo(data).ConfigureAwait(false),
                            "bilibili_video_info" => await PreprocessingBilibiliVideoInfo(data).ConfigureAwait(false),
                            "bilibili_user_info" => await PreprocessingBilibiliUserInfo(data).ConfigureAwait(false),
                            "bilibili_live_info" => await PreprocessingBilibiliLiveInfo(data).ConfigureAwait(false),
                            _ => string.Empty,
                        };

                        if (!string.IsNullOrEmpty(resultMessage))
                            result.Add(resultMessage);
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
            }

            return string.Join("\n\n", result);
        }

        private async Task<string> PreprocessingWebSearch(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for web search action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for web search action");
            var param = paramToken.ToObject<PreprocessingActionParam.WebSearchActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for web search action");
            if (string.IsNullOrWhiteSpace(param.Query))
                throw new InvalidDataException("Invalid query for web search action");
            var result = await GoogleSearchProviderService.WebSearch(param.Query).ConfigureAwait(false);
            var resultStrings = result.Take(5).Select(x => $"# {x.Title}\n{x.Snippet}\n\n{x.Link}");
            return $"[Google Search: \"{param.Query}\"]\n{string.Join("\n\n", resultStrings)}";
        }

        private static async Task<string> PreprocessingDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for date base info action");
            var param = paramToken.ToObject<PreprocessingActionParam.DateBaseInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for date base info action");
            var time = param.Date.AsSettingsOffset();
            var days = await OpenApi.GetCalendarAsync(time).ConfigureAwait(false);
            var today = days.FirstOrDefault(x => x.ODate.ConvertToSettingsOffset() == time);
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

        private static async Task<string> PreprocessingRangeDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for range date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for range  date base info action");
            var param = paramToken.ToObject<PreprocessingActionParam.RangeDateBaseInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for range date base info action");
            var days = new List<BaiduCalendarDay>();
            var from = param.From.AsSettingsOffset();
            var end = param.To.AsSettingsOffset();
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

        private async Task<string> PreprocessingBilibiliVideoInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var param = paramToken.ToObject<PreprocessingActionParam.BilibiliVideoInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var playerService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await playerService.GetVideoPageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Video Info: {param.Id}]\n{InformationStringBuilder.BuildVideoInfo(info)}";
        }

        private async Task<string> PreprocessingBilibiliUserInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var param = paramToken.ToObject<PreprocessingActionParam.BilibiliUserInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var userService = BiliKernelProviderService.GetRequiredService<IUserService>();
            var info = await userService.GetUserInformationAsync(param.Id.ToString())
                .ConfigureAwait(false);
            return $"[Bilibili User Info: {param.Id}]\n{InformationStringBuilder.BuildUserInfo(info)}";
        }

        private async Task<string> PreprocessingBilibiliLiveInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var param = paramToken.ToObject<PreprocessingActionParam.BilibiliLiveInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var liveService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await liveService.GetLivePageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Live Info: {param.Id}]\n{InformationStringBuilder.BuildLiveInfo(info)}";
        }

        private static class PreprocessingActionParam
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