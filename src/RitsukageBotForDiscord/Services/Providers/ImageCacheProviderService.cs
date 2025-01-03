using CacheTower;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Image cache provider service
    /// </summary>
    public class ImageCacheProviderService
    {
        /// <summary>
        ///     Tag cache key
        /// </summary>
        public const string TagCacheKey = "image_cache";

        private readonly ICacheStack _cacheProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ImageCacheProviderService> _logger;

        /// <summary>
        ///     Image cache provider service
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="cacheProvider"></param>
        /// <param name="httpClientFactory"></param>
        public ImageCacheProviderService(ILogger<ImageCacheProviderService> logger, ICacheStack cacheProvider, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _cacheProvider = cacheProvider;
            _httpClientFactory = httpClientFactory;
        }

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
            var imageCache = await _cacheProvider.GetAsync<byte[]>(cacheKey);
            if (imageCache?.Value is { Length: > 0 })
            {
                _logger.LogDebug("Found image cache for {url}", url);
                using var memoryStream = new MemoryStream(imageCache.Value);
                return await Image.LoadAsync<Rgba32>(memoryStream);
            }

            _logger.LogDebug("Downloading image from {url}", url);
            var httpClient = _httpClientFactory.CreateClient();
            await using var stream = await httpClient.GetStreamAsync(url);
            using var cacheStream = new MemoryStream();
            await stream.CopyToAsync(cacheStream);
            await _cacheProvider.SetAsync(cacheKey, cacheStream.ToArray(), cacheTime == TimeSpan.Zero ? TimeSpan.FromDays(1) : cacheTime);
            _logger.LogDebug("Saved image cache for {url}", url);
            cacheStream.Seek(0, SeekOrigin.Begin);
            return await Image.LoadAsync<Rgba32>(cacheStream);
        }
    }
}