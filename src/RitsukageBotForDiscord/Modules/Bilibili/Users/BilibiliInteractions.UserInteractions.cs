using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        /// <summary>
        ///     Bilibili user operations.
        /// </summary>
        [Group("user", "Bilibili user operations.")]
        public partial class UserInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<UserInteractions> Logger { get; set; }

            /// <summary>
            ///     Bilibili kernel provider.
            /// </summary>
            public required BiliKernelProviderService BiliKernelProvider { get; set; }

            /// <summary>
            ///     User service.
            /// </summary>
            public IUserService UserService => BiliKernelProvider.GetRequiredService<IUserService>();
        }
    }

    public partial class BilibiliInteractionButton
    {
        /// <summary>
        ///     Bilibili live operations.
        /// </summary>
        public partial class
            UserInteractionsButton : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
        {
            /// <summary>
            ///     Logger.
            /// </summary>
            public required ILogger<UserInteractionsButton> Logger { get; set; }

            /// <summary>
            ///     Bilibili kernel provider.
            /// </summary>
            public required BiliKernelProviderService BiliKernelProvider { get; set; }

            /// <summary>
            ///     User service.
            /// </summary>
            public IUserService UserService => BiliKernelProvider.GetRequiredService<IUserService>();
        }
    }
}