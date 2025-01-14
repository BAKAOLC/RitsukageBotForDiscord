using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili
{
    /// <summary>
    ///     Bilibili interactions.
    /// </summary>
    [Group("bilibili", "Bilibili interactions")]
    public partial class BilibiliInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Tag custom ID
        /// </summary>
        public const string TagCustomId = "bilibili_interaction";

        /// <summary>
        ///     Logger.
        /// </summary>
        public required ILogger<BilibiliInteractions> Logger { get; set; }

        /// <summary>
        ///     Bilibili kernel provider.
        /// </summary>
        public required BiliKernelProviderService BiliKernelProvider { get; set; }
    }

    /// <summary>
    ///     Bilibili interaction button.
    /// </summary>
    public partial class
        BilibiliInteractionButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        /// <summary>
        ///     Logger.
        /// </summary>
        public required ILogger<BilibiliInteractionButton> Logger { get; set; }

        /// <summary>
        ///     Bilibili kernel provider.
        /// </summary>
        public required BiliKernelProviderService BiliKernelProvider { get; set; }
    }
}