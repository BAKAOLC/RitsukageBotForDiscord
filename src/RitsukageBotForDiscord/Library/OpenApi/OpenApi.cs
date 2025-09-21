using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace RitsukageBot.Library.OpenApi
{
    /// <summary>
    ///     OpenApi
    /// </summary>
    public class OpenApi
    {
        private static readonly Lazy<OpenApi> OpenApiInstance = new(() => new());

        private OpenApi()
        {
        }

        /// <summary>
        ///     Cache provider
        /// </summary>
        public IFusionCache? CacheProvider { get; private set; }

        /// <summary>
        ///     Logger
        /// </summary>
        public ILogger<OpenApi>? Logger { get; private set; }

        /// <summary>
        ///     Instance of OpenApi
        /// </summary>
        public static OpenApi Instance => OpenApiInstance.Value;

        /// <summary>
        ///     Set cache provider
        /// </summary>
        /// <param name="cacheProvider"></param>
        public void SetCacheProvider(IFusionCache cacheProvider)
        {
            CacheProvider = cacheProvider;
        }

        /// <summary>
        ///     Set logger
        /// </summary>
        /// <param name="logger"></param>
        public void SetLogger(ILogger<OpenApi> logger)
        {
            Logger = logger;
        }
    }
}