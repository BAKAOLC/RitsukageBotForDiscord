using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        /// <summary>
        ///     Bilibili live operations.
        /// </summary>
        [Group("live", "Bilibili live operations.")]
        public partial class LiveInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<LiveInteractions> Logger { get; set; }

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
        }
    }

    public partial class BilibiliInteractionButton
    {
        /// <summary>
        ///     Bilibili live operations.
        /// </summary>
        public partial class
            LiveInteractionsButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<LiveInteractionsButton> Logger { get; set; }

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