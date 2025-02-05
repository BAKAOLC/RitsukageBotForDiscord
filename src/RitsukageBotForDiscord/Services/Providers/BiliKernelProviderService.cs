using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Moment;
using RichasyKernel;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Abstractions.Moment;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Authorizers;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Resolvers;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Services.Moment;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Bili kernel provider service.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    public class BiliKernelProviderService(IServiceProvider serviceProvider)
    {
        private readonly Lazy<Kernel> _kernelInstance = new(() => BuildKernel(serviceProvider));

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

        private static Kernel BuildKernel(IServiceProvider serviceProvider)
        {
            var kernelBuilder = Kernel.CreateBuilder();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            kernelBuilder.Services.AddSingleton(loggerFactory);
            kernelBuilder.AddArticleDiscoveryService();
            kernelBuilder.AddArticleOperationService();
            kernelBuilder.AddBiliAuthenticator();
            kernelBuilder.AddCommentService();
            kernelBuilder.AddDanmakuService();
            kernelBuilder.AddEntertainmentDiscoveryService();
            kernelBuilder.AddFavoriteService();
            kernelBuilder.AddBiliClient();
            kernelBuilder.AddLiveDiscoveryService();
            kernelBuilder.AddMessageService();

            //kernelBuilder.AddMomentDiscoveryService();
            //kernelBuilder.AddMomentOperationService();
            kernelBuilder.AddMyProfileService();

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
            var database = serviceProvider.GetRequiredService<DatabaseProviderService>();
            var databaseAccountResolver = new DatabaseAccountResolver(database);
            kernelBuilder.Services.AddSingleton<IBiliTokenResolver>(databaseAccountResolver);
            kernelBuilder.Services.AddSingleton<IBiliCookiesResolver>(databaseAccountResolver);
            kernelBuilder.Services.AddSingleton<IMomentService, MomentService>();
            kernelBuilder.Services.AddSingleton<IMomentDiscoveryService, MomentService>();
            kernelBuilder.Services.AddSingleton<IMomentOperationService, MomentService>();
            return new(kernelBuilder.Services.BuildServiceProvider());
        }
    }
}