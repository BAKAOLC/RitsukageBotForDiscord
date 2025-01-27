using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili.Schedules
{
    internal class LiveWatcherScheduleTask(IServiceProvider serviceProvider) : PeriodicScheduleTask(serviceProvider)
    {
        private readonly BiliKernelProviderService _biliKernelProvider =
            serviceProvider.GetRequiredService<BiliKernelProviderService>();

        private readonly DatabaseProviderService _databaseProviderService =
            serviceProvider.GetRequiredService<DatabaseProviderService>();

        private readonly DiscordSocketClient _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();

        private readonly ILogger<LiveWatcherScheduleTask> _logger =
            serviceProvider.GetRequiredService<ILogger<LiveWatcherScheduleTask>>();

        private readonly Dictionary<string, LiveInformation> _records = [];

        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(1),
        };

        private IPlayerService PlayerService => _biliKernelProvider.GetRequiredService<IPlayerService>();

        private ILiveDiscoveryService LiveDiscoveryService =>
            _biliKernelProvider.GetRequiredService<ILiveDiscoveryService>();

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("LiveWatcherScheduleTask is triggered");
            var table = _databaseProviderService.Table<BilibiliWatcherConfiguration>();
            var configs = await table.Where(x => x.Type == WatcherType.Live).ToArrayAsync().ConfigureAwait(false);
            List<BilibiliWatcherConfiguration> needRemoved = [];
            await UpdateFollowsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var config in configs)
            {
                if (!ulong.TryParse(config.Target, out var roomId))
                {
                    _logger.LogWarning("Invalid room id: {Target}", config.Target);
                    needRemoved.Add(config);
                    continue;
                }

                var roomIdStr = roomId.ToString();
                var liveInfo = await GetRoomInformationAsync(roomIdStr, cancellationToken).ConfigureAwait(false);
                var isLiving = liveInfo.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
                var isLivingStr = isLiving.ToString();
                if (config.LastInformation == isLivingStr) continue;
                config.LastInformation = isLivingStr;

                var channel = await _discordClient.GetChannelAsync(config.ChannelId).ConfigureAwait(false);
                if (channel is not IMessageChannel messageChannel)
                {
                    _logger.LogWarning("Channel {ChannelId} is not found", config.ChannelId);
                    continue;
                }

                var embed = InformationEmbedBuilder.BuildLiveInfo(liveInfo);
                embed.WithBilibiliLogoIconFooter();
                embed.Timestamp = null; // Remove timestamp
                var text = $"{liveInfo.User.Name}'s live room is now {(isLiving ? "living!" : "offline...")}";
                if (!isLiving)
                {
                    await messageChannel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        text, embed: embed.Build()).ConfigureAwait(false);
                    continue;
                }

                var components = new ComponentBuilder().WithButton("Watch Stream", style: ButtonStyle.Link,
                    url: $"https://live.bilibili.com/{roomIdStr}");
                await messageChannel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                    BilibiliIconData.LogoIconFileName,
                    text, embed: embed.Build(), components: components.Build()).ConfigureAwait(false);
            }

            _logger.LogDebug("Updating database");
            await _databaseProviderService.UpdateAllAsync(configs).ConfigureAwait(false);
            foreach (var config in needRemoved)
                await _databaseProviderService.DeleteAsync(config).ConfigureAwait(false);
            _logger.LogDebug("Database updated");

            _logger.LogDebug("LiveWatcherScheduleTask is completed");
        }

        private async Task UpdateFollowsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating follows list");
            List<LiveInformation> lives = [];
            var (follows, _, nextPageNumber) = await LiveDiscoveryService
                .GetFeedAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            while (follows is not null && follows.Any())
            {
                lives.AddRange(follows);
                _logger.LogDebug("Page {PageNumber} is loaded", nextPageNumber);
                (follows, _, nextPageNumber) = await LiveDiscoveryService
                    .GetFeedAsync(nextPageNumber, cancellationToken).ConfigureAwait(false);
            }

            _records.Clear();
            foreach (var live in lives)
            {
                live.AddExtensionIfNotNull(LiveExtensionDataId.IsLiving, true);
                _records[live.Identifier.Id] = live;
            }

            _logger.LogDebug("Follows list updated");
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