using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     AI scheduled task data model.
    /// </summary>
    [Table("AiScheduledTask")]
    public class AiScheduledTask
    {
        /// <summary>
        ///     Task ID (GUID).
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Task name.
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        ///     User ID who created the task.
        /// </summary>
        [Column("user_id")]
        public ulong UserId { get; set; }

        /// <summary>
        ///     Guild ID where the task belongs.
        /// </summary>
        [Column("guild_id")]
        public ulong GuildId { get; set; }

        /// <summary>
        ///     Channel ID for output (optional).
        /// </summary>
        [Column("channel_id")]
        public ulong? ChannelId { get; set; }

        /// <summary>
        ///     Pre-generated prompt for AI.
        /// </summary>
        [Column("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        ///     AI role to use.
        /// </summary>
        [Column("ai_role")]
        public string? AiRole { get; set; }

        /// <summary>
        ///     Schedule type (OneTime, Periodic, Countdown, UntilTime).
        /// </summary>
        [Column("schedule_type")]
        public string ScheduleType { get; set; } = string.Empty;

        /// <summary>
        ///     Schedule time (when to start).
        /// </summary>
        [Column("schedule_time")]
        public DateTimeOffset ScheduleTime { get; set; }

        /// <summary>
        ///     Interval for periodic tasks (in seconds).
        /// </summary>
        [Column("interval_seconds")]
        public long? IntervalSeconds { get; set; }

        /// <summary>
        ///     Target times for countdown tasks.
        /// </summary>
        [Column("target_times")]
        public ulong? TargetTimes { get; set; }

        /// <summary>
        ///     Target time for until-time tasks.
        /// </summary>
        [Column("target_time")]
        public DateTimeOffset? TargetTime { get; set; }

        /// <summary>
        ///     Whether the task is enabled.
        /// </summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        ///     Whether the task is finished.
        /// </summary>
        [Column("is_finished")]
        public bool IsFinished { get; set; } = false;

        /// <summary>
        ///     Last executed time.
        /// </summary>
        [Column("last_executed_time")]
        public DateTimeOffset? LastExecutedTime { get; set; }

        /// <summary>
        ///     Number of times executed.
        /// </summary>
        [Column("executed_times")]
        public ulong ExecutedTimes { get; set; } = 0;

        /// <summary>
        ///     Created time.
        /// </summary>
        [Column("created_time")]
        public DateTimeOffset CreatedTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Updated time.
        /// </summary>
        [Column("updated_time")]
        public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.UtcNow;
    }
}