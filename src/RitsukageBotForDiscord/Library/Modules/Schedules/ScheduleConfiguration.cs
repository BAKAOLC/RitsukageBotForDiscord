namespace RitsukageBot.Library.Modules.Schedules
{
    /// <summary>
    ///     Schedule configuration base.
    /// </summary>
    public abstract class ScheduleConfigurationBase
    {
        /// <summary>
        ///     Schedule GUID.
        /// </summary>
        public Guid Guid { get; init; } = Guid.NewGuid();

        /// <summary>
        ///     Schedule type.
        /// </summary>
        public ScheduleType ScheduleType { get; protected init; }

        /// <summary>
        ///     Is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        ///     Is finished.
        /// </summary>
        public bool IsFinished { get; set; }

        /// <summary>
        ///     Schedule time.
        /// </summary>
        public DateTimeOffset ScheduleTime { get; set; }

        /// <summary>
        ///     Last executed time.
        /// </summary>
        public DateTimeOffset LastExecutedTime { get; set; }

        /// <summary>
        ///     Next executed time.
        /// </summary>
        public abstract DateTimeOffset NextExecutedTime { get; }

        /// <summary>
        ///     Executed times.
        /// </summary>
        public ulong ExecutedTimes { get; set; }
    }

    /// <summary>
    ///     One-time schedule configuration.
    /// </summary>
    public class OneTimeScheduleConfiguration : ScheduleConfigurationBase
    {
        /// <inheritdoc />
        public OneTimeScheduleConfiguration()
        {
            ScheduleType = ScheduleType.Once;
        }

        /// <inheritdoc />
        public override DateTimeOffset NextExecutedTime => IsFinished ? DateTime.MaxValue : ScheduleTime;
    }

    /// <summary>
    ///     Periodic schedule configuration.
    /// </summary>
    public class PeriodicScheduleConfiguration : ScheduleConfigurationBase
    {
        /// <inheritdoc />
        public PeriodicScheduleConfiguration()
        {
            ScheduleType = ScheduleType.Periodic;
        }

        /// <summary>
        ///     Periodic interval.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <inheritdoc />
        public override DateTimeOffset NextExecutedTime => LastExecutedTime + Interval;
    }

    /// <summary>
    ///     Countdown schedule configuration.
    /// </summary>
    public class CountdownScheduleConfiguration : PeriodicScheduleConfiguration
    {
        /// <inheritdoc />
        public CountdownScheduleConfiguration()
        {
            ScheduleType = ScheduleType.Countdown;
        }

        /// <summary>
        ///     Target times.
        /// </summary>
        public ulong TargetTimes { get; set; }

        /// <inheritdoc />
        public override DateTimeOffset NextExecutedTime => IsFinished ? DateTimeOffset.MaxValue : base.NextExecutedTime;
    }

    /// <summary>
    ///     Until time schedule configuration.
    /// </summary>
    public class UntilTimeScheduleConfiguration : PeriodicScheduleConfiguration
    {
        /// <inheritdoc />
        public UntilTimeScheduleConfiguration()
        {
            ScheduleType = ScheduleType.UntilTime;
        }

        /// <summary>
        ///     Target time.
        /// </summary>
        public DateTimeOffset TargetTime { get; set; }

        /// <summary>
        ///     Execute when target time.
        /// </summary>
        public bool ForceExecuteWhenTargetTime { get; set; }

        /// <inheritdoc />
        public override DateTimeOffset NextExecutedTime
        {
            get
            {
                if (IsFinished) return DateTime.MaxValue;
                if (base.NextExecutedTime >= TargetTime)
                    return ForceExecuteWhenTargetTime ? TargetTime : DateTime.MaxValue;
                return base.NextExecutedTime;
            }
        }
    }
}