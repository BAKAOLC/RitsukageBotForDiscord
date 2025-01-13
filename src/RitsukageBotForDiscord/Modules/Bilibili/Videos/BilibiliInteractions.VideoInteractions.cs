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
        ///     Bilibili video operations.
        /// </summary>
        [Group("video", "Bilibili video operations.")]
        public partial class VideoInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<VideoInteractions> Logger { get; set; }

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
        ///     Bilibili video operations.
        /// </summary>
        public partial class VideoInteractionsButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<VideoInteractionsButton> Logger { get; set; }

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