namespace RitsukageBot.Library.Custom.Structs.Times
{
    /// <summary>
    ///     ShouSiDateTime
    /// </summary>
    public readonly struct ShouSiDateTime
    {
        /// <summary>
        ///     Base date
        /// </summary>
        public static readonly DateTime BaseDate = new DateTime(2021, 08, 21).Date;

        /// <summary>
        ///     Time zone
        /// </summary>
        public static readonly TimeSpan TimeZone = TimeSpan.FromHours(8);

        /// <summary>
        ///     Year
        /// </summary>
        public int Year { get; }

        /// <summary>
        ///     Month
        /// </summary>
        public int Month { get; }

        /// <summary>
        ///     Day
        /// </summary>
        public int Day { get; }

        /// <summary>
        ///     Hour
        /// </summary>
        public int Hour => TimeOfDay.Hours;

        /// <summary>
        ///     Minute
        /// </summary>
        public int Minute => TimeOfDay.Minutes;

        /// <summary>
        ///     Second
        /// </summary>
        public int Second => TimeOfDay.Seconds;

        /// <summary>
        ///     Millisecond
        /// </summary>
        public int Millisecond => TimeOfDay.Milliseconds;

        /// <summary>
        ///     Microsecond
        /// </summary>
        public int Microsecond => TimeOfDay.Microseconds;

        /// <summary>
        ///     Nanosecond
        /// </summary>
        public int Nanosecond => TimeOfDay.Nanoseconds;

        /// <summary>
        ///     Time of day
        /// </summary>
        public TimeSpan TimeOfDay { get; }

        private static bool IsLeap(int year)
        {
            return year % 4 == 0 && year % 100 != 0 || year % 400 == 0;
        }

        private static readonly int[] Days = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
        private static readonly int[] LeapDays = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

        /// <summary>
        ///     Initialize a new instance of <see cref="ShouSiDateTime" />
        /// </summary>
        /// <param name="now"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ShouSiDateTime(DateTime now)
        {
            TimeOfDay = now.TimeOfDay;
            var dt = now - BaseDate;
            if (dt.TotalSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(now), "寿司历于2021年08月21号开始计时");
            var year = 1;
            var month = 1;
            var day = 1 + dt.Days;
            var n = 1;
            while (day > 0)
            {
                var days = 365;
                if (IsLeap(n))
                    days = 366;
                if (day > days)
                {
                    year++;
                    day -= days;
                }
                else
                    break;

                n++;
            }

            n = 1;
            var leapDays = IsLeap(year) ? LeapDays : Days;
            while (day > 0)
            {
                var days = leapDays[n - 1];
                if (day > days)
                {
                    month++;
                    day -= days;
                }
                else
                    break;

                if (n == 12)
                    n = 1;
                else
                    n++;
            }

            Year = year;
            Month = month;
            Day = day;
        }

        /// <summary>
        ///     Get current time
        /// </summary>
        public static ShouSiDateTime Now => new(DateTime.Now);

        /// <summary>
        ///     Get current Utc time
        /// </summary>
        public static ShouSiDateTime UtcNow => new(DateTime.UtcNow);

        /// <summary>
        ///     Get current time
        /// </summary>
        public static ShouSiDateTime Today => new(DateTime.Today);
    }
}