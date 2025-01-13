using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Bili.Authorization;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Authorizers;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Resolvers;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Bili kernel provider service.
    /// </summary>
    public class BiliKernelProviderService
    {
        private readonly Lazy<Kernel> _kernelInstance;

        /// <summary>
        ///     Bili kernel provider service.
        /// </summary>
        /// <param name="loggerFactory"></param>
        public BiliKernelProviderService(ILoggerFactory? loggerFactory = null)
        {
            _kernelInstance = new(() => BuildKernel(loggerFactory));
        }

        /// <summary>
        ///     Kernel.
        /// </summary>
        public Kernel Kernel => _kernelInstance.Value;

        /// <summary>Gets a required service from the <see cref="Services" /> provider.</summary>
        /// <typeparam name="T">Specifies the type of the service to get.</typeparam>
        /// <param name="serviceKey">An object that specifies the key of the service to get.</param>
        /// <returns>The found service instance.</returns>
        /// <exception cref="KernelException">A service of the specified type and name could not be found.</exception>
        public T GetRequiredService<T>(object? serviceKey = null) where T : class
        {
            return Kernel.GetRequiredService<T>(serviceKey);
        }


        /// <summary>Gets all services of the specified type.</summary>
        /// <typeparam name="T">Specifies the type of the services to retrieve.</typeparam>
        /// <returns>An enumerable of all instances of the specified service that are registered.</returns>
        /// <remarks>There is no guaranteed ordering on the results.</remarks>
        public IEnumerable<T> GetAllServices<T>() where T : class
        {
            return Kernel.GetAllServices<T>();
        }

        private Kernel BuildKernel(ILoggerFactory? loggerFactory)
        {
            var kernelBuilder = Kernel.CreateBuilder();
            if (loggerFactory is not null) kernelBuilder.Services.AddSingleton(loggerFactory);

            kernelBuilder.AddArticleDiscoveryService();
            kernelBuilder.AddArticleOperationService();
            kernelBuilder.AddBasicAuthenticator();
            kernelBuilder.AddCommentService();
            kernelBuilder.AddDanmakuService();
            kernelBuilder.AddEntertainmentDiscoveryService();
            kernelBuilder.AddFavoriteService();
            kernelBuilder.AddHttpClient();
            kernelBuilder.AddLiveDiscoveryService();
            kernelBuilder.AddMessageService();
            kernelBuilder.AddMomentDiscoveryService();
            kernelBuilder.AddMomentOperationService();
            kernelBuilder.AddMyProfileService();
            kernelBuilder.AddNativeCookiesResolver();
            kernelBuilder.AddNativeTokenResolver();
            //kernelBuilder.AddNativeQRCodeResolver();
            kernelBuilder.AddPlayerService();
            kernelBuilder.AddRelationshipService();
            kernelBuilder.AddSearchService();
            kernelBuilder.AddSubtitleService();
            //kernelBuilder.AddTVAuthentication();
            kernelBuilder.AddUserService();
            kernelBuilder.AddVideoDiscoveryService();
            kernelBuilder.AddViewHistoryService();
            kernelBuilder.AddViewLaterService();
            kernelBuilder.Services.AddSingleton<IQRCodeResolver, EmptyQrCodeResolver>();
            kernelBuilder.Services.AddSingleton<IAuthenticationService, TvAuthenticationService>();
            return kernelBuilder.Build();
        }
    }
}