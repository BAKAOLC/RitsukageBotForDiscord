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