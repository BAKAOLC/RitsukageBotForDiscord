namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Time utility class.
    /// </summary>
    public static class TimeUtility
    {
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string TimeFormatWithoutSpace = "yyyy-MM-dd_HH:mm:ss";
        private static readonly TimeSpan Offset = TimeSpan.FromHours(8);

        /// <summary>
        ///     Gets the current time in settings offset.
        /// </summary>
        public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to the settings offset.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static DateTimeOffset ConvertToSettingsOffset(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToOffset(Offset);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTime" /> to the settings offset.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTimeOffset AsSettingsOffset(this DateTime dateTime)
        {
            return new(dateTime, Offset);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to a string.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToTimeString(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(TimeFormat);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to a string without space.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToTimeStringWithoutSpace(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(TimeFormatWithoutSpace);
        }
    }
}