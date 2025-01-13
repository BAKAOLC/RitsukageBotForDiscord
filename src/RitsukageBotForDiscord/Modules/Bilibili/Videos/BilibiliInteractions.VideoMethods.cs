using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.Utils;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class VideoInteractions
        {
            private const string TextFormatVideoBvId = "[{0}](https://www.bilibili.com/video/{0}/)";
            private const string TextFormatVideoAvId = "[av{0}](https://www.bilibili.com/video/av{0}/)";
            private const string TextFormatAuthor = "[{0}](https://space.bilibili.com/{1})";
            private const string TextFormatTag = "[{0}](https://search.bilibili.com/all?keyword={0})";

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
                var detail = await PlayerService.GetVideoPageDetailAsync(media).ConfigureAwait(false);
                var embed = new EmbedBuilder();
                embed.WithColor(Color.Green);
                embed.WithTitle(detail.Information.Identifier.Title);

                // cover
                if (detail.Information.Identifier.Cover is not null)
                    embed.WithImageUrl(detail.Information.Identifier.Cover.SourceUri.ToString());

                var content = new StringBuilder();

                // video id
                content.AppendFormat(TextFormatVideoBvId, detail.Information.BvId);
                content.Append("\u3000\u3000");
                content.AppendFormat(TextFormatVideoAvId, detail.Information.Identifier.Id);

                // author
                {
                    var authorBuilder = new EmbedAuthorBuilder();
                    authorBuilder.WithName(detail.Information.Publisher.User.Name);
                    authorBuilder.WithUrl($"https://space.bilibili.com/{detail.Information.Publisher.User.Id}");
                    if (detail.Information.Publisher.User.Avatar != null)
                        authorBuilder.WithIconUrl(detail.Information.Publisher.User.Avatar.SourceUri.ToString());
                    embed.WithAuthor(authorBuilder);

                    var multiAuthors = detail.Information.Collaborators is not null;
                    content.AppendLine();
                    content.AppendLine();
                    content.Append(multiAuthors ? "Authors:" : "Author:");
                    content.AppendLine();
                    content.AppendFormat(TextFormatAuthor, detail.Information.Publisher.User.Name,
                        detail.Information.Publisher.User.Id);

                    if (multiAuthors)
                        foreach (var collaborator in detail.Information.Collaborators!)
                            content.Append(',').Append(' ').AppendFormat(TextFormatAuthor, collaborator.User.Name,
                                collaborator.User.Id);
                }

                // tags
                if (detail.Tags is not null)
                {
                    content.AppendLine();
                    content.AppendLine();
                    content.Append("Tags:");
                    content.AppendLine();
                    content.Append(string.Join(", ",
                        detail.Tags.Select(tag => string.Format(TextFormatTag, tag.Name))));
                }

                // video parts
                if (detail.Parts is not null)
                {
                    content.AppendLine();
                    content.AppendLine();
                    content.Append("Parts:");
                    for (var i = 0; i < detail.Parts.Count; i++)
                    {
                        var part = detail.Parts[i];
                        content.AppendLine();
                        content.Append('P');
                        content.Append(i + 1);
                        content.Append(") ");
                        content.Append(part.Identifier.Title);
                        content.Append("\u3000\u3000");
                        content.Append(TimeSpan.FromSeconds(part.Duration).ToString(@"hh\:mm\:ss"));
                    }
                }

                content.AppendLine();
                content.Append("Total Duration: ")
                    .Append(TimeSpan.FromSeconds(detail.Information.Duration ?? 0).ToString(@"hh\:mm\:ss"));

                // statistics
                if (detail.Information.CommunityInformation is not null)
                {
                    content.AppendLine();
                    content.AppendLine();
                    content.Append("Statistics:");
                    content.AppendLine();
                    content.Append("Play: ").Append(detail.Information.CommunityInformation.PlayCount);
                    content.Append("\u3000\u3000");
                    content.Append("Danmaku: ").Append(detail.Information.CommunityInformation.DanmakuCount);
                    content.Append("\u3000\u3000");
                    content.Append("Reply: ").Append(detail.Information.CommunityInformation.CommentCount);
                    content.Append("\u3000\u3000");
                    content.Append("Favorite: ").Append(detail.Information.CommunityInformation.FavoriteCount);
                    content.Append("\u3000\u3000");
                    content.Append("Coin: ").Append(detail.Information.CommunityInformation.CoinCount);
                    content.Append("\u3000\u3000");
                    content.Append("Share: ").Append(detail.Information.CommunityInformation.ShareCount);
                    content.Append("\u3000\u3000");
                    content.Append("Like: ").Append(detail.Information.CommunityInformation.LikeCount);
                }

                // description
                var description = detail.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    content.AppendLine();
                    content.AppendLine();
                    content.Append("Description:");
                    content.AppendLine();
                    content.Append(description);
                }

                // footer
                {
                    var footerBuilder = new EmbedFooterBuilder();
                    footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
                    footerBuilder.WithText("Bilibili");
                    embed.WithFooter(footerBuilder);
                }

                // publish time
                if (detail.Information.PublishTime is not null)
                    embed.WithTimestamp(detail.Information.PublishTime.Value);

                content.AppendLine();
                content.AppendLine();
                content.Append($"https://www.bilibili.com/video/{detail.Information.BvId}/");
                embed.WithDescription(content.ToString());

                await FollowupWithFileAsync(new MemoryStream(BilibiliIconData), "bilibili-icon.png",
                    embed: embed.Build()).ConfigureAwait(false);
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