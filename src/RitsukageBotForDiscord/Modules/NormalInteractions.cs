using Discord.Interactions;
using Discord.WebSocket;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Normal Interactions
    /// </summary>
    public class NormalInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Database Provider Service
        /// </summary>
        public required DatabaseProviderService DatabaseProvider { get; set; }

        /// <summary>
        ///     Give bot a candy
        /// </summary>
        [SlashCommand("candy", "Give bot a candy")]
        public async Task CandyAsync()
        {
            var (userCandy, totalCandy) = await GiveBotACandy(Context.User.Id).ConfigureAwait(false);
            await RespondAsync($":candy: You've given me {userCandy} candy! | I've received {totalCandy} candy total!")
                .ConfigureAwait(false);
        }

        private async Task<(long, long)> GiveBotACandy(ulong userId)
        {
            var (_, botRecord) = await DatabaseProvider.GetOrCreateAsync<BotRecordInformation>(0).ConfigureAwait(false);
            var (_, userConfig) = await DatabaseProvider.GetOrCreateAsync<DiscordUserConfiguration>(userId)
                .ConfigureAwait(false);

            botRecord.TotalCandyCount++;
            userConfig.CandyCount++;
            await DatabaseProvider.InsertOrUpdateAsync(botRecord).ConfigureAwait(false);
            await DatabaseProvider.InsertOrUpdateAsync(userConfig).ConfigureAwait(false);

            return (userConfig.CandyCount, botRecord.TotalCandyCount);
        }
    }
}