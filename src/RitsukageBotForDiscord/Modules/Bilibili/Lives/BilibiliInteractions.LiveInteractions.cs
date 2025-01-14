using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
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
            ///     Bilibili kernel provider.
            /// </summary>
            public required BiliKernelProviderService BiliKernelProvider { get; set; }

            /// <summary>
            ///     Player service.
            /// </summary>
            public IPlayerService PlayerService => BiliKernelProvider.GetRequiredService<IPlayerService>();
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