namespace RitsukageBot.Library.Modules.Schedules
{
    /// <summary>
    ///     Scheduled task.
    /// </summary>
    public interface IScheduledTask
    {
        /// <summary>
        ///     Configuration.
        /// </summary>
        ScheduleConfigurationBase Configuration { get; }

        /// <summary>
        ///     Schedule type.
        /// </summary>
        ScheduleType Type => Configuration.ScheduleType;

        /// <summary>
        ///     Services provider.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        ///     Execute the task.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}