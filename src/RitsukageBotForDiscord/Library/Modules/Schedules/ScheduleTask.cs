using Microsoft.Extensions.DependencyInjection;

namespace RitsukageBot.Library.Modules.Schedules
{
    /// <inheritdoc />
    public abstract class ScheduleTask(IServiceProvider serviceProvider) : IScheduledTask
    {
        /// <inheritdoc cref="ScheduleConfigurationBase.Guid" />
        public Guid Guid => Configuration.Guid;

        /// <inheritdoc cref="ScheduleConfigurationBase.ScheduleType" />
        public ScheduleType ScheduleType => Configuration.ScheduleType;

        /// <inheritdoc cref="ScheduleConfigurationBase.IsEnabled" />
        public bool IsEnabled
        {
            get => Configuration.IsEnabled;
            set => Configuration.IsEnabled = value;
        }

        /// <inheritdoc cref="ScheduleConfigurationBase.IsFinished" />
        public bool IsFinished
        {
            get => Configuration.IsFinished;
            private set => Configuration.IsFinished = value;
        }

        /// <inheritdoc cref="ScheduleConfigurationBase.ScheduleTime" />
        public DateTimeOffset ScheduleTime
        {
            get => Configuration.ScheduleTime;
            set => Configuration.ScheduleTime = value;
        }

        /// <inheritdoc cref="ScheduleConfigurationBase.LastExecutedTime" />
        public DateTimeOffset LastExecutedTime => Configuration.LastExecutedTime;

        /// <inheritdoc cref="ScheduleConfigurationBase.NextExecutedTime" />
        public DateTimeOffset NextExecutedTime => Configuration.NextExecutedTime;

        /// <inheritdoc cref="ScheduleConfigurationBase.ExecutedTimes" />
        public ulong ExecutedTimes => Configuration.ExecutedTimes;

        /// <inheritdoc />
        public abstract ScheduleConfigurationBase Configuration { get; }

        /// <inheritdoc />
        public IServiceProvider Services { get; } = serviceProvider;

        /// <inheritdoc />
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        ///     Trigger the task.
        /// </summary>
        /// <param name="triggerTime"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task TriggerAsync(DateTimeOffset triggerTime, CancellationToken cancellationToken)
        {
            Configuration.ExecutedTimes++;
            Configuration.LastExecutedTime = triggerTime;
            if (Configuration is OneTimeScheduleConfiguration ||
                (Configuration is CountdownScheduleConfiguration countdownConfiguration &&
                 countdownConfiguration.TargetTimes == Configuration.ExecutedTimes) ||
                (Configuration is UntilTimeScheduleConfiguration untilTimeConfiguration &&
                 untilTimeConfiguration.TargetTime <= Configuration.LastExecutedTime)
               ) IsFinished = true;
            return ExecuteAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public abstract class OneTimeScheduleTask(IServiceProvider serviceProvider) : ScheduleTask(serviceProvider)
    {
    }

    /// <inheritdoc />
    public abstract class PeriodicScheduleTask(IServiceProvider serviceProvider) : ScheduleTask(serviceProvider)
    {
        /// <summary>
        ///     Interval.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public TimeSpan Interval
        {
            get
            {
                if (Configuration is PeriodicScheduleConfiguration periodicConfiguration)
                    return periodicConfiguration.Interval;
                return TimeSpan.Zero;
            }
            set
            {
                if (Configuration is PeriodicScheduleConfiguration periodicConfiguration)
                    periodicConfiguration.Interval = value;
                throw new InvalidOperationException("Cannot set interval for a non-periodic schedule.");
            }
        }
    }

    /// <inheritdoc />
    public abstract class CountdownScheduleTask(IServiceProvider serviceProvider)
        : PeriodicScheduleTask(serviceProvider)
    {
        /// <summary>
        ///     Target times.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public ulong TargetTimes
        {
            get
            {
                return Configuration switch
                {
                    CountdownScheduleConfiguration countdownConfiguration => countdownConfiguration.TargetTimes,
                    PeriodicScheduleConfiguration => ulong.MaxValue,
                    _ => 0,
                };
            }
            set
            {
                if (Configuration is CountdownScheduleConfiguration countdownConfiguration)
                    countdownConfiguration.TargetTimes = value;
                throw new InvalidOperationException("Cannot set target times for a non-countdown schedule.");
            }
        }
    }

    /// <inheritdoc />
    public abstract class UntilTimeScheduleTask(IServiceProvider serviceProvider)
        : PeriodicScheduleTask(serviceProvider)
    {
        /// <summary>
        ///     Target time.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public DateTimeOffset TargetTime
        {
            get
            {
                return Configuration switch
                {
                    UntilTimeScheduleConfiguration untilTimeConfiguration => untilTimeConfiguration.TargetTime,
                    PeriodicScheduleConfiguration => DateTimeOffset.MaxValue,
                    _ => DateTimeOffset.MinValue,
                };
            }
            set
            {
                if (Configuration is UntilTimeScheduleConfiguration untilTimeConfiguration)
                    untilTimeConfiguration.TargetTime = value;
                throw new InvalidOperationException("Cannot set target time for a non-until-time schedule.");
            }
        }

        /// <summary>
        ///     Execute when target time.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public bool ForceExecuteWhenTargetTime
        {
            get => Configuration switch
            {
                UntilTimeScheduleConfiguration untilTimeConfiguration => untilTimeConfiguration
                    .ForceExecuteWhenTargetTime,
                _ => false,
            };
            set
            {
                if (Configuration is UntilTimeScheduleConfiguration untilTimeConfiguration)
                    untilTimeConfiguration.ForceExecuteWhenTargetTime = value;
                throw new InvalidOperationException(
                    "Cannot set force execute when target time for a non-until-time schedule.");
            }
        }
    }
}