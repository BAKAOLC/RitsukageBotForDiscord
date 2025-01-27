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
                options.FailSafeMaxDuration = options.Duration * 3;
            }).ConfigureAwait(false);

            var cacheStream = new MemoryStream(imageBytes);
            return await Image.LoadAsync<Rgba32>(cacheStream).ConfigureAwait(false);
        }

        /// <summary>
        ///     Cache image
        /// </summary>
        /// <param name="image"></param>
        /// <param name="tag"></param>
        /// <param name="cacheTime"></param>
        /// <returns></returns>
        public async Task<string> CacheImageAsync(Image<Rgba32> image, string tag = "default",
            TimeSpan cacheTime = default)
        {
            var guid = Guid.NewGuid();
            var cacheKey = $"{CacheKey}:{tag}:{guid}";
            using var cacheStream = new MemoryStream();
            if (image.Frames.Count > 1)
                await image.SaveAsGifAsync(cacheStream).ConfigureAwait(false);
            else
                await image.SaveAsPngAsync(cacheStream).ConfigureAwait(false);
            await cacheProvider.SetAsync(cacheKey, cacheStream.ToArray(), options =>
            {
                options.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                options.FactorySoftTimeout = TimeSpan.FromSeconds(20);
                options.Duration = cacheTime == TimeSpan.Zero ? TimeSpan.FromDays(1) : cacheTime;
                options.FailSafeMaxDuration = options.Duration * 3;
            }).ConfigureAwait(false);

            return guid.ToString("D").ToUpperInvariant();
        }

        /// <summary>
        ///     Get image from guid
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public async Task<Image<Rgba32>?> GetImageFromGuid(string guid, string tag = "default")
        {
            var id = Guid.Parse(guid);
            var cacheKey = $"{CacheKey}:{tag}:{id}";
            var imageBytes = await cacheProvider.GetOrDefaultAsync<byte[]>(cacheKey).ConfigureAwait(false);
            if (imageBytes == null || imageBytes.Length == 0)
                return null;
            using var cacheStream = new MemoryStream(imageBytes);
            return await Image.LoadAsync<Rgba32>(cacheStream).ConfigureAwait(false);
        }
    }
}