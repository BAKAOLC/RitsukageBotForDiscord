namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Platform utility.
    /// </summary>
    public static class PlatformUtility
    {
        private static PlatformID OperatingSystem { get; } = InnerGetOperatingSystem();

        /// <summary>
        ///     Get the operating system.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public static PlatformID GetOperatingSystem()
        {
            return OperatingSystem;
        }

        private static PlatformID InnerGetOperatingSystem()
        {
            var winDir = Environment.GetEnvironmentVariable("windir");
            if (!string.IsNullOrEmpty(winDir) && winDir.Contains('\\') && Directory.Exists(winDir))
                return PlatformID.Win32NT;

            if (File.Exists(@"/proc/sys/kernel/ostype"))
            {
                var osType = File.ReadAllText(@"/proc/sys/kernel/ostype");
                if (osType.StartsWith("Linux", StringComparison.OrdinalIgnoreCase)) return PlatformID.Unix;
                throw new PlatformNotSupportedException("Unknown OS type: " + osType);
            }

            if (File.Exists(@"/System/Library/CoreServices/SystemVersion.plist"))
                return PlatformID.MacOSX;

            throw new PlatformNotSupportedException("Unknown OS type.");
        }

        /// <summary>
        ///     Check if the operating system is Windows.
        /// </summary>
        /// <returns></returns>
        public static bool IsWindows()
        {
            return OperatingSystem == PlatformID.Win32NT;
        }

        /// <summary>
        ///     Check if the operating system is Linux.
        /// </summary>
        /// <returns></returns>
        public static bool IsLinux()
        {
            return OperatingSystem == PlatformID.Unix;
        }

        /// <summary>
        ///     Check if the operating system is macOS.
        /// </summary>
        /// <returns></returns>
        public static bool IsMacOs()
        {
            return OperatingSystem == PlatformID.MacOSX;
        }
    }
}