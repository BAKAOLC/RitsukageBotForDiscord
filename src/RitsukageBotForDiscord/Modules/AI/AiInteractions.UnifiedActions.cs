using System.Text;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.OpenApi;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;
using Discord.WebSocket;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        /// <summary>
        /// Unified action processing that can be used across all phases (preprocessing, processing, postprocessing)
        /// </summary>
        /// <param name="json">JSON array containing actions to process</param>
        /// <param name="phase">The phase this is being called from (for context)</param>
        /// <returns>Array of embed builders or string results depending on phase</returns>
        private async Task<object[]> ProcessUnifiedActions(string json, ActionPhase phase)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            var result = new List<object>();
            
            try
            {
                var actionArrayData = JArray.Parse(json);
                Logger.LogInformation("Processing unified actions in {Phase} phase: {ActionArrayData}", phase, json);
                
                var showGoodChange = ChatClientProvider.GetConfig<bool>("ShowGoodChange");
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

                        var actionResult = actionType switch
                        {
                            // Preprocessing actions (return strings)
                            "web_search" => await ProcessWebSearch(data).ConfigureAwait(false),
                            "date_base_info" => await ProcessDateBaseInfo(data).ConfigureAwait(false),
                            "range_date_base_info" => await ProcessRangeDateBaseInfo(data).ConfigureAwait(false),
                            "bilibili_video_info" => await ProcessBilibiliVideoInfo(data).ConfigureAwait(false),
                            "bilibili_user_info" => await ProcessBilibiliUserInfo(data).ConfigureAwait(false),
                            "bilibili_live_info" => await ProcessBilibiliLiveInfo(data).ConfigureAwait(false),
                            
                            // Processing/Chatting actions (return EmbedBuilder or null)
                            "good" => await ProcessModifyGood(data, showGoodChange).ConfigureAwait(false),
                            "add_short_memory" => await ProcessAddShortMemory(data, showMemoryChange).ConfigureAwait(false),
                            "add_long_memory" => await ProcessAddLongMemory(data, showMemoryChange).ConfigureAwait(false),
                            "update_self_state" => await ProcessUpdateSelfState(data, showMemoryChange).ConfigureAwait(false),
                            "remove_long_memory" or "remove_chat_history" => await ProcessRemoveLongMemory(data, showMemoryChange).ConfigureAwait(false),
                            "remove_self_state" => await ProcessRemoveSelfState(data, showMemoryChange).ConfigureAwait(false),
                            "query_user_id" => await ProcessQueryUserId(data).ConfigureAwait(false),
                            "get_user_info" => await ProcessGetUserInfo(data).ConfigureAwait(false),
                            "list_channels" => await ProcessListChannels(data).ConfigureAwait(false),
                            "send_dm" => await ProcessSendDm(data).ConfigureAwait(false),
                            "send_channel_message" => await ProcessSendChannelMessage(data).ConfigureAwait(false),
                            "random_users" => await ProcessRandomUsers(data).ConfigureAwait(false),
                            "create_scheduled_task" => await ProcessCreateScheduledTask(data).ConfigureAwait(false),
                            "list_scheduled_tasks" => await ProcessListScheduledTasks(data).ConfigureAwait(false),
                            "delete_scheduled_task" => await ProcessDeleteScheduledTask(data).ConfigureAwait(false),
                            "toggle_scheduled_task" => await ProcessToggleScheduledTask(data).ConfigureAwait(false),
                            
                            _ => null,
                        };

                        if (actionResult is not null)
                            result.Add(actionResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while processing the JSON action: {Json}", data.ToString());
                        
                        // Return appropriate error format based on phase
                        if (phase == ActionPhase.Preprocessing)
                            result.Add($"[Error processing {data.Value<string>("action")}]: {ex.Message}");
                        else
                        {
                            var errorEmbed = new EmbedBuilder();
                            errorEmbed.WithColor(Color.Red);
                            errorEmbed.WithDescription($"Error processing action: {ex.Message}");
                            result.Add(errorEmbed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while parsing the JSON header: {JsonHeader}", json);
                
                if (phase == ActionPhase.Preprocessing)
                    result.Add($"[Error parsing JSON]: {ex.Message}");
                else
                {
                    var errorEmbed = new EmbedBuilder();
                    errorEmbed.WithColor(Color.Red);
                    errorEmbed.WithDescription("An error occurred while processing the response");
                    result.Add(errorEmbed);
                }
            }

            return [.. result];
        }

        #region Preprocessing Actions (return strings)

        private async Task<string> ProcessWebSearch(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for web search action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for web search action");
            var param = paramToken.ToObject<UnifiedActionParam.WebSearchActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for web search action");
            if (string.IsNullOrWhiteSpace(param.Query))
                throw new InvalidDataException("Invalid query for web search action");
            var result = await GoogleSearchProviderService.WebSearch(param.Query).ConfigureAwait(false);
            var resultStrings = result.Take(5).Select(x => $"# {x.Title}\n{x.Snippet}\n\n{x.Link}");
            return $"[Google Search: \"{param.Query}\"]\n{string.Join("\n\n", resultStrings)}";
        }

        private static async Task<string> ProcessDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for date base info action");
            var param = paramToken.ToObject<UnifiedActionParam.DateBaseInfoActionParam>()
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

        private static async Task<string> ProcessRangeDateBaseInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for range date base info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for range  date base info action");
            var param = paramToken.ToObject<UnifiedActionParam.RangeDateBaseInfoActionParam>()
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

        private async Task<string> ProcessBilibiliVideoInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var param = paramToken.ToObject<UnifiedActionParam.BilibiliVideoInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili video info action");
            var playerService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await playerService.GetVideoPageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Video Info: {param.Id}]\n{InformationStringBuilder.BuildVideoInfo(info)}";
        }

        private async Task<string> ProcessBilibiliUserInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var param = paramToken.ToObject<UnifiedActionParam.BilibiliUserInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili user info action");
            var userService = BiliKernelProviderService.GetRequiredService<IUserService>();
            var info = await userService.GetUserInformationAsync(param.Id.ToString())
                .ConfigureAwait(false);
            return $"[Bilibili User Info: {param.Id}]\n{InformationStringBuilder.BuildUserInfo(info)}";
        }

        private async Task<string> ProcessBilibiliLiveInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var param = paramToken.ToObject<UnifiedActionParam.BilibiliLiveInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for bilibili live info action");
            var liveService = BiliKernelProviderService.GetRequiredService<IPlayerService>();
            var info = await liveService.GetLivePageDetailAsync(new(param.Id.ToString(), null, null))
                .ConfigureAwait(false);
            return $"[Bilibili Live Info: {param.Id}]\n{InformationStringBuilder.BuildLiveInfo(info)}";
        }

        #endregion

        #region Processing/Chatting Actions (return EmbedBuilder or null)

        private async Task<EmbedBuilder?> ProcessModifyGood(JObject data, bool showGoodChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for good action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for good action");
            var param = paramToken.ToObject<UnifiedActionParam.GoodActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for good action");
            if (param.Value == 0) return null;

            var (_, userRecord) = await DatabaseProviderService
                .GetOrCreateAsync<ChatUserInformation>(Context.User.Id)
                .ConfigureAwait(false);
            userRecord.Good += param.Value;
            await DatabaseProviderService.InsertOrUpdateAsync(userRecord).ConfigureAwait(false);
            await ChatClientProvider.RecordChatDataChangeHistory(Context.User.Id, "good", param.Value, param.Reason,
                Context.Interaction.CreatedAt).ConfigureAwait(false);
            
            if (!showGoodChange) return null;
                
            Color color;
            string modifyTag;
            if (param.Value > 0)
            {
                color = Color.Green;
                modifyTag = "Increased";
            }
            else
            {
                color = Color.Red;
                modifyTag = "Decreased";
            }

            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(color);
            embedBuilder.WithDescription(
                string.IsNullOrWhiteSpace(param.Reason)
                    ? $"{modifyTag} by {Math.Abs(param.Value)} points, current points: {userRecord.Good}"
                    : $"{modifyTag} by {Math.Abs(param.Value)} points, current points: {userRecord.Good} ({param.Reason})");
            return embedBuilder;
        }

        private async Task<EmbedBuilder?> ProcessAddShortMemory(JObject data, bool showMemoryChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_short_memory action");
            var param = paramToken.ToObject<UnifiedActionParam.MemoryActionParam>()
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

            if (sb.Length == 0 || !showMemoryChange) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added short-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessAddLongMemory(JObject data, bool showMemoryChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for add_long_memory action");
            var param = paramToken.ToObject<UnifiedActionParam.MemoryActionParam>()
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

            if (sb.Length == 0 || !showMemoryChange) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Added long-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessRemoveLongMemory(JObject data, bool showMemoryChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_long_memory action");
            var param = paramToken.ToObject<UnifiedActionParam.RemoveMemoryActionParam>()
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

            if (sb.Length == 0 || !showMemoryChange) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed long-term memory: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessUpdateSelfState(JObject data, bool showMemoryChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for update_self_state action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for update_self_state action");
            var param = paramToken.ToObject<UnifiedActionParam.MemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for update_self_state action");
            if (param.Data.Count == 0)
                throw new InvalidDataException("Invalid JSON data for update_self_state action");
            var sb = new StringBuilder();
            foreach (var (key, value) in param.Data)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value?.ToString()))
                    continue;
                await ChatClientProvider
                    .InsertMemory(Context.User.Id, ChatMemoryType.SelfState, key, value.ToString())
                    .ConfigureAwait(false);
                sb.Append($"{key} = {value}\n");
            }

            if (sb.Length == 0 || !showMemoryChange) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkGreen);
            embed.WithDescription($"Updated self state: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessRemoveSelfState(JObject data, bool showMemoryChange)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            var param = paramToken.ToObject<UnifiedActionParam.RemoveMemoryActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            if (param.Keys.Length == 0)
                throw new InvalidDataException("Invalid JSON data for remove_self_state action");
            var sb = new StringBuilder();
            foreach (var key in param.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                await ChatClientProvider.RemoveMemory(Context.User.Id, ChatMemoryType.SelfState, key)
                    .ConfigureAwait(false);
                sb.Append($"{key}\n");
            }

            if (sb.Length == 0 || !showMemoryChange) return null;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkRed);
            embed.WithDescription($"Removed self state: \n{sb}");
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessQueryUserId(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for query_user_id action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for query_user_id action");
            var param = paramToken.ToObject<UnifiedActionParam.QueryUserIdActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for query_user_id action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var users = Context.Guild.Users
                .Where(u => u.Username.Contains(param.Username, StringComparison.OrdinalIgnoreCase) ||
                           (u.GlobalName?.Contains(param.Username, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(10)
                .ToList();

            if (!users.Any())
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "User Search",
                    Description = $"No users found with username containing: {param.Username}",
                    Color = Color.Orange,
                };
                return notFoundEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "User Search Results",
                Description = $"Found {users.Count} user(s) matching: {param.Username}",
                Color = Color.Green,
            };

            foreach (var user in users)
            {
                var fieldName = user.GlobalName ?? user.Username;
                var fieldValue = $"ID: `{user.Id}`\nUsername: {user.Username}\nMention: <@{user.Id}>";
                embed.AddField(fieldName, fieldValue, true);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessGetUserInfo(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for get_user_info action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for get_user_info action");
            var param = paramToken.ToObject<UnifiedActionParam.GetUserInfoActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for get_user_info action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            if (!ulong.TryParse(param.UserId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var user = Context.Guild.GetUser(id);
            if (user is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "User Information",
                    Description = $"User with ID `{id}` not found in this server.",
                    Color = Color.Orange,
                };
                return notFoundEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "User Information",
                Color = Color.Blue,
            };

            embed.WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());
            embed.AddField("Display Name", user.DisplayName, true);
            embed.AddField("Username", user.Username, true);
            embed.AddField("User ID", user.Id.ToString(), true);
            embed.AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F>", true);
            embed.AddField("Joined Server", user.JoinedAt.HasValue ? $"<t:{user.JoinedAt.Value.ToUnixTimeSeconds()}:F>" : "Unknown", true);
            embed.AddField("Is Bot", user.IsBot ? "Yes" : "No", true);

            if (user.Roles.Any(r => r.Id != Context.Guild.EveryoneRole.Id))
            {
                var roles = string.Join(", ", user.Roles.Where(r => r.Id != Context.Guild.EveryoneRole.Id).Select(r => r.Mention));
                embed.AddField("Roles", roles.Length > 1024 ? "Too many roles to display" : roles);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessListChannels(JObject data)
        {
            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var textChannels = Context.Guild.TextChannels
                .Where(c => Context.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                .OrderBy(c => c.Position)
                .ToList();

            var voiceChannels = Context.Guild.VoiceChannels
                .Where(c => Context.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                .OrderBy(c => c.Position)
                .ToList();

            var embed = new EmbedBuilder
            {
                Title = "Accessible Channels",
                Color = Color.Green,
            };

            if (textChannels.Any())
            {
                var textChannelList = string.Join("\n", textChannels.Take(20).Select(c => $"<#{c.Id}> (`{c.Id}`)"));
                if (textChannels.Count > 20)
                    textChannelList += $"\n... and {textChannels.Count - 20} more";
                embed.AddField($"Text Channels ({textChannels.Count})", textChannelList);
            }

            if (voiceChannels.Any())
            {
                var voiceChannelList = string.Join("\n", voiceChannels.Take(20).Select(c => $"{c.Name} (`{c.Id}`)"));
                if (voiceChannels.Count > 20)
                    voiceChannelList += $"\n... and {voiceChannels.Count - 20} more";
                embed.AddField($"Voice Channels ({voiceChannels.Count})", voiceChannelList);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessSendDm(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for send_dm action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for send_dm action");
            var param = paramToken.ToObject<UnifiedActionParam.SendDmActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for send_dm action");

            // Check if the bot owner is performing this action (security check)
            if (Context.User.Id != Context.Client.Application.Owner.Id)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "Only the bot owner can send direct messages through AI actions.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            if (!ulong.TryParse(param.UserId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid user ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var user = Context.Client.GetUser(id);
            if (user is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"User with ID `{id}` not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            try
            {
                await user.SendMessageAsync(param.Message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent private message to {user.Username} (`{user.Id}`)",
                    Color = Color.Green,
                };
                return successEmbed;
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }
        }

        private async Task<EmbedBuilder?> ProcessSendChannelMessage(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for send_channel_message action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for send_channel_message action");
            var param = paramToken.ToObject<UnifiedActionParam.SendChannelMessageActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for send_channel_message action");

            // Check if the user has admin permissions (security check)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "Only administrators can send channel messages through AI actions.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            if (!ulong.TryParse(param.ChannelId, out var id))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid channel ID format.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var channel = Context.Client.GetChannel(id) as IMessageChannel;
            if (channel is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Channel with ID `{id}` not found or is not a message channel.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            try
            {
                await channel.SendMessageAsync(param.Message).ConfigureAwait(false);
                var successEmbed = new EmbedBuilder
                {
                    Title = "Message Sent",
                    Description = $"Successfully sent message to <#{channel.Id}>",
                    Color = Color.Green,
                };
                return successEmbed;
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Failed to send message: {ex.Message}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }
        }

        private async Task<EmbedBuilder?> ProcessRandomUsers(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for random_users action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for random_users action");
            var param = paramToken.ToObject<UnifiedActionParam.RandomUsersActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for random_users action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var count = Math.Clamp(param.Count, 1, 10);
            var users = Context.Guild.Users
                .Where(u => !u.IsBot && u.Status != UserStatus.Offline)
                .ToList();

            if (!users.Any())
            {
                var noUsersEmbed = new EmbedBuilder
                {
                    Title = "Random Users",
                    Description = "No online users found in this server.",
                    Color = Color.Orange,
                };
                return noUsersEmbed;
            }

            var randomUsers = users.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();

            var embed = new EmbedBuilder
            {
                Title = "Random Users",
                Description = $"Selected {randomUsers.Count} random online user(s):",
                Color = Color.Blue,
            };

            foreach (var user in randomUsers)
            {
                var fieldName = user.DisplayName;
                var fieldValue = $"Username: {user.Username}\nID: `{user.Id}`\nStatus: {user.Status}";
                embed.AddField(fieldName, fieldValue, true);
            }

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessCreateScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");
            var param = paramToken.ToObject<UnifiedActionParam.CreateScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for create_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Parse schedule time
            if (!DateTime.TryParseExact(param.ScheduleTime, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedTime))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Invalid schedule time format. Use: yyyy-MM-dd HH:mm (e.g., 2024-12-25 14:30)",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var scheduleTimeOffset = new DateTimeOffset(parsedTime, TimeZoneInfo.Local.GetUtcOffset(parsedTime));

            // Validate channel ID if provided
            ulong? channelIdParsed = null;
            if (!string.IsNullOrWhiteSpace(param.ChannelId))
            {
                if (!ulong.TryParse(param.ChannelId, out var cId))
                {
                    var errorEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "Invalid channel ID format.",
                        Color = Color.Red,
                    };
                    return errorEmbed;
                }
                channelIdParsed = cId;

                var channel = Context.Client.GetChannel(cId);
                if (channel is null)
                {
                    var errorEmbed = new EmbedBuilder
                    {
                        Title = "Error",
                        Description = "Channel not found.",
                        Color = Color.Red,
                    };
                    return errorEmbed;
                }
            }

            // Validate AI role if provided
            if (!string.IsNullOrWhiteSpace(param.AiRole) && !ChatClientProvider.GetRoleData(out _, out _, param.AiRole))
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = $"Invalid AI role: {param.AiRole}",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Create task
            var task = new AiScheduledTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = param.Name,
                UserId = Context.User.Id,
                GuildId = Context.Guild.Id,
                ChannelId = channelIdParsed,
                Prompt = param.Prompt,
                AiRole = param.AiRole,
                ScheduleType = param.ScheduleType,
                ScheduleTime = scheduleTimeOffset,
                IntervalSeconds = param.ScheduleType is "Periodic" or "Countdown" or "UntilTime" ? param.IntervalMinutes * 60L : null,
                TargetTimes = param.ScheduleType == "Countdown" ? (ulong)param.TargetTimes : null,
                TargetTime = param.ScheduleType == "UntilTime" ? scheduleTimeOffset.AddMinutes(param.IntervalMinutes * param.TargetTimes) : null,
                IsEnabled = true,
                CreatedTime = DateTimeOffset.UtcNow,
                UpdatedTime = DateTimeOffset.UtcNow
            };

            await DatabaseProviderService.InsertOrUpdateAsync(task).ConfigureAwait(false);

            var embed = new EmbedBuilder
            {
                Title = "AI Scheduled Task Created",
                Description = $"Task **{param.Name}** has been created successfully.",
                Color = Color.Green,
            };

            embed.AddField("Task ID", task.Id, true);
            embed.AddField("Schedule Type", param.ScheduleType, true);
            embed.AddField("Schedule Time", $"<t:{scheduleTimeOffset.ToUnixTimeSeconds()}:F>", true);
            embed.AddField("Output", channelIdParsed.HasValue ? $"<#{channelIdParsed.Value}>" : "Direct Message", true);
            if (!string.IsNullOrWhiteSpace(param.AiRole))
                embed.AddField("AI Role", param.AiRole, true);

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessListScheduledTasks(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");
            var param = paramToken.ToObject<UnifiedActionParam.ListScheduledTasksActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for list_scheduled_tasks action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            var query = param.ShowAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? "SELECT * FROM AiScheduledTask WHERE guild_id = ? ORDER BY created_time DESC"
                : "SELECT * FROM AiScheduledTask WHERE guild_id = ? AND user_id = ? ORDER BY created_time DESC";

            var parameters = param.ShowAll && Context.User.Id == Context.Client.Application.Owner.Id
                ? new object[] { Context.Guild.Id }
                : new object[] { Context.Guild.Id, Context.User.Id };

            var tasks = await DatabaseProviderService.QueryAsync<AiScheduledTask>(query, parameters).ConfigureAwait(false);

            if (!tasks.Any())
            {
                var noTasksEmbed = new EmbedBuilder
                {
                    Title = "AI Scheduled Tasks",
                    Description = param.ShowAll ? "No scheduled tasks found in this server." : "You have no scheduled tasks.",
                    Color = Color.Orange,
                };
                return noTasksEmbed;
            }

            var embed = new EmbedBuilder
            {
                Title = "AI Scheduled Tasks",
                Description = $"Found {tasks.Count} task(s)",
                Color = Color.Blue,
            };

            foreach (var task in tasks.Take(10)) // Limit to first 10 tasks
            {
                var statusIcon = task.IsFinished ? "✅" : (task.IsEnabled ? "🟢" : "⏸️");
                var fieldName = $"{statusIcon} {task.Name}";
                
                var fieldValue = $"ID: `{task.Id[..8]}...`\n" +
                                $"Type: {task.ScheduleType}\n" +
                                $"Schedule: <t:{task.ScheduleTime.ToUnixTimeSeconds()}:R>\n" +
                                $"Executed: {task.ExecutedTimes} times";

                if (task.LastExecutedTime.HasValue)
                    fieldValue += $"\nLast: <t:{task.LastExecutedTime.Value.ToUnixTimeSeconds()}:R>";

                if (param.ShowAll)
                    fieldValue += $"\nCreator: <@{task.UserId}>";

                embed.AddField(fieldName, fieldValue, true);
            }

            if (tasks.Count > 10)
                embed.WithFooter($"Showing 10 of {tasks.Count} tasks");

            embed.WithCurrentTimestamp();
            return embed;
        }

        private async Task<EmbedBuilder?> ProcessDeleteScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");
            var param = paramToken.ToObject<UnifiedActionParam.DeleteScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for delete_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            // Check permissions (user can delete own tasks, admins can delete any)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var isTaskCreator = task.UserId == Context.User.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !isTaskCreator && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "You don't have permission to delete this task.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            // Delete the task
            await DatabaseProviderService.DeleteAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);

            var successEmbed = new EmbedBuilder
            {
                Title = "Task Deleted",
                Description = $"Successfully deleted task **{task.Name}** (`{task.Id[..8]}...`)",
                Color = Color.Green,
            };

            successEmbed.WithCurrentTimestamp();
            return successEmbed;
        }

        private async Task<EmbedBuilder?> ProcessToggleScheduledTask(JObject data)
        {
            if (data is null) throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");
            if (!data.TryGetValue("param", out var paramValue) || paramValue is not JObject paramToken)
                throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");
            var param = paramToken.ToObject<UnifiedActionParam.ToggleScheduledTaskActionParam>()
                        ?? throw new InvalidDataException("Invalid JSON data for toggle_scheduled_task action");

            if (Context.Guild is null)
            {
                var errorEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "This action can only be used in a guild context.",
                    Color = Color.Red,
                };
                return errorEmbed;
            }

            // Find the task
            var task = await DatabaseProviderService.GetAsync<AiScheduledTask>(param.TaskId).ConfigureAwait(false);
            if (task is null)
            {
                var notFoundEmbed = new EmbedBuilder
                {
                    Title = "Error",
                    Description = "Task not found.",
                    Color = Color.Red,
                };
                return notFoundEmbed;
            }

            // Check permissions (same as delete)
            var isOwner = Context.User.Id == Context.Client.Application.Owner.Id;
            var isTaskCreator = task.UserId == Context.User.Id;
            var hasAdminPermission = Context.User is SocketGuildUser guildUser && 
                                    guildUser.GuildPermissions.Administrator;

            if (!isOwner && !isTaskCreator && !hasAdminPermission)
            {
                var permissionEmbed = new EmbedBuilder
                {
                    Title = "Permission Denied",
                    Description = "You don't have permission to modify this task.",
                    Color = Color.Red,
                };
                return permissionEmbed;
            }

            // Toggle the status
            task.IsEnabled = !task.IsEnabled;
            task.UpdatedTime = DateTimeOffset.UtcNow;
            await DatabaseProviderService.InsertOrUpdateAsync(task).ConfigureAwait(false);

            var statusText = task.IsEnabled ? "enabled" : "disabled";
            var statusColor = task.IsEnabled ? Color.Green : Color.Orange;

            var successEmbed = new EmbedBuilder
            {
                Title = "Task Status Updated",
                Description = $"Task **{task.Name}** has been {statusText}.",
                Color = statusColor,
            };

            successEmbed.WithCurrentTimestamp();
            return successEmbed;
        }

        #endregion

        /// <summary>
        /// Enumeration representing the different phases where actions can be processed
        /// </summary>
        private enum ActionPhase
        {
            Preprocessing,
            Processing,
            Postprocessing
        }

        /// <summary>
        /// Unified action parameter classes that can be used across all phases
        /// </summary>
        private static class UnifiedActionParam
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

            internal class GoodActionParam
            {
                [JsonProperty("value")] public int Value { get; set; }
                [JsonProperty("reason")] public string Reason { get; set; } = string.Empty;
            }

            internal class MemoryActionParam
            {
                [JsonProperty("data")] public JObject Data { get; set; } = [];
            }

            internal class RemoveMemoryActionParam
            {
                [JsonProperty("keys")] public string[] Keys { get; set; } = [];
            }

            internal class QueryUserIdActionParam
            {
                [JsonProperty("username")] public string Username { get; set; } = string.Empty;
            }

            internal class GetUserInfoActionParam
            {
                [JsonProperty("userId")] public string UserId { get; set; } = string.Empty;
            }

            internal class SendDmActionParam
            {
                [JsonProperty("userId")] public string UserId { get; set; } = string.Empty;
                [JsonProperty("message")] public string Message { get; set; } = string.Empty;
            }

            internal class SendChannelMessageActionParam
            {
                [JsonProperty("channelId")] public string ChannelId { get; set; } = string.Empty;
                [JsonProperty("message")] public string Message { get; set; } = string.Empty;
            }

            internal class RandomUsersActionParam
            {
                [JsonProperty("count")] public int Count { get; set; } = 5;
            }

            internal class CreateScheduledTaskActionParam
            {
                [JsonProperty("name")] public string Name { get; set; } = string.Empty;
                [JsonProperty("prompt")] public string Prompt { get; set; } = string.Empty;
                [JsonProperty("scheduleTime")] public string ScheduleTime { get; set; } = string.Empty;
                [JsonProperty("scheduleType")] public string ScheduleType { get; set; } = "OneTime";
                [JsonProperty("channelId")] public string? ChannelId { get; set; }
                [JsonProperty("intervalMinutes")] public int IntervalMinutes { get; set; } = 60;
                [JsonProperty("targetTimes")] public int TargetTimes { get; set; } = 1;
                [JsonProperty("aiRole")] public string? AiRole { get; set; }
            }

            internal class ListScheduledTasksActionParam
            {
                [JsonProperty("showAll")] public bool ShowAll { get; set; } = false;
            }

            internal class DeleteScheduledTaskActionParam
            {
                [JsonProperty("taskId")] public string TaskId { get; set; } = string.Empty;
            }

            internal class ToggleScheduledTaskActionParam
            {
                [JsonProperty("taskId")] public string TaskId { get; set; } = string.Empty;
            }
        }
    }
}