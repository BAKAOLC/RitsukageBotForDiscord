using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Data;
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

        /// <summary>
        ///     Database provider service.
        /// </summary>
        public required DatabaseProviderService DatabaseProviderService { get; set; }

        /// <summary>
        ///     Automatically resolve Bilibili links.
        /// </summary>
        /// <param name="active"></param>
        [RequireUserPermission(GuildPermission.Administrator
                               | GuildPermission.ManageGuild
                               | GuildPermission.ManageChannels)]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("auto-resolve", "Automatically resolve Bilibili links.")]
        public async Task AutoResolveBilibiliLinkAsync(bool active = true)
        {
            await DeferAsync().ConfigureAwait(false);
            var channelId = Context.Channel.Id;
            var config = await GetConfigAsync(channelId).ConfigureAwait(false);
            if (active == config.AutomaticallyResolveBilibiliLinks)
            {
                await FollowupAsync("The setting is already set to the specified value.").ConfigureAwait(false);
                return;
            }

            config.AutomaticallyResolveBilibiliLinks = active;
            await DatabaseProviderService.InsertOrUpdateAsync(config).ConfigureAwait(false);
            await FollowupAsync($"Automatically resolve Bilibili links has been {(active ? "enabled" : "disabled")}.")
                .ConfigureAwait(false);
        }

        private async Task<DiscordChannelConfiguration> GetConfigAsync(ulong channelId)
        {
            var (_, config) = await DatabaseProviderService.GetOrCreateAsync<DiscordChannelConfiguration>(channelId)
                .ConfigureAwait(false);
            return config;
        }
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