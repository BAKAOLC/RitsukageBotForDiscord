using System.ComponentModel.DataAnnotations;

namespace RitsukageBot.Options
{
    internal class CacheOption
    {
        internal double CleanUpFrequency { get; init; } = TimeSpan.FromMinutes(30).TotalMilliseconds;

        internal CacheLayerOption[] CacheProvider { get; init; } = [];

        internal class CacheLayerOption
        {
            [Required] internal string Type { get; init; } = null!;

            internal string Path { get; init; } = string.Empty;

            internal double SaveInterval { get; init; } = TimeSpan.FromMinutes(5).TotalMilliseconds;
        }
    }
}