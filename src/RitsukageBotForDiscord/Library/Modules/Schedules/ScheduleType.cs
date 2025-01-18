namespace RitsukageBot.Library.Modules.Schedules
{
    /// <summary>
    ///     Schedule type.
    /// </summary>
    public enum ScheduleType
    {
        /// <summary>
        ///     Run once.
        /// </summary>
        Once,

        /// <summary>
        ///     Run every x.
        /// </summary>
        Periodic,

        /// <summary>
        ///     Run x times.
        /// </summary>
        Countdown,

        /// <summary>
        ///     Run until.
        /// </summary>
        UntilTime,
    }
}