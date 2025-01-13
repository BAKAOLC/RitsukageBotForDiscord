using CacheTower;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Image cache provider service
    /// </summary>
    /// <remarks>
    ///     Image cache provider service
    /// </remarks>
    /// <param name="logger"></param>
    /// <param name="cacheProvider"></param>
    /// <param name="httpClientFactory"></param>
    public class ImageCacheProviderService(ILogger<ImageCacheProviderService> logger, ICacheStack cacheProvider, IHttpClientFactory httpClientFactory)
    {
        /// <summary>
        ///     Tag cache key
        /// </summary>
        public const string TagCacheKey = "image_cache";

        private readonly ILogger<ImageCacheProviderService> _logger = logger;

        /// <summary>
        ///     Get image
        /// </summary>
        /// <param name="url"></param>
        /// <param name="tag"></param>
        /// <param name="cacheTime"></param>
        /// <returns></returns>
        public async Task<Image<Rgba32>> GetImageAsync(string url, string tag = "default", TimeSpan cacheTime = default)
        {
            var cacheKey = $"{TagCacheKey}:{tag}:{url}";
            var imageCache = await cacheProvider.GetAsync<byte[]>(cacheKey).ConfigureAwait(false);
            if (imageCache?.Value is { Length: > 0 })
            {
                _logger.LogDebug("Found image cache for {url}", url);
                using var memoryStream = new MemoryStream(imageCache.Value);
                return await Image.LoadAsync<Rgba32>(memoryStream);
            }

            _logger.LogDebug("Downloading image from {url}", url);
            var httpClient = httpClientFactory.CreateClient();
            await using var stream = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
            using var cacheStream = new MemoryStream();
            await stream.CopyToAsync(cacheStream).ConfigureAwait(false);
            await cacheProvider.SetAsync(cacheKey, cacheStream.ToArray(), cacheTime == TimeSpan.Zero ? TimeSpan.FromDays(1) : cacheTime).ConfigureAwait(false);
            _logger.LogDebug("Saved image cache for {url}", url);
            cacheStream.Seek(0, SeekOrigin.Begin);
            return await Image.LoadAsync<Rgba32>(cacheStream);
        }
    }
}