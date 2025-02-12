namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Time utility class.
    /// </summary>
    public static class TimeUtility
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm:ss";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string DateTimeFormatWithoutSpace = "yyyy-MM-dd_HH:mm:ss";
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
        ///     Converts the given <see cref="DateTimeOffset" /> to a date string.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToDateString(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(DateFormat);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to a time string.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string ToTimeString(this DateTime dateTime)
        {
            return dateTime.ToString(TimeFormat);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to a date time string.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToDateTimeString(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(DateTimeFormat);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to a date time string without space.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToDateTimeStringWithoutSpace(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(DateTimeFormatWithoutSpace);
        }

        /// <summary>
        ///     Converts the given <see cref="DateTimeOffset" /> to iso8601 string.
        /// </summary>
        /// <param name="dateTimeOffset"></param>
        /// <returns></returns>
        public static string ToUtcDateTimeString(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
    }
}