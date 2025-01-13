using Microsoft.Extensions.DependencyInjection;
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
        private readonly Lazy<Kernel> _kernelInstance = new(BuildKernel);

        /// <summary>
        ///     Kernel.
        /// </summary>
        public Kernel Kernel => _kernelInstance.Value;

        private static Kernel BuildKernel()
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddNativeTokenResolver();
//          kernelBuilder.AddNativeQRCodeResolver();
            kernelBuilder.Services.AddSingleton<IQRCodeResolver, EmptyQrCodeResolver>();
            kernelBuilder.AddNativeCookiesResolver();
            kernelBuilder.AddHttpClient();
            kernelBuilder.AddBasicAuthenticator();
//          kernelBuilder.AddTVAuthentication();
            kernelBuilder.Services.AddSingleton<IAuthenticationService, TvAuthenticationService>();
            kernelBuilder.AddMyProfileService();
            kernelBuilder.AddRelationshipService();
            kernelBuilder.AddViewLaterService();
            kernelBuilder.AddViewHistoryService();
            kernelBuilder.AddVideoDiscoveryService();
            kernelBuilder.AddLiveDiscoveryService();
            kernelBuilder.AddEntertainmentDiscoveryService();
            kernelBuilder.AddArticleDiscoveryService();
            kernelBuilder.AddMomentDiscoveryService();
            kernelBuilder.AddMomentOperationService();
            kernelBuilder.AddMessageService();
            kernelBuilder.AddFavoriteService();
            kernelBuilder.AddSearchService();
            return kernelBuilder.Build();
        }
    }
}