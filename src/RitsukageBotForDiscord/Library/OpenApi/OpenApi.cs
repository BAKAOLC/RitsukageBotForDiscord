using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        ///     Service provider
        /// </summary>
        public IServiceProvider? ServiceProvider { get; private set; }

        /// <summary>
        ///     Cache provider
        /// </summary>
        public IFusionCache? CacheProvider => ServiceProvider?.GetService<IFusionCache>();

        /// <summary>
        ///     Configuration
        /// </summary>
        public IConfiguration? Configuration => ServiceProvider?.GetService<IConfiguration>();

        /// <summary>
        ///     Logger
        /// </summary>
        public ILogger<OpenApi>? Logger => ServiceProvider?.GetService<ILogger<OpenApi>>();

        /// <summary>
        ///     Instance of OpenApi
        /// </summary>
        public static OpenApi Instance => OpenApiInstance.Value;

        /// <summary>
        ///     Set service provider
        /// </summary>
        /// <param name="serviceProvider"></param>
        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}