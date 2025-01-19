using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili.Schedules
{
    /// <inheritdoc />
    public class LiveWatcherScheduleTask(IServiceProvider serviceProvider) : PeriodicScheduleTask(serviceProvider)
    {
        private readonly Dictionary<string, LiveInformation> _records = [];

        /// <inheritdoc />
        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(1),
        };

        private ILogger<LiveWatcherScheduleTask> Logger => GetRequiredService<ILogger<LiveWatcherScheduleTask>>();

        private DiscordSocketClient DiscordClient => GetRequiredService<DiscordSocketClient>();

        private DatabaseProviderService DatabaseProviderService => GetRequiredService<DatabaseProviderService>();

        private BiliKernelProviderService BiliKernelProvider => GetRequiredService<BiliKernelProviderService>();

        private IPlayerService PlayerService => BiliKernelProvider.GetRequiredService<IPlayerService>();

        private ILiveDiscoveryService LiveDiscoveryService =>
            BiliKernelProvider.GetRequiredService<ILiveDiscoveryService>();

        /// <inheritdoc />
        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("LiveWatcherScheduleTask is triggered.");
            var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
            var configs = await table.Where(x => x.Type == WatcherType.Live).ToArrayAsync().ConfigureAwait(false);
            List<BilibiliWatcherConfiguration> needRemoved = [];
            await UpdateFollowsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var config in configs)
            {
                if (!ulong.TryParse(config.Target, out var roomId))
                {
                    Logger.LogWarning("Invalid room id: {Target}", config.Target);
                    needRemoved.Add(config);
                    continue;
                }

                var roomIdStr = roomId.ToString();
                var liveInfo = await GetRoomInformationAsync(roomIdStr, cancellationToken).ConfigureAwait(false);
                var isLiving = liveInfo.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
                var isLivingStr = isLiving.ToString();
                if (config.LastInformation == isLivingStr) continue;
                config.LastInformation = isLivingStr;

                var embed = InformationEmbedBuilder.BuildLiveInfo(liveInfo);
                var channel = await DiscordClient.GetChannelAsync(config.ChannelId).ConfigureAwait(false);
                if (channel is not IMessageChannel messageChannel)
                {
                    Logger.LogWarning("Channel {ChannelId} is not found.", config.ChannelId);
                    continue;
                }

                var text = $"{liveInfo.User.Name}'s live room is now {(isLiving ? "living!" : "offline...")}";
                if (!isLiving)
                {
                    await messageChannel.SendMessageAsync(text, embed: embed.Build()).ConfigureAwait(false);
                    continue;
                }

                var components = new ComponentBuilder().WithButton("Watch Stream", style: ButtonStyle.Link,
                    url: $"https://live.bilibili.com/{roomIdStr}");
                await messageChannel.SendMessageAsync(text, embed: embed.Build(), components: components.Build())
                    .ConfigureAwait(false);
            }

            Logger.LogInformation("Updating database.");
            await DatabaseProviderService.UpdateAllAsync(configs).ConfigureAwait(false);
            foreach (var config in needRemoved) await DatabaseProviderService.DeleteAsync(config).ConfigureAwait(false);
            Logger.LogInformation("Database updated.");

            Logger.LogInformation("LiveWatcherScheduleTask is completed.");
        }

        private async Task UpdateFollowsAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Updating follows list.");
            List<LiveInformation> lives = [];
            var (follows, _, nextPageNumber) = await LiveDiscoveryService
                .GetFeedAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            while (follows is not null && follows.Any())
            {
                lives.AddRange(follows);
                Logger.LogInformation("Page {PageNumber} is loaded.", nextPageNumber);
                (follows, _, nextPageNumber) = await LiveDiscoveryService
                    .GetFeedAsync(nextPageNumber, cancellationToken).ConfigureAwait(false);
            }

            _records.Clear();
            foreach (var live in lives)
            {
                live.AddExtensionIfNotNull(LiveExtensionDataId.IsLiving, true);
                _records[live.Identifier.Id] = live;
            }

            Logger.LogInformation("Follows list updated.");
        }

        private async Task<LiveInformation> GetRoomInformationAsync(string roomId, CancellationToken cancellationToken)
        {
            if (_records.TryGetValue(roomId, out var info)) return info;
            var mediaIdentifier = new MediaIdentifier(roomId, null, null);
            var detail = await PlayerService.GetLivePageDetailAsync(mediaIdentifier, cancellationToken)
                .ConfigureAwait(false);
            _records[detail.Information.Identifier.Id] = detail.Information;
            return detail.Information;
        }
    }
}