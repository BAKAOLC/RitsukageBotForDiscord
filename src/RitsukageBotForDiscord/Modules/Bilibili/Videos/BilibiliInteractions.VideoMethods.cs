using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class VideoInteractions
        {
            private const string TextFormatVideoBvId = "[{0}](https://www.bilibili.com/video/{0}/)";
            private const string TextFormatVideoAvId = "[av{0}](https://www.bilibili.com/video/av{0}/)";
            private const string TextFormatAuthor = "[{0}](https://space.bilibili.com/{1})";
            private const string TextFormatTag = "[{0}](https://search.bilibili.com/all?keyword={1})";

            private static readonly Regex MatchVideoIdRegex = GetMatchVideoIdRegex();

            /// <summary>
            ///     Get video information
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("info", "Get video information")]
            public async Task GetVideoInfoAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                ulong avid;

                id = id.Trim();

                if (MatchVideoIdRegex.IsMatch(id))
                {
                    var match = MatchVideoIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (id.StartsWith("bv", StringComparison.CurrentCultureIgnoreCase))
                {
                    try
                    {
                        avid = VideoIdConverter.ToAvid(id);
                    }
                    catch (Exception e)
                    {
                        await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription(e.Message).Build()).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    if (id.StartsWith("av", StringComparison.CurrentCultureIgnoreCase)) id = id[2..];

                    if (!ulong.TryParse(id, out avid))
                    {
                        await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Invalid video id.").Build()).ConfigureAwait(false);
                        return;
                    }
                }

                if (avid == 0)
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                        .WithDescription("Invalid video id.").Build()).ConfigureAwait(false);

                var media = new MediaIdentifier(avid.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetVideoPageDetailAsync(media).ConfigureAwait(false);
                    var embed = CreateVideoInfoEmbed(detail);
                    await FollowupWithFileAsync(new MemoryStream(BilibiliIconData), "bilibili-icon.png",
                        embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get video information.");
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Failed to get video information: " + ex.Message).Build())
                        .ConfigureAwait(false);
                }
            }

            private static EmbedBuilder CreateVideoInfoEmbed(VideoPlayerView detail)
            {
                var embed = new EmbedBuilder();
                embed.WithColor(Color.Green);
                embed.WithTitle(detail.Information.Identifier.Title);

                // cover
                if (detail.Information.Identifier.Cover is not null)
                    embed.WithImageUrl(detail.Information.Identifier.Cover.SourceUri.ToString());

                // video id
                embed.AddField("BvId", string.Format(TextFormatVideoBvId, detail.Information.BvId), true);
                embed.AddField("AvId", string.Format(TextFormatVideoAvId, detail.Information.Identifier.Id), true);

                var content = new StringBuilder();

                // author
                {
                    var authorBuilder = new EmbedAuthorBuilder();
                    authorBuilder.WithName(detail.Information.Publisher.User.Name);
                    authorBuilder.WithUrl($"https://space.bilibili.com/{detail.Information.Publisher.User.Id}");
                    if (detail.Information.Publisher.User.Avatar != null)
                        authorBuilder.WithIconUrl(detail.Information.Publisher.User.Avatar.SourceUri.ToString());
                    embed.WithAuthor(authorBuilder);

                    var multiAuthors = detail.Information.Collaborators is not null;
                    var authors = new List<string>
                    {
                        string.Format(TextFormatAuthor, detail.Information.Publisher.User.Name,
                            detail.Information.Publisher.User.Id),
                    };
                    if (multiAuthors)
                        authors.AddRange(detail.Information.Collaborators!.Select(collaborator =>
                            string.Format(TextFormatAuthor, collaborator.User.Name, collaborator.User.Id)));

                    embed.AddField(multiAuthors ? "Authors" : "Author", string.Join(", ", authors));
                }

                // tags
                if (detail.Tags is not null) embed.AddField("Tags", string.Join(", ", detail.Tags.Select(tag => string.Format(TextFormatTag, tag.Name, tag.Name.UrlEncode()))));

                // video parts
                if (detail.Parts is { Count: > 1 })
                {
                    embed.AddField("Parts", string.Join("\n", detail.Parts.Select((part, i) => $"P{i + 1})\u3000{part.Identifier.Title}\n\u3000\u3000\u3000Duration: {TimeSpan.FromSeconds(part.Duration):hh\\:mm\\:ss}")));
                    embed.AddField("Total Duration", TimeSpan.FromSeconds(detail.Information.Duration ?? 0).ToString(@"hh\:mm\:ss"));
                }
                else
                {
                    embed.AddField("Duration", TimeSpan.FromSeconds(detail.Information.Duration ?? 0).ToString(@"hh\:mm\:ss"));
                }

                // statistics
                if (detail.Information.CommunityInformation is not null)
                {
                    embed.AddField("Play", detail.Information.CommunityInformation.PlayCount, true);
                    embed.AddField("Danmaku", detail.Information.CommunityInformation.DanmakuCount, true);
                    embed.AddField("Reply", detail.Information.CommunityInformation.CommentCount, true);
                    embed.AddField("Favorite", detail.Information.CommunityInformation.FavoriteCount, true);
                    embed.AddField("Coin", detail.Information.CommunityInformation.CoinCount, true);
                    embed.AddField("Share", detail.Information.CommunityInformation.ShareCount, true);
                    embed.AddField("Like", detail.Information.CommunityInformation.LikeCount, true);
                }

                // description
                var description = detail.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
                if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description);

                // publish time
                if (detail.Information.PublishTime is not null)
                    embed.WithTimestamp(detail.Information.PublishTime.Value);

                // footer
                {
                    var footerBuilder = new EmbedFooterBuilder();
                    footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
                    footerBuilder.WithText("Bilibili");
                    embed.WithFooter(footerBuilder);
                }

                embed.WithUrl($"https://www.bilibili.com/video/{detail.Information.BvId}/");
                embed.WithDescription(content.ToString());
                return embed;
            }

            [GeneratedRegex(@"((https?://)?www\.bilibili\.com/video/)(?<id>[0-9a-zA-Z]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)]
            private static partial Regex GetMatchVideoIdRegex();
        }
    }

    public partial class BilibiliInteractionButton
    {
        public partial class VideoInteractionsButton
        {
        }
    }
}