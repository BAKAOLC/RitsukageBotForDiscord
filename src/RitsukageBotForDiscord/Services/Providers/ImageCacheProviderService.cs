using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZiggyCreatures.Caching.Fusion;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Image cache provider service
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="cacheProvider"></param>
    /// <param name="httpClientFactory"></param>
    public class ImageCacheProviderService(
        ILogger<ImageCacheProviderService> logger,
        IFusionCache cacheProvider,
        IHttpClientFactory httpClientFactory)
    {
        /// <summary>
        ///     Cache key
        /// </summary>
        public const string CacheKey = "image_cache";

        /// <summary>
        ///     Get image
        /// </summary>
        /// <param name="url"></param>
        /// <param name="tag"></param>
        /// <param name="cacheTime"></param>
        /// <returns></returns>
        public async Task<Image<Rgba32>> GetImageAsync(string url, string tag = "default", TimeSpan cacheTime = default)
        {
            var cacheKey = $"{CacheKey}:{tag}:{url}";
            var imageBytes = await cacheProvider.GetOrSetAsync<byte[]>(cacheKey, async cancellationToken =>
            {
                logger.LogDebug("Downloading image from {Url}", url);
                var httpClient = httpClientFactory.CreateClient();
                await using var stream = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
                using var cacheStream = new MemoryStream();
                await stream.CopyToAsync(cacheStream, cancellationToken).ConfigureAwait(false);
                return cacheStream.ToArray();
            }, options =>
            {
                options.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                options.FactorySoftTimeout = TimeSpan.FromSeconds(20);
                options.Duration = cacheTime == TimeSpan.Zero ? TimeSpan.FromDays(1) : cacheTime;
            }).ConfigureAwait(false);

            var cacheStream = new MemoryStream(imageBytes);
            return await Image.LoadAsync<Rgba32>(cacheStream).ConfigureAwait(false);
        }
    }
}