using System.Reflection;

namespace RitsukageBot.Library.Networking
{
    internal class UserAgent
    {
        private const string Unknown = "Unknown";

        private const string TagMozilla = "Mozilla/5.0";

        private const string TagChrome = "Chrome/114.0.0.0";

        private const string TagSafari = "Safari/537.36";

        private const string TagEdge = "Edge/114.0.1823.43";

        private const string TagAppleWebKit = "AppleWebKit/537.36 (KHTML, like Gecko)";

        private const string TagMozillaWindows = $"{TagMozilla} (Windows NT 10.0; Win64; x64)";

        private const string TagMozillaAndroid = $"{TagMozilla} (Linux; Android 12)";

        private const string TagMozillaLinux = $"{TagMozilla} (X11; Linux x86_64)";

        private const string TagMozillaMac = $"{TagMozilla} (Macintosh; Intel Mac OS X 10_15_7)";

        private const string TagMozillaIos = $"{TagMozilla} (iPhone; CPU iPhone OS 15_0 like Mac OS X)";

        private static readonly string AssemblyAuthor = typeof(UserAgent).Assembly
            .GetCustomAttributes(false)
            .OfType<AssemblyCompanyAttribute>()
            .FirstOrDefault()?.Company ?? Unknown;

        private static readonly string AssemblyName = typeof(UserAgent).Assembly.GetName().Name ?? string.Empty;
        
        private static readonly string AssemblyVersion =  
            typeof(UserAgent).Assembly.GetName().Version?.ToString() ?? string.Empty;

        private static readonly string AssemblyRepositoryUrl = typeof(UserAgent).Assembly
            .GetCustomAttributes(false)
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "RepositoryUrl")?.Value ?? string.Empty;

        internal static readonly string AssemblyUserAgent =
            $"{(string.IsNullOrWhiteSpace(AssemblyName) ? Unknown : AssemblyName)}/{(string.IsNullOrWhiteSpace(AssemblyVersion) ? Unknown : AssemblyVersion)} ({(string.IsNullOrWhiteSpace(AssemblyAuthor) ? Unknown : AssemblyAuthor)}{(string.IsNullOrWhiteSpace(AssemblyRepositoryUrl) ? string.Empty : $", {AssemblyRepositoryUrl}")})";

        internal static readonly string Windows =
            $"{TagMozillaWindows} {TagAppleWebKit} {TagChrome} {TagSafari} {TagEdge} {AssemblyUserAgent}";

        internal static readonly string Android =
            $"{TagMozillaAndroid} {TagAppleWebKit} {TagChrome} {TagSafari} {TagEdge} {AssemblyUserAgent}";

        internal static readonly string Linux =
            $"{TagMozillaLinux} {TagAppleWebKit} {TagChrome} {TagSafari} {TagEdge} {AssemblyUserAgent}";

        internal static readonly string Mac =
            $"{TagMozillaMac} {TagAppleWebKit} {TagChrome} {TagSafari} {TagEdge} {AssemblyUserAgent}";

        internal static readonly string Ios =
            $"{TagMozillaIos} {TagAppleWebKit} {TagChrome} {TagSafari} {TagEdge} {AssemblyUserAgent}";

        internal static string Default =>
            Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => Windows,
                PlatformID.Unix => Linux,
                PlatformID.MacOSX => Mac,
                _ => Windows,
            };
    }
}