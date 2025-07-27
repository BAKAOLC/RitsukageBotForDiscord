using System.Text;
using Discord;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.OpenApi;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Services.AI
{
    /// <summary>
    /// AI Function Calling Service for proper Function Calling implementation
    /// </summary>
    public class AiFunctionCallingService
    {
        private readonly ILogger<AiFunctionCallingService> _logger;
        private readonly DatabaseProviderService _databaseProviderService;
        private readonly ChatClientProviderService _chatClientProviderService;
        private readonly GoogleSearchProviderService _googleSearchProviderService;
        private readonly BiliKernelProviderService _biliKernelProviderService;

        private ulong _currentUserId;
        private DateTimeOffset _currentTimestamp;

        public AiFunctionCallingService(
            ILogger<AiFunctionCallingService> logger,
            DatabaseProviderService databaseProviderService,
            ChatClientProviderService chatClientProviderService,
            GoogleSearchProviderService googleSearchProviderService,
            BiliKernelProviderService biliKernelProviderService)
        {
            _logger = logger;
            _databaseProviderService = databaseProviderService;
            _chatClientProviderService = chatClientProviderService;
            _googleSearchProviderService = googleSearchProviderService;
            _biliKernelProviderService = biliKernelProviderService;
        }

        /// <summary>
        /// Set the current context for function calls
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="timestamp">Interaction timestamp</param>
        public void SetContext(ulong userId, DateTimeOffset timestamp)
        {
            _currentUserId = userId;
            _currentTimestamp = timestamp;
        }

        /// <summary>
        /// Get function invocation handlers for AI function calling
        /// </summary>
        /// <returns>Dictionary of function name to handler</returns>
        public Dictionary<string, Func<FunctionCallContent, CancellationToken, Task<FunctionResultContent>>> GetFunctionHandlers()
        {
            return new Dictionary<string, Func<FunctionCallContent, CancellationToken, Task<FunctionResultContent>>>
            {
                { nameof(AiFunctionTools.WebSearchAsync), HandleWebSearch },
                { nameof(AiFunctionTools.GetDateInfoAsync), HandleGetDateInfo },
                { nameof(AiFunctionTools.GetRangeDateInfoAsync), HandleGetRangeDateInfo },
                { nameof(AiFunctionTools.GetBilibiliVideoInfoAsync), HandleGetBilibiliVideoInfo },
                { nameof(AiFunctionTools.GetBilibiliUserInfoAsync), HandleGetBilibiliUserInfo },
                { nameof(AiFunctionTools.GetBilibiliLiveInfoAsync), HandleGetBilibiliLiveInfo },
                { nameof(AiFunctionTools.AddShortMemoryAsync), HandleAddShortMemory },
                { nameof(AiFunctionTools.AddLongMemoryAsync), HandleAddLongMemory },
                { nameof(AiFunctionTools.UpdateSelfStateAsync), HandleUpdateSelfState },
                { nameof(AiFunctionTools.RemoveLongMemoryAsync), HandleRemoveLongMemory },
                { nameof(AiFunctionTools.RemoveSelfStateAsync), HandleRemoveSelfState },
                { nameof(AiFunctionTools.ModifyUserGoodAsync), HandleModifyUserGood }
            };
        }

        /// <summary>
        /// Extract function call result displays from executed functions
        /// </summary>
        /// <param name="functionCalls">List of function call results</param>
        /// <returns>Embed builders for display</returns>
        public List<EmbedBuilder> ExtractFunctionDisplays(IEnumerable<FunctionResultContent> functionCalls)
        {
            var embeds = new List<EmbedBuilder>();
            var showGoodChange = _chatClientProviderService.GetConfig<bool>("ShowGoodChange");
            var showMemoryChange = _chatClientProviderService.GetConfig<bool>("ShowMemoryChange");

            foreach (var result in functionCalls)
            {
                if (result.Result is JObject resultObj && resultObj.TryGetValue("embed", out var embedToken) && embedToken is JObject embedObj)
                {
                    var functionName = result.CallId ?? "Unknown";
                    
                    // Filter based on function type and settings
                    var shouldShow = functionName switch
                    {
                        nameof(AiFunctionTools.ModifyUserGoodAsync) => showGoodChange,
                        nameof(AiFunctionTools.AddShortMemoryAsync) or 
                        nameof(AiFunctionTools.AddLongMemoryAsync) or 
                        nameof(AiFunctionTools.UpdateSelfStateAsync) or 
                        nameof(AiFunctionTools.RemoveLongMemoryAsync) or 
                        nameof(AiFunctionTools.RemoveSelfStateAsync) => showMemoryChange,
                        _ => false
                    };

                    if (shouldShow && TryCreateEmbedFromJson(embedObj, out var embed))
                    {
                        embeds.Add(embed);
                    }
                }
            }

            return embeds;
        }

        private bool TryCreateEmbedFromJson(JObject embedObj, out EmbedBuilder embed)
        {
            embed = new EmbedBuilder();
            
            if (embedObj.TryGetValue("color", out var colorToken) && colorToken.Type == JTokenType.String)
            {
                var colorStr = colorToken.Value<string>();
                if (Enum.TryParse<Color>(colorStr, out var color))
                {
                    embed.WithColor(color);
                }
            }

            if (embedObj.TryGetValue("description", out var descToken) && descToken.Type == JTokenType.String)
            {
                embed.WithDescription(descToken.Value<string>());
                return true;
            }

            return false;
        }

        // Function handlers

        private async Task<FunctionResultContent> HandleWebSearch(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("query", out var queryObj) || queryObj is not string query)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid query parameter");
                }

                var result = await _googleSearchProviderService.WebSearch(query);
                var resultStrings = result.Take(5).Select(x => $"# {x.Title}\n{x.Snippet}\n\n{x.Link}");
                var searchResult = $"[Google Search: \"{query}\"]\n{string.Join("\n\n", resultStrings)}";
                
                return new FunctionResultContent(call.CallId, call.Name, searchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in web search function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error performing web search: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleGetDateInfo(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("date", out var dateObj) || dateObj is not string dateStr)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid date parameter");
                }

                if (!DateTime.TryParse(dateStr, out var date))
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid date format");
                }

                var time = date.AsSettingsOffset();
                var days = await OpenApi.GetCalendarAsync(time);
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
                
                var result = $"[Date Base Info: {date:yyyy-MM-dd}]\n{todayString}";
                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get date info function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error getting date info: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleGetRangeDateInfo(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("fromDate", out var fromDateObj) || fromDateObj is not string fromDateStr ||
                    !args.TryGetValue("toDate", out var toDateObj) || toDateObj is not string toDateStr)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid date parameters");
                }

                if (!DateTime.TryParse(fromDateStr, out var fromDate) || !DateTime.TryParse(toDateStr, out var toDate))
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid date format");
                }

                var days = new List<BaiduCalendarDay>();
                var from = fromDate.AsSettingsOffset();
                var end = toDate.AsSettingsOffset();
                var query = from;
                
                while (query <= end)
                {
                    var dayInfo = await OpenApi.GetCalendarAsync(query);
                    days.AddRange(dayInfo);
                    query = query.AddMonths(1);
                }

                days = [.. days.Distinct().OrderBy(x => x.ODate).Where(x => x.ODate >= from && x.ODate <= end)];

                var sb = new StringBuilder();
                sb.AppendLine($"[Range Date Base Info: {fromDate:yyyy-MM-dd} - {toDate:yyyy-MM-dd}]");
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
                        sb.AppendLine($"{day.ODate:yyyy-MM-dd} | {day.CnDay} | {day.LunarYear}年{day.LMonth}月{day.LDate}日 | {workday} | {string.Join(", ", day.FestivalInfoList.Select(x => x.Name))}");
                    else
                        sb.AppendLine($"{day.ODate:yyyy-MM-dd} | {day.CnDay} | {day.LunarYear}年{day.LMonth}月{day.LDate}日 | {workday} | 无");
                }

                sb.AppendLine("---");
                sb.Append($"共计{days.Count}天");
                
                return new FunctionResultContent(call.CallId, call.Name, sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get range date info function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error getting range date info: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleGetBilibiliVideoInfo(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("videoId", out var videoIdObj) || videoIdObj is not string videoId)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid videoId parameter");
                }

                var playerService = _biliKernelProviderService.GetRequiredService<IPlayerService>();
                var info = await playerService.GetVideoPageDetailAsync(new(videoId, null, null));
                var result = $"[Bilibili Video Info: {videoId}]\n{InformationStringBuilder.BuildVideoInfo(info)}";
                
                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get Bilibili video info function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error getting Bilibili video info: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleGetBilibiliUserInfo(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("userId", out var userIdObj) || userIdObj is not string userId)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid userId parameter");
                }

                var userService = _biliKernelProviderService.GetRequiredService<IUserService>();
                var info = await userService.GetUserInformationAsync(userId);
                var result = $"[Bilibili User Info: {userId}]\n{InformationStringBuilder.BuildUserInfo(info)}";
                
                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get Bilibili user info function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error getting Bilibili user info: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleGetBilibiliLiveInfo(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("liveId", out var liveIdObj) || liveIdObj is not string liveId)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid liveId parameter");
                }

                var liveService = _biliKernelProviderService.GetRequiredService<IPlayerService>();
                var info = await liveService.GetLivePageDetailAsync(new(liveId, null, null));
                var result = $"[Bilibili Live Info: {liveId}]\n{InformationStringBuilder.BuildLiveInfo(info)}";
                
                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get Bilibili live info function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error getting Bilibili live info: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleAddShortMemory(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("key", out var keyObj) || keyObj is not string key ||
                    !args.TryGetValue("value", out var valueObj) || valueObj is not string value)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid parameters");
                }

                await _chatClientProviderService.InsertMemory(_currentUserId, ChatMemoryType.ShortTerm, key, value);
                
                var embed = new JObject
                {
                    ["color"] = "DarkGreen",
                    ["description"] = $"Added short-term memory: \n{key} = {value}"
                };
                
                var result = new JObject
                {
                    ["message"] = $"Added short-term memory: {key} = {value}",
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in add short memory function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error adding short memory: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleAddLongMemory(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("key", out var keyObj) || keyObj is not string key ||
                    !args.TryGetValue("value", out var valueObj) || valueObj is not string value)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid parameters");
                }

                await _chatClientProviderService.InsertMemory(_currentUserId, ChatMemoryType.LongTerm, key, value);
                
                var embed = new JObject
                {
                    ["color"] = "DarkGreen",
                    ["description"] = $"Added long-term memory: \n{key} = {value}"
                };
                
                var result = new JObject
                {
                    ["message"] = $"Added long-term memory: {key} = {value}",
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in add long memory function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error adding long memory: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleUpdateSelfState(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("key", out var keyObj) || keyObj is not string key ||
                    !args.TryGetValue("value", out var valueObj) || valueObj is not string value)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid parameters");
                }

                await _chatClientProviderService.InsertMemory(_currentUserId, ChatMemoryType.SelfState, key, value);
                
                var embed = new JObject
                {
                    ["color"] = "DarkGreen",
                    ["description"] = $"Updated self state: \n{key} = {value}"
                };
                
                var result = new JObject
                {
                    ["message"] = $"Updated self state: {key} = {value}",
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in update self state function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error updating self state: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleRemoveLongMemory(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("key", out var keyObj) || keyObj is not string key)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid key parameter");
                }

                await _chatClientProviderService.RemoveMemory(_currentUserId, ChatMemoryType.LongTerm, key);
                
                var embed = new JObject
                {
                    ["color"] = "DarkRed",
                    ["description"] = $"Removed long-term memory: \n{key}"
                };
                
                var result = new JObject
                {
                    ["message"] = $"Removed long-term memory: {key}",
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in remove long memory function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error removing long memory: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleRemoveSelfState(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("key", out var keyObj) || keyObj is not string key)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid key parameter");
                }

                await _chatClientProviderService.RemoveMemory(_currentUserId, ChatMemoryType.SelfState, key);
                
                var embed = new JObject
                {
                    ["color"] = "DarkRed",
                    ["description"] = $"Removed self state: \n{key}"
                };
                
                var result = new JObject
                {
                    ["message"] = $"Removed self state: {key}",
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in remove self state function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error removing self state: {ex.Message}");
            }
        }

        private async Task<FunctionResultContent> HandleModifyUserGood(FunctionCallContent call, CancellationToken cancellationToken)
        {
            try
            {
                var args = call.Arguments;
                if (!args.TryGetValue("value", out var valueObj) || 
                    (valueObj is not int intValue && !int.TryParse(valueObj.ToString(), out intValue)))
                {
                    return new FunctionResultContent(call.CallId, call.Name, "Invalid value parameter");
                }

                var reason = "";
                if (args.TryGetValue("reason", out var reasonObj) && reasonObj is string reasonStr)
                {
                    reason = reasonStr;
                }

                if (intValue == 0)
                {
                    return new FunctionResultContent(call.CallId, call.Name, "No change needed for zero value");
                }

                var (_, userRecord) = await _databaseProviderService.GetOrCreateAsync<ChatUserInformation>(_currentUserId);
                userRecord.Good += intValue;
                await _databaseProviderService.InsertOrUpdateAsync(userRecord);
                await _chatClientProviderService.RecordChatDataChangeHistory(_currentUserId, "good", intValue, reason, _currentTimestamp);

                Color color;
                string modifyTag;
                if (intValue > 0)
                {
                    color = Color.Green;
                    modifyTag = "Increased";
                }
                else
                {
                    color = Color.Red;
                    modifyTag = "Decreased";
                }

                var description = string.IsNullOrWhiteSpace(reason)
                    ? $"{modifyTag} by {Math.Abs(intValue)} points, current points: {userRecord.Good}"
                    : $"{modifyTag} by {Math.Abs(intValue)} points, current points: {userRecord.Good} ({reason})";

                var embed = new JObject
                {
                    ["color"] = color.ToString(),
                    ["description"] = description
                };
                
                var result = new JObject
                {
                    ["message"] = description,
                    ["embed"] = embed
                };

                return new FunctionResultContent(call.CallId, call.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in modify user good function");
                return new FunctionResultContent(call.CallId, call.Name, $"Error modifying user good: {ex.Message}");
            }
        }
    }
}