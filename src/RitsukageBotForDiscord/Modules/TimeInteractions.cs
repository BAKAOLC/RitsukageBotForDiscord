using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Custom.Structs.Times;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Time interactions
    /// </summary>
    [Group("time", "Time interactions")]
    public class TimeInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Custom ID
        /// </summary>
        public const string CustomId = "time_interaction";

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<ImageInteractions> Logger { get; set; }

        /// <summary>
        ///     Get current time
        /// </summary>
        [SlashCommand("current", "Get current time")]
        public async Task CurrentTimeAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var now = DateTimeOffset.Now;
            await FollowupAsync($"Host Time: {now:yyyy-MM-dd HH:mm:ss zzz}").ConfigureAwait(false);
        }

        /// <summary>
        ///     Get current Utc time
        /// </summary>
        [SlashCommand("utc", "Get current Utc time")]
        public async Task CurrentUtcTimeAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            await FollowupAsync($"Host Utc Time: {now:yyyy-MM-dd HH:mm:ss zzz}").ConfigureAwait(false);
        }

        /// <summary>
        ///     Get current time in YunJue calendar
        /// </summary>
        [SlashCommand("云绝历", "获取bot服务器当前的时间所对应的云绝历时间")]
        public async Task GetYunJueTimeAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var now = DateTimeOffset.Now;
            var day = Math.Floor((now.Date - new DateTimeOffset(2018, 8, 19, 0, 0, 0, TimeSpan.Zero)).TotalDays) + 1;
            await FollowupAsync($"云绝历: 2018-08-{day,00} {now:HH:mm:ss zzz}").ConfigureAwait(false);
        }

        /// <summary>
        ///     Get current time in ShouSi calendar
        /// </summary>
        [SlashCommand("寿司历", "获取bot服务器当前的时间所对应的寿司历时间")]
        public async Task GetShouSiTimeAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var now = ShouSiDateTime.Now;
            var timeZoneString = ShouSiDateTime.TimeZone >= TimeSpan.Zero
                ? $"+{ShouSiDateTime.TimeZone:hh\\:mm}"
                : $"{ShouSiDateTime.TimeZone:hh\\:mm}";
            await FollowupAsync(
                    $"寿司历: {now.Year:0000}-{now.Month:00}-{now.Day:00} {now.Hour:00}:{now.Minute:00}:{now.Second:00} {timeZoneString}")
                .ConfigureAwait(false);
        }


        /// <summary>
        ///     Get current time in Chinese GaoKao countdown
        /// </summary>
        [SlashCommand("高考倒计时", "获取bot服务器当前的时间到高考开始所差的时间")]
        public async Task GetGaoKaoCountdownAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var now = DateTime.Now.Date;
            var target = new DateTime(now.Year, 6, 7, 0, 0, 0);
            var day = Math.Floor((target - now).TotalDays);
            if (day < -90)
            {
                target = target.AddYears(1);
                day = Math.Floor((target - now).TotalDays);
            }

            var message = day switch
            {
                > 3 => $"距离高考还有 {day} 天",
                3 => "距离高考还有 3 天，冲冲冲",
                2 => "距离高考还有 2 天，加油啊",
                1 => "明天就开始高考啦，祝你们好运！",
                < 1 and > -4 => "已经在高考期间啦，考个好成绩回来哦！",
                _ => "考完啦，放松一下吧",
            };
            await FollowupAsync(message).ConfigureAwait(false);
        }
    }
}