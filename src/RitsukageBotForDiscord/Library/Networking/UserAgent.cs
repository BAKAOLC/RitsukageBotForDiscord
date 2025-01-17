﻿using System.Reflection;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Library.Networking
{
    internal class UserAgent
    {
        private const string Unknown = "Unknown";

        private const string Mozilla = "Mozilla/5.0";

        private const string Chrome = "Chrome/114.0.0.0";

        private const string Safari = "Safari/537.36";

        private const string Edge = "Edge/114.0.1823.43";

        private const string AppleWebKit = "AppleWebKit/537.36 (KHTML, like Gecko)";

        private const string MozillaWindows = $"{Mozilla} (Windows NT 10.0; Win64; x64)";

        private const string MozillaAndroid = $"{Mozilla} (Linux; Android 12)";

        private const string MozillaLinux = $"{Mozilla} (X11; Linux x86_64)";

        private const string MozillaMac = $"{Mozilla} (Macintosh; Intel Mac OS X 10_15_7)";

        private const string MozillaIos = $"{Mozilla} (iPhone; CPU iPhone OS 15_0 like Mac OS X)";

        private static readonly string AssemblyAuthor = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(false)
            .OfType<AssemblyCompanyAttribute>()
            .FirstOrDefault()?.Company ?? Unknown;

        private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;

        private static readonly string AssemblyVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

        private static readonly string AssemblyRepositoryUrl = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(false)
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "RepositoryUrl")?.Value ?? string.Empty;

        internal static readonly string AssemblyUserAgent =
            $"{(string.IsNullOrWhiteSpace(AssemblyName) ? Unknown : AssemblyName)}/{(string.IsNullOrWhiteSpace(AssemblyVersion) ? Unknown : AssemblyVersion)} ({(string.IsNullOrWhiteSpace(AssemblyAuthor) ? Unknown : AssemblyAuthor)}{(string.IsNullOrWhiteSpace(AssemblyRepositoryUrl) ? string.Empty : $", {AssemblyRepositoryUrl}")})";

        internal static readonly string Windows =
            $"{MozillaWindows} {AppleWebKit} {Chrome} {Safari} {Edge} {AssemblyUserAgent}";

        internal static readonly string Android =
            $"{MozillaAndroid} {AppleWebKit} {Chrome} {Safari} {Edge} {AssemblyUserAgent}";

        internal static readonly string Linux =
            $"{MozillaLinux} {AppleWebKit} {Chrome} {Safari} {Edge} {AssemblyUserAgent}";

        internal static readonly string Mac =
            $"{MozillaMac} {AppleWebKit} {Chrome} {Safari} {Edge} {AssemblyUserAgent}";

        internal static readonly string Ios =
            $"{MozillaIos} {AppleWebKit} {Chrome} {Safari} {Edge} {AssemblyUserAgent}";

        internal static string Default =>
            // Environment.OSVersion.Platform switch // On MacOs, it will incorrectly return Unix
            PlatformUtility.GetOperatingSystem() switch
            {
                PlatformID.Win32NT => Windows,
                PlatformID.Unix => Linux,
                PlatformID.MacOSX => Mac,
                _ => Windows,
            };
    }
}