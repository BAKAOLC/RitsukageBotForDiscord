using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili.Schedules
{
    /// <inheritdoc />
    public class DynamicWatcherScheduleTask(IServiceProvider serviceProvider) : PeriodicScheduleTask(serviceProvider)
    {
        private readonly Dictionary<string, IReadOnlyList<MomentInformation>> _follows = [];

        /// <inheritdoc />
        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(5),
        };

        private ILogger<DynamicWatcherScheduleTask> Logger => GetRequiredService<ILogger<DynamicWatcherScheduleTask>>();

        private DiscordSocketClient DiscordClient => GetRequiredService<DiscordSocketClient>();

        private DatabaseProviderService DatabaseProviderService => GetRequiredService<DatabaseProviderService>();

        private BiliKernelProviderService BiliKernelProvider => GetRequiredService<BiliKernelProviderService>();

        private IMomentDiscoveryService MomentDiscoveryService =>
            BiliKernelProvider.GetRequiredService<IMomentDiscoveryService>();

        /// <inheritdoc />
        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("DynamicWatcherScheduleTask is triggered.");
            var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
            var configs = await table.Where(x => x.Type == WatcherType.Dynamic).ToArrayAsync().ConfigureAwait(false);
            List<BilibiliWatcherConfiguration> needRemoved = [];
            foreach (var config in configs)
            {
                if (ulong.TryParse(config.Target, out _)) continue;
                Logger.LogWarning("Invalid user id: {Target}", config.Target);
                needRemoved.Add(config);
            }

            configs = configs.Except(needRemoved).ToArray();

            var usersRequests = GetMomentsRequests(configs);
            await UpdateMomentsAsync(usersRequests).ConfigureAwait(false);

            foreach (var config in configs)
            {
                if (!_follows.TryGetValue(config.Target, out var userMoments)) continue;
                var moments = userMoments.ToArray();
                if (moments.Length == 0)
                {
                    Logger.LogWarning("No moments found for user: {UserId}", config.Target);
                    continue;
                }

                var lastMoment = moments.FirstOrDefault(x => x.Id == config.LastInformation);
                var index = Array.IndexOf(moments, lastMoment);
                if (index == -1)
                {
                    Logger.LogWarning("Moment {MomentId} is not found.", config.LastInformation);
                    lastMoment = null;
                }

                var channel = await DiscordClient.GetChannelAsync(config.ChannelId).ConfigureAwait(false);
                if (channel is not IMessageChannel messageChannel)
                {
                    Logger.LogWarning("Channel {ChannelId} is not found.", config.ChannelId);
                    continue;
                }

                if (lastMoment == null)
                {
                    var moment = moments[0];
                    var embeds = InformationEmbedBuilder.BuildMomentInfo(moment);
                    embeds[^1].WithBilibiliLogoIconFooter();
                    var text = $"User {moment.User?.Name} has a new moment!";
                    await messageChannel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        text, embeds: embeds.Select(x => x.Build()).ToArray()).ConfigureAwait(false);
                    continue;
                }

                for (var i = index - 1; i >= 0; i--)
                {
                    var moment = moments[i];
                    var embeds = InformationEmbedBuilder.BuildMomentInfo(moment);
                    embeds[^1].WithBilibiliLogoIconFooter();
                    var text = $"User {moment.User?.Name} has a new moment!";
                    await messageChannel.SendFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        text, embeds: embeds.Select(x => x.Build()).ToArray()).ConfigureAwait(false);
                }
            }

            Logger.LogInformation("Updating database.");
            await DatabaseProviderService.UpdateAllAsync(configs).ConfigureAwait(false);
            foreach (var config in needRemoved) await DatabaseProviderService.DeleteAsync(config).ConfigureAwait(false);
            Logger.LogInformation("Database updated.");

            Logger.LogInformation("DynamicWatcherScheduleTask is completed.");
        }

        private async Task UpdateMomentsAsync(params IEnumerable<UserMomentsRequest> requests)
        {
            Logger.LogInformation("Updating follows moments.");
            _follows.Clear();
            foreach (var request in requests)
            {
                var userProfile = new UserProfile(request.UserId);
                var (moments, offset, hasMore) = await MomentDiscoveryService
                    .GetUserVideoMomentsAsync(userProfile, request.Offset).ConfigureAwait(false);
                if (moments.Count == 0) continue;
                while (IsSmallerOffset(request.Offset, offset) && hasMore)
                {
                    (var addMoments, offset, hasMore) = await MomentDiscoveryService
                        .GetUserVideoMomentsAsync(userProfile, offset).ConfigureAwait(false);
                    if (addMoments.Count == 0) break;
                    moments = [.. moments, .. addMoments];
                }

                _follows[request.UserId] = moments;
            }

            Logger.LogInformation("Moments updated.");
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