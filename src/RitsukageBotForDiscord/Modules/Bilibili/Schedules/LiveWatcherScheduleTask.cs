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
        private readonly List<LiveInformation> _follows = [];

        /// <inheritdoc />
        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(10),
        };

        private ILogger<LiveWatcherScheduleTask> Logger => GetRequiredService<ILogger<LiveWatcherScheduleTask>>();

        private DiscordSocketClient DiscordClient => GetRequiredService<DiscordSocketClient>();

        private DatabaseProviderService DatabaseProviderService => GetRequiredService<DatabaseProviderService>();

        private BiliKernelProviderService BiliKernelProvider => GetRequiredService<BiliKernelProviderService>();

        private ILiveDiscoveryService LiveDiscoveryService => BiliKernelProvider.GetRequiredService<ILiveDiscoveryService>();

        /// <inheritdoc />
        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("LiveWatcherScheduleTask is triggered.");
            var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
            var configs = await table.Where(x => x.Type == WatcherType.Live).ToArrayAsync().ConfigureAwait(false);
            List<BilibiliWatcherConfiguration> needRemoved = [];
            await UpdateFollowsAsync(cancellationToken);
            foreach (var config in configs)
            {
                if (!ulong.TryParse(config.Target, out var roomId))
                {
                    Logger.LogWarning("Invalid room id: {Target}", config.Target);
                    needRemoved.Add(config);
                    continue;
                }

                var roomIdStr = roomId.ToString();
                var liveInfo = _follows.FirstOrDefault(x => x.Identifier.Id == roomIdStr);
                if (liveInfo is null)
                {
                    Logger.LogWarning("Room {RoomId} is not found.", roomId);
                    continue;
                }

                var isLiving = liveInfo.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
                var isLivingStr = isLiving.ToString();
                if (config.LastInformation == isLivingStr) continue;
                config.LastInformation = isLivingStr;

                var embed = InformationEmbedBuilder.BuildLiveInfo(liveInfo);
                var channel = await DiscordClient.GetChannelAsync(config.ChannelId);
                if (channel is not IMessageChannel messageChannel)
                {
                    Logger.LogWarning("Channel {ChannelId} is not found.", config.ChannelId);
                    continue;
                }

                var text = $"Room {roomId} is now {(liveInfo.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving) ? "live!" : "offline...")}";
                await messageChannel.SendMessageAsync(text, embed: embed.Build()).ConfigureAwait(false);
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
            _follows.Clear();
            var (follows, _, nextPageNumber) = await LiveDiscoveryService.GetFeedAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            while (follows is not null && follows.Any())
            {
                _follows.AddRange(follows);
                Logger.LogInformation("Page {PageNumber} is loaded.", nextPageNumber);
                (follows, _, nextPageNumber) = await LiveDiscoveryService.GetFeedAsync(nextPageNumber, cancellationToken).ConfigureAwait(false);
            }

            Logger.LogInformation("Follows list updated.");
        }
    }
}