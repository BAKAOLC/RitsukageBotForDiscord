using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.Media;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class LiveInteractions
        {
            private static readonly Regex MatchLiveIdRegex = GetMatchLiveIdRegex();

            /// <summary>
            ///     Get live information
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("info", "Get live information")]
            public async Task GetVideoInfoAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                ulong roomId;

                id = id.Trim();

                if (MatchLiveIdRegex.IsMatch(id))
                {
                    var match = MatchLiveIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out roomId) || roomId == 0)
                {
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                        .WithDescription("Invalid video id.").Build()).ConfigureAwait(false);
                    return;
                }

                var media = new MediaIdentifier(roomId.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetLivePageDetailAsync(media).ConfigureAwait(false);
                    var embed = CreateLiveInfoEmbed(detail);
                    await FollowupWithFileAsync(new MemoryStream(BilibiliIconData), "bilibili-icon.png",
                        embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get live information.");
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Failed to get live information: " + ex.Message).Build())
                        .ConfigureAwait(false);
                }
            }

            private static EmbedBuilder CreateLiveInfoEmbed(LivePlayerView detail)
            {
                var embed = new EmbedBuilder();
                embed.WithColor(Color.Green);
                embed.WithTitle(detail.Information.Identifier.Title);

                // cover
                if (detail.Information.Identifier.Cover is not null)
                    embed.WithImageUrl(detail.Information.Identifier.Cover.SourceUri.ToString());

                var description = detail.Information.GetExtensionIfNotNull<string>(LiveExtensionDataId.Description);
                if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description);

                // author
                var authorBuilder = new EmbedAuthorBuilder();
                authorBuilder.WithName(detail.Information.User.Name);
                authorBuilder.WithUrl($"https://space.bilibili.com/{detail.Information.User.Id}");
                if (detail.Information.User.Avatar != null)
                    authorBuilder.WithIconUrl(detail.Information.User.Avatar.SourceUri.ToString());
                embed.WithAuthor(authorBuilder);

                // tag
                embed.AddField("Tag", detail.Tag.Name, true);

                // viewer count
                var viewerCount = detail.Information.GetExtensionIfNotNull<int>(LiveExtensionDataId.ViewerCount);
                embed.AddField("Viewer Count", viewerCount.ToString(), true);

                // is living
                var isLiving = detail.Information.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
                embed.AddField("Is Living", isLiving ? "Yes" : "No");

                // start time
                if (isLiving)
                {
                    var startTime = detail.Information.GetExtensionIfNotNull<DateTimeOffset>(LiveExtensionDataId.StartTime);
                    embed.WithTimestamp(startTime);
                }

                // footer
                {
                    var footerBuilder = new EmbedFooterBuilder();
                    footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
                    footerBuilder.WithText("Bilibili");
                    embed.WithFooter(footerBuilder);
                }

                embed.WithUrl($"https://live.bilibili.com/{detail.Information.Identifier.Id}/");

                return embed;
            }

            [GeneratedRegex(@"((https?://)?live\.bilibili\.com/)(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
            private static partial Regex GetMatchLiveIdRegex();
        }
    }

    public partial class BilibiliInteractionButton
    {
        public partial class LiveInteractionsButton
        {
        }
    }
}