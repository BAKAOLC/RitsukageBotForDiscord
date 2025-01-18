using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Abstractions.Moment;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        /// <summary>
        ///     Bilibili dynamic operations.
        /// </summary>
        [Group("dynamic", "Bilibili dynamic operations.")]
        public partial class DynamicInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<DynamicInteractions> Logger { get; set; }

            /// <summary>
            ///     Database provider service.
            /// </summary>
            public required DatabaseProviderService DatabaseProviderService { get; set; }

            /// <summary>
            ///     Bilibili kernel provider.
            /// </summary>
            public required BiliKernelProviderService BiliKernelProvider { get; set; }

            /// <summary>
            ///     Media service.
            /// </summary>
            public IUserService UserService => BiliKernelProvider.GetRequiredService<IUserService>();

            /// <summary>
            ///     Player service.
            /// </summary>
            public IPlayerService PlayerService => BiliKernelProvider.GetRequiredService<IPlayerService>();

            /// <summary>
            ///     Relationship service.
            /// </summary>
            public IRelationshipService RelationshipService => BiliKernelProvider.GetRequiredService<IRelationshipService>();
            
            /// <summary>
            ///     Moment service.
            /// </summary>
            public IMomentService MomentService => BiliKernelProvider.GetRequiredService<IMomentService>();
        }
    }

    public partial class BilibiliInteractionButton
    {
        /// <summary>
        ///     Bilibili live operations.
        /// </summary>
        public partial class
            DynamicInteractionsButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<DynamicInteractionsButton> Logger { get; set; }

            /// <summary>
            ///     Bilibili kernel provider.
            /// </summary>
            public required BiliKernelProviderService BiliKernelProvider { get; set; }

            /// <summary>
            ///     Player service.
            /// </summary>
            public IPlayerService PlayerService => BiliKernelProvider.GetRequiredService<IPlayerService>();
        }
    }
}