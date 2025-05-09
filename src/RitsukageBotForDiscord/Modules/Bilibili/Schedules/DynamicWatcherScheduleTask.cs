using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili.Schedules
{
    internal class DynamicWatcherScheduleTask(IServiceProvider serviceProvider) : PeriodicScheduleTask(serviceProvider)
    {
        private readonly BiliKernelProviderService _biliKernelProvider =
            serviceProvider.GetRequiredService<BiliKernelProviderService>();

        private readonly DatabaseProviderService _databaseProviderService =
            serviceProvider.GetRequiredService<DatabaseProviderService>();

        private readonly DiscordSocketClient _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();

        private readonly Dictionary<string, IReadOnlyList<MomentInformation>> _follows = [];

        private readonly ILogger<DynamicWatcherScheduleTask> _logger =
            serviceProvider.GetRequiredService<ILogger<DynamicWatcherScheduleTask>>();

        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(5),
        };

        private IMomentDiscoveryService MomentDiscoveryService =>
            _biliKernelProvider.GetRequiredService<IMomentDiscoveryService>();

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("DynamicWatcherScheduleTask is triggered");
            var table = _databaseProviderService.Table<BilibiliWatcherConfiguration>();
            var configs = await table.Where(x => x.Type == WatcherType.Dynamic).ToArrayAsync().ConfigureAwait(false);
            List<BilibiliWatcherConfiguration> needRemoved = [];
            foreach (var config in configs)
            {
                if (ulong.TryParse(config.Target, out _)) continue;
                _logger.LogWarning("Invalid user id: {Target}", config.Target);
                needRemoved.Add(config);
            }

            configs = [.. configs.Except(needRemoved)];

            var usersRequests = GetMomentsRequests(configs);
            await UpdateMomentsAsync(usersRequests).ConfigureAwait(false);

            foreach (var config in configs)
            {
                if (!_follows.TryGetValue(config.Target, out var userMoments)) continue;
                var moments = userMoments.ToArray();
                if (moments.Length == 0)
                {
                    _logger.LogWarning("No moments found for user: {UserId}", config.Target);
                    continue;
                }

                var lastMoment = moments.FirstOrDefault(x => x.Id == config.LastInformation);
                var index = Array.IndexOf(moments, lastMoment);
                if (index == -1)
                {
                    _logger.LogWarning("Moment {MomentId} is not found", config.LastInformation);
                    lastMoment = null;
                }

                var channel = await _discordClient.GetChannelAsync(config.ChannelId).ConfigureAwait(false);
                if (channel is not IMessageChannel messageChannel)
                {
                    _logger.LogWarning("Channel {ChannelId} is not found", config.ChannelId);
                    continue;
                }

                if (lastMoment == null)
                {
                    config.LastInformation = moments[0].Id;
                    await SendMomentAsync(messageChannel, moments[0]).ConfigureAwait(false);
                    continue;
                }

                config.LastInformation = moments.First().Id;
                for (var i = index - 1; i >= 0; i--)
                    await SendMomentAsync(messageChannel, moments[i]).ConfigureAwait(false);
            }

            _logger.LogDebug("Updating database");
            await _databaseProviderService.UpdateAllAsync(configs).ConfigureAwait(false);
            foreach (var config in needRemoved)
                await _databaseProviderService.DeleteAsync(config).ConfigureAwait(false);
            _logger.LogDebug("Database updated");

            _logger.LogDebug("DynamicWatcherScheduleTask is completed");
        }

        private async Task SendMomentAsync(IMessageChannel channel, MomentInformation moment)
        {
            try
            {
                var embeds = InformationEmbedBuilder.BuildMomentInfo(moment);
                embeds[^1].WithBilibiliLogoIconFooter();
                var text = $"User {moment.User?.Name} has a new moment!";
                var component = new ComponentBuilder().WithButton("Watch moment",
                    url: $"https://www.bilibili.com/opus/{moment.Id}", style: ButtonStyle.Link);

                var embedBatches = embeds.Select((embed, index) => new { embed, index })
                    .GroupBy(x => x.index / 10)
                    .Select(g => g.Select(x => x.embed.Build()).ToArray())
                    .ToArray();

                switch (embedBatches.Length)
                {
                    case 0:
                    {
                        await channel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                            BilibiliIconData.LogoIconFileName,
                            text, components: component.Build()).ConfigureAwait(false);
                        break;
                    }
                    default:
                    {
                        await channel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                            BilibiliIconData.LogoIconFileName,
                            text,
                            embeds: embedBatches[0], components: component.Build()).ConfigureAwait(false);
                        for (var i = 1; i < embedBatches.Length; i++)
                            await channel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                                BilibiliIconData.LogoIconFileName,
                                embeds: embedBatches[i]).ConfigureAwait(false);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send moment {MomentId}", moment.Id);
            }
        }

        private async Task UpdateMomentsAsync(params IEnumerable<UserMomentsRequest> requests)
        {
            _logger.LogDebug("Updating follows moments");
            _follows.Clear();
            foreach (var request in requests)
            {
                var userProfile = new UserProfile(request.UserId);
                var (moments, offset, hasMore) = await MomentDiscoveryService
                    .GetUserMomentsAsync(userProfile).ConfigureAwait(false);
                if (moments.Count == 0) continue;
                while (IsSmallerOffset(request.Offset, offset) && hasMore)
                {
                    (var addMoments, offset, hasMore) = await MomentDiscoveryService
                        .GetUserMomentsAsync(userProfile, offset).ConfigureAwait(false);
                    if (addMoments.Count == 0) break;
                    moments = [.. moments, .. addMoments];
                }

                _follows[request.UserId] = moments;
            }

            _logger.LogDebug("Moments updated");
        }

        private static IEnumerable<UserMomentsRequest> GetMomentsRequests(
            IEnumerable<BilibiliWatcherConfiguration> configs)
        {
            var totalRequests = configs.Select(x => new UserMomentsRequest
            {
                UserId = x.Target,
                Offset = x.LastInformation,
            });

            Dictionary<string, UserMomentsRequest> results = [];
            foreach (var request in totalRequests)
            {
                if (results.TryAdd(request.UserId, request)) continue;
                if (IsSmallerOffset(request.Offset, results[request.UserId].Offset)) results[request.UserId] = request;
            }

            foreach (var x in results.ToArray()) yield return x.Value;
        }

        private static bool IsSmallerOffset(string offset, string target)
        {
            if (ulong.TryParse(offset, out var offsetValue) && ulong.TryParse(target, out var targetValue))
                return offsetValue < targetValue;
            return false;
        }

        private readonly record struct UserMomentsRequest
        {
            public string UserId { get; init; }

            public string Offset { get; init; }
        }
    }
}