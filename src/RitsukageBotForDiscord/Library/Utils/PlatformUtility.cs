namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Platform utility.
    /// </summary>
    public static class PlatformUtility
    {
        /// <summary>
        ///     Get the operating system.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public static PlatformID GetOperatingSystem()
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
    }
}