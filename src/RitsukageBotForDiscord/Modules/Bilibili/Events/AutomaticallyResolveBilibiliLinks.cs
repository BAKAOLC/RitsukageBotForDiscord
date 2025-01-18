using System.Text.RegularExpressions;
using Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Events;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Bilibili.Events
{
    /// <summary>
    ///     Automatically resolve Bilibili links.
    /// </summary>
    public partial class AutomaticallyResolveBilibiliLinks : INotificationHandler<MessageNotification>
    {
        private const string BilibiliVideoUrl = "https://www.bilibili.com/video/";

        /// <inheritdoc />
        public async Task Handle(MessageNotification messageNotification, CancellationToken cancellationToken)
        {
            var logger = messageNotification.Services.GetRequiredService<ILogger<AutomaticallyResolveBilibiliLinks>>();

            var biliKernelProvider = messageNotification.Services.GetRequiredService<BiliKernelProviderService>();
            var databaseProviderService = messageNotification.Services.GetRequiredService<DatabaseProviderService>();

            var message = messageNotification.Message;
            if (message.Author.IsBot) return;

            var channelId = message.Channel.Id;
            var config = await GetConfigAsync(databaseProviderService, channelId).ConfigureAwait(false);
            if (config is null) return;

            var keyIds = await ResolveKeyId(message.Content).ConfigureAwait(false);
            if (keyIds.Length == 0) return;

            var footerBuilder = new EmbedFooterBuilder();
            footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
            footerBuilder.WithText("Bilibili");

            foreach (var keyId in keyIds)
            {
                EmbedBuilder? embed = null;
                try
                {
                    switch (keyId.Type)
                    {
                        case KeyIdType.Video:
                            logger.LogInformation("Try to resolve video {VideoId}", keyId.Id);
                            var videoMediaIdentifier = new MediaIdentifier(keyId.Id, null, null);
                            var videoPlayerView = await biliKernelProvider.GetRequiredService<IPlayerService>()
                                .GetVideoPageDetailAsync(videoMediaIdentifier, cancellationToken).ConfigureAwait(false);
                            embed = InformationEmbedBuilder.BuildVideoInfo(videoPlayerView);
                            break;
                        case KeyIdType.Live:
                            logger.LogInformation("Try to resolve live {LiveId}", keyId.Id);
                            var liveMediaIdentifier = new MediaIdentifier(keyId.Id, null, null);
                            var livePlayerView = await biliKernelProvider.GetRequiredService<IPlayerService>()
                                .GetLivePageDetailAsync(liveMediaIdentifier, cancellationToken).ConfigureAwait(false);
                            embed = InformationEmbedBuilder.BuildLiveInfo(livePlayerView);
                            break;
                        case KeyIdType.Dynamic:
                            break;
                        case KeyIdType.User:
                        {
                            logger.LogInformation("Try to resolve user {UserId}", keyId.Id);
                            var userCard = await biliKernelProvider.GetRequiredService<IUserService>()
                                .GetUserInformationAsync(keyId.Id, cancellationToken).ConfigureAwait(false);
                            embed = InformationEmbedBuilder.BuildUserInfo(userCard);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to resolve key id {KeyId}", keyId);
                }

                if (embed is null) continue;
                embed.WithFooter(footerBuilder);
                await message.Channel
                    .SendFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: embed.Build())
                    .ConfigureAwait(false);
            }
        }

        private static async Task<DiscordChannelConfiguration?> GetConfigAsync(DatabaseProviderService database,
            ulong channelId)
        {
            try
            {
                return await database.GetAsync<DiscordChannelConfiguration>(channelId).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static async Task<KeyId[]> ResolveKeyId(string content)
        {
            HashSet<KeyId> keyIds = [];
            var firstMatch = MatchKeyId(content);
            if (firstMatch.Length > 0) keyIds.UnionWith(firstMatch);
            var urls = content.MatchUrls();
            var resolvedUrls = await ResolveShortLinkAsync(urls).ConfigureAwait(false);
            foreach (var url in resolvedUrls)
            {
                var secondMatch = MatchKeyId(url);
                if (secondMatch.Length > 0) keyIds.UnionWith(secondMatch);
            }

            return [.. keyIds];
        }

        private static KeyId[] MatchKeyId(string content)
        {
            List<KeyId> keyIds = [];

            var videoMatches = GetVideoRegex().Matches(content);
            if (videoMatches.Count > 0)
                foreach (Match match in videoMatches)
                {
                    var id = match.Groups["id"].Value;
                    if (string.IsNullOrEmpty(id)) continue;
                    keyIds.Add(new()
                        { Type = KeyIdType.Video, Id = id });
                }

            var liveRoomMatches = GetLiveRoomRegex().Matches(content);
            if (liveRoomMatches.Count > 0)
                foreach (Match match in liveRoomMatches)
                {
                    var id = match.Groups["id"].Value;
                    if (string.IsNullOrEmpty(id)) continue;
                    keyIds.Add(new()
                        { Type = KeyIdType.Live, Id = id });
                }

            var dynamicMatches = GetDynamicRegex().Matches(content);
            if (dynamicMatches.Count > 0)
                foreach (Match match in dynamicMatches)
                {
                    var id = match.Groups["id"].Value;
                    if (string.IsNullOrEmpty(id)) continue;
                    keyIds.Add(new()
                        { Type = KeyIdType.Dynamic, Id = id });
                }

            var userMatches = GetUserRegex().Matches(content);
            // ReSharper disable once InvertIf
            if (userMatches.Count > 0)
                foreach (Match match in userMatches)
                {
                    var id = match.Groups["id"].Value;
                    if (string.IsNullOrEmpty(id)) continue;
                    keyIds.Add(new()
                        { Type = KeyIdType.User, Id = id });
                }

            return [.. keyIds];
        }

        private static async Task<string[]> ResolveShortLinkAsync(string[] urls)
        {
            List<string> resolvedUrls = [];
            foreach (var url in urls)
            {
                var match = GetShortLinkRegex().Match(url);
                if (!match.Success) continue;

                var data = match.Groups["data"].Value;
                if (string.IsNullOrEmpty(data)) continue;

                if (IsAvOrBv(data))
                {
                    resolvedUrls.Add($"{BilibiliVideoUrl}{data}");
                    continue;
                }

                var resolvedUrl = await NetworkUtility.SolveShortLinkAsync(url).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(resolvedUrl)) resolvedUrls.Add(resolvedUrl);
            }

            return [.. resolvedUrls];
        }

        private static bool IsAvOrBv(string url)
        {
            return GetAvidRegex().IsMatch(url) || GetBvidRegex().IsMatch(url);
        }


        [GeneratedRegex("((https?://)?b23\\.tv/)(?<data>[0-9a-zA-Z]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetShortLinkRegex();

        [GeneratedRegex(@"((https?://)?space\.bilibili\.com/)(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetUserRegex();

        [GeneratedRegex(@"^\s*[Aa][Vv](?<av>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetAvidRegex();

        [GeneratedRegex(@"^\s*[Bb][Vv](?<bv>1[1-9a-km-zA-HJ-NP-Z]{9})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetBvidRegex();

        [GeneratedRegex(@"((https?://)?www\.bilibili\.com/video/)(?<id>[0-9a-zA-Z]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetVideoRegex();

        [GeneratedRegex(@"((https?://)?live\.bilibili\.com/)(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetLiveRoomRegex();

        [GeneratedRegex(@"((https?://)?t\.bilibili\.com/)(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex GetDynamicRegex();

        private enum KeyIdType
        {
            Video,
            Live,
            Dynamic,
            User,
        }

        private record struct KeyId
        {
            public KeyIdType Type { get; init; }

            public string Id { get; init; }
        }
    }
}