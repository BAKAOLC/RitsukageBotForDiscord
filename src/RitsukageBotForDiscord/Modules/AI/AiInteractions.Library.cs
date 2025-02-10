using Discord;
using RitsukageBot.Library.Data;

namespace RitsukageBot.Modules.AI
{
    // ReSharper disable once MismatchedFileName
    public partial class AiInteractions
    {
        private async Task<bool> CheckEnabled()
        {
            if (ChatClientProvider.IsEnabled())
                return true;

            var embed = new EmbedBuilder();
            embed.WithTitle("Error");
            embed.WithDescription("AI chat is disabled.");
            embed.WithColor(Color.Red);
            await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
            return false;
        }

        private async Task<DiscordChannelConfiguration> GetConfigAsync(ulong channelId)
        {
            var (_, config) = await DatabaseProviderService.GetOrCreateAsync<DiscordChannelConfiguration>(channelId)
                .ConfigureAwait(false);
            return config;
        }

        private async Task<UserGoodInfo[]> GetUserGoodInfoAsync(ChatUserInformation[] users)
        {
            var list = new List<UserGoodInfo>();
            var channel = await Context.Client.GetChannelAsync(Context.Channel.Id).ConfigureAwait(false);
            foreach (var user in users)
            {
                var userInfo = await channel.GetUserAsync(user.Id).ConfigureAwait(false) ??
                               await Context.Client.Rest.GetUserAsync(user.Id).ConfigureAwait(false);
                if (userInfo is null)
                {
                    list.Add(new(user.Id, "Unknown", user.Good));
                    continue;
                }

                list.Add(new(user.Id, userInfo.GlobalName ?? userInfo.Username, user.Good));
            }

            return [..list];
        }

        private record UserGoodInfo(ulong Id, string Name, int Good);
    }
}