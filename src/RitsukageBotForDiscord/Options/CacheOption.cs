using System.ComponentModel.DataAnnotations;

namespace RitsukageBot.Options
{
    internal class CacheOption
    {
        public double CleanUpFrequency { get; init; } = TimeSpan.FromMinutes(30).TotalMilliseconds;

        public CacheLayerOption[] CacheProvider { get; init; } = [];

        public class CacheLayerOption
        {
            [Required] public string Type { get; init; } = null!;

            public string Path { get; init; } = string.Empty;

            public double SaveInterval { get; init; } = TimeSpan.FromMinutes(5).TotalMilliseconds;
        }
    }
}