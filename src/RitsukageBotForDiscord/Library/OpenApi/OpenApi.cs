using ZiggyCreatures.Caching.Fusion;

namespace RitsukageBot.Library.OpenApi
{
    /// <summary>
    ///     OpenApi
    /// </summary>
    public partial class OpenApi
    {
        private static IFusionCache? _cacheProvider;

        private OpenApi()
        {
        }

        /// <summary>
        ///     Get cache provider
        /// </summary>
        /// <param name="cacheProvider"></param>
        public static void SetCacheProvider(IFusionCache cacheProvider)
        {
            _cacheProvider = cacheProvider;
        }
    }
}