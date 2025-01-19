using System.Text;
using Discord;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Library.Bilibili.DiscordBridges
{
    /// <summary>
    ///     Media information embed builder.
    /// </summary>
    public static class InformationEmbedBuilder
    {
        /// <summary>
        ///     Build my info.
        /// </summary>
        /// <param name="myInfo"></param>
        /// <param name="myCommunityInfo"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildMyInfo(UserDetailProfile myInfo, UserCommunityInformation myCommunityInfo)
        {
            return BuildUserInfo(new(myInfo, myCommunityInfo));
        }

        /// <summary>
        ///     Build video info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildVideoInfo(VideoPlayerView detail)
        {
            const string textFormatTag = "[{0}](https://search.bilibili.com/all?keyword={1})";

            var embed = BuildVideoInfo(detail.Information);

            // tags
            if (detail.Tags is not null)
                embed.AddField("Tags",
                    string.Join(", ",
                        detail.Tags.Select(tag => string.Format(textFormatTag, tag.Name, tag.Name.UrlEncode()))));

            // video parts
            if (detail.Parts is { Count: > 1 })
            {
                embed.AddField("Parts",
                    string.Join("\n",
                        detail.Parts.Select((part, i) =>
                            $"P{i + 1})\u3000{part.Identifier.Title}\n\u3000\u3000\u3000Duration: {TimeSpan.FromSeconds(part.Duration):hh\\:mm\\:ss}")));
                embed.AddField("Total Duration",
                    TimeSpan.FromSeconds(detail.Information.Duration ?? 0).ToString(@"hh\:mm\:ss"));
            }
            else
            {
                embed.AddField("Duration",
                    TimeSpan.FromSeconds(detail.Information.Duration ?? 0).ToString(@"hh\:mm\:ss"));
            }

            return embed;
        }

        /// <summary>
        ///     Build video info.
        /// </summary>
        /// <param name="detail"></param>
        /// <param name="momentInformation"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildVideoInfo(VideoInformation detail, MomentInformation? momentInformation = null)
        {
            const string textFormatVideoBvId = "[{0}](https://www.bilibili.com/video/{0}/)";
            const string textFormatVideoAvId = "[av{0}](https://www.bilibili.com/video/av{0}/)";
            const string textFormatAuthor = "[{0}](https://space.bilibili.com/{1})";

            var embed = new EmbedBuilder();
            embed.WithTitle(detail.Identifier.Title);

            // cover
            if (detail.Identifier.Cover is not null)
                embed.WithImageUrl(detail.Identifier.Cover.SourceUri.ToString());

            // video id
            embed.AddField("BvId", string.Format(textFormatVideoBvId, detail.BvId), true);
            embed.AddField("AvId", string.Format(textFormatVideoAvId, detail.Identifier.Id), true);

            var content = new StringBuilder();

            // author
            {
                var multiAuthors = detail.Collaborators is not null;
                List<string> authors = [];

                var authorBuilder = new EmbedAuthorBuilder();
                embed.WithAuthor(authorBuilder);
                if (momentInformation is null)
                {
                    authorBuilder.WithName(detail.Publisher.User.Name);
                    authorBuilder.WithUrl($"https://space.bilibili.com/{detail.Publisher.User.Id}");
                    if (detail.Publisher.User.Avatar != null)
                        authorBuilder.WithIconUrl(detail.Publisher.User.Avatar.SourceUri.ToString());

                    authors.Add(string.Format(textFormatAuthor, detail.Publisher.User.Name,
                        detail.Publisher.User.Id));
                }
                else if (momentInformation.User is not null)
                {
                    authorBuilder.WithName(momentInformation.User.Name);
                    authorBuilder.WithUrl($"https://space.bilibili.com/{momentInformation.User.Id}");
                    if (momentInformation.User.Avatar is not null)
                        authorBuilder.WithIconUrl(momentInformation.User.Avatar.SourceUri.ToString());

                    authors.Add(string.Format(textFormatAuthor, momentInformation.User.Name,
                        momentInformation.User.Id));
                }

                if (multiAuthors)
                    authors.AddRange(detail.Collaborators!.Select(collaborator =>
                        string.Format(textFormatAuthor, collaborator.User.Name, collaborator.User.Id)));

                embed.AddField(multiAuthors ? "Authors" : "Author", string.Join(", ", authors));
            }

            // statistics
            if (detail.CommunityInformation is not null)
            {
                embed.AddField("Play", detail.CommunityInformation.PlayCount, true);
                embed.AddField("Danmaku", detail.CommunityInformation.DanmakuCount, true);
                embed.AddField("Reply", detail.CommunityInformation.CommentCount, true);
                embed.AddField("Favorite", detail.CommunityInformation.FavoriteCount, true);
                embed.AddField("Coin", detail.CommunityInformation.CoinCount, true);
                embed.AddField("Share", detail.CommunityInformation.ShareCount, true);
                embed.AddField("Like", detail.CommunityInformation.LikeCount, true);
            }

            // description
            var description = detail.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
            if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description.Replace("&amp;", "&"));

            // publish time
            if (detail.PublishTime is not null)
                embed.WithTimestamp(detail.PublishTime.Value);

            embed.WithUrl($"https://www.bilibili.com/video/{detail.BvId}/");
            embed.WithDescription(content.ToString());
            return embed;
        }

        /// <summary>
        ///     Build live info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildLiveInfo(LivePlayerView detail)
        {
            var embed = new EmbedBuilder();
            embed.WithTitle(detail.Information.Identifier.Title);

            // cover
            if (detail.Information.Identifier.Cover is not null)
                embed.WithImageUrl(detail.Information.Identifier.Cover.SourceUri.ToString());

            var description = detail.Information.GetExtensionIfNotNull<string>(LiveExtensionDataId.Description);
            if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description.Replace("&amp;", "&"));

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
            embed.AddField("Is Living", isLiving ? "Yes" : "No", true);

            // start time
            if (isLiving)
            {
                var startTime = detail.Information.GetExtensionIfNotNull<DateTimeOffset>(LiveExtensionDataId.StartTime);
                embed.WithTimestamp(startTime);
            }

            embed.WithUrl($"https://live.bilibili.com/{detail.Information.Identifier.Id}/");

            return embed;
        }

        /// <summary>
        ///     Build live info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildLiveInfo(LiveInformation detail)
        {
            var embed = new EmbedBuilder();
            embed.WithTitle(detail.Identifier.Title);

            // cover
            if (detail.Identifier.Cover is not null)
                embed.WithImageUrl(detail.Identifier.Cover.SourceUri.ToString());

            var description = detail.GetExtensionIfNotNull<string>(LiveExtensionDataId.Description);
            if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description.Replace("&amp;", "&"));

            // author
            var authorBuilder = new EmbedAuthorBuilder();
            authorBuilder.WithName(detail.User.Name);
            authorBuilder.WithUrl($"https://space.bilibili.com/{detail.User.Id}");
            if (detail.User.Avatar != null)
                authorBuilder.WithIconUrl(detail.User.Avatar.SourceUri.ToString());
            embed.WithAuthor(authorBuilder);

            // tag
            var tagName = detail.GetExtensionIfNotNull<string>(LiveExtensionDataId.TagName);
            if (!string.IsNullOrWhiteSpace(tagName)) embed.AddField("Tag", tagName, true);

            // viewer count
            var viewerCount = detail.GetExtensionIfNotNull<int>(LiveExtensionDataId.ViewerCount);
            embed.AddField("Viewer Count", viewerCount.ToString(), true);

            // is living
            var isLiving = detail.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
            embed.AddField("Is Living", isLiving ? "Yes" : "No", true);

            // start time
            if (isLiving)
            {
                var startTime = detail.GetExtensionIfNotNull<DateTimeOffset>(LiveExtensionDataId.StartTime);
                embed.WithTimestamp(startTime);
            }

            embed.WithUrl($"https://live.bilibili.com/{detail.Identifier.Id}/");

            return embed;
        }

        /// <summary>
        ///     Build user info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildUserInfo(UserCard detail)
        {
            if (detail.Profile is null) return new EmbedBuilder().WithTitle("User Not Found");
            var embed = new EmbedBuilder();
            embed.WithTitle(detail.Profile.User.Name);

            if (detail.Profile.User.Avatar is not null)
                embed.WithThumbnailUrl(detail.Profile.User.Avatar.SourceUri.ToString());

            embed.AddField("UID", detail.Profile.User.Id, true);
            if (detail.Profile.Level.HasValue) embed.AddField("Level", detail.Profile.Level, true);

            embed.AddField("Is Hardcore", detail.Profile.IsHardcore ?? false, true);

            if (!string.IsNullOrWhiteSpace(detail.Profile.Introduce))
                embed.WithDescription(detail.Profile.Introduce);

            if (detail.Community?.FollowCount is not null)
                embed.AddField("Follows", detail.Community.FollowCount, true);

            if (detail.Community?.FansCount is not null)
                embed.AddField("Fans", detail.Community.FansCount, true);

            embed.AddField("Is Vip", detail.Profile.IsVip ?? false, true);

            embed.WithUrl($"https://space.bilibili.com/{detail.Profile.User.Id}");

            return embed;
        }

        /// <summary>
        ///     Build moment info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder[] BuildMomentInfo(MomentInformation detail)
        {
            if (detail.User is null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Error")
                    .WithDescription("Moment user is null.");
                return [errorEmbed];
            }

            List<EmbedBuilder> embeds = [];
            var embed = new EmbedBuilder();
            embeds.Add(embed);
            var authorBuilder = new EmbedAuthorBuilder();
            authorBuilder.WithName(detail.User.Name);
            authorBuilder.WithUrl($"https://space.bilibili.com/{detail.User.Id}");
            if (detail.User.Avatar is not null)
                authorBuilder.WithIconUrl(detail.User.Avatar.SourceUri.ToString());
            embed.WithAuthor(authorBuilder);
            embed.WithUrl($"https://www.bilibili.com/opus/{detail.Id}");

            if (detail.Description is not null && !string.IsNullOrWhiteSpace(detail.Description.Text))
                embed.WithDescription(detail.Description.Text);

            switch (detail.MomentType)
            {
                case MomentItemType.Video:
                {
                    if (detail.Data is not VideoInformation videoInformation)
                    {
                        var errorEmbed = new EmbedBuilder();
                        errorEmbed.WithTitle("Moment Type Error");
                        errorEmbed.WithColor(Color.Red);
                        errorEmbed.WithDescription("Moment type is not video.");
                        embeds.Add(errorEmbed);
                        break;
                    }

                    embeds.Add(BuildVideoInfo(videoInformation, detail));
                    break;
                }
                case MomentItemType.Pgc:
                {
                    var errorEmbed = new EmbedBuilder();
                    errorEmbed.WithTitle("Moment Type Error");
                    errorEmbed.WithColor(Color.Red);
                    errorEmbed.WithDescription("Moment type is not supported.");
                    embeds.Add(errorEmbed);
                    break;
                }
                case MomentItemType.Article:
                {
                    if (detail.Data is not ArticleInformation)
                    {
                        var errorEmbed = new EmbedBuilder();
                        errorEmbed.WithTitle("Moment Type Error");
                        errorEmbed.WithColor(Color.Red);
                        errorEmbed.WithDescription("Moment type is not article.");
                        embeds.Add(errorEmbed);
                        break;
                    }

                    // extraEmbed = BuildArticleInfo(articleInformation);
                    var extraEmbed = new EmbedBuilder();
                    extraEmbed.WithTitle("Moment Type Error");
                    extraEmbed.WithColor(Color.Red);
                    extraEmbed.WithDescription("Moment type is not supported.");
                    embeds.Add(extraEmbed);
                    break;
                }
                case MomentItemType.Image:
                {
                    if (detail.Data is not List<BiliImage> images)
                    {
                        var errorEmbed = new EmbedBuilder();
                        errorEmbed.WithTitle("Moment Type Error");
                        errorEmbed.WithColor(Color.Red);
                        errorEmbed.WithDescription("Moment type is not image.");
                        embeds.Add(errorEmbed);
                        break;
                    }

                    var extraEmbed = new EmbedBuilder();
                    extraEmbed.WithDescription(string.Join('\n', images.Select(image => image.SourceUri.ToString())));
                    embeds.Add(extraEmbed);
                    break;
                }
                case MomentItemType.Forward:
                {
                    if (detail.Data is not MomentInformation forward)
                    {
                        var errorEmbed = new EmbedBuilder();
                        errorEmbed.WithTitle("Moment Type Error");
                        errorEmbed.WithColor(Color.Red);
                        errorEmbed.WithDescription("Moment type is not forward.");
                        embeds.Add(errorEmbed);
                        break;
                    }

                    embeds.AddRange(BuildMomentInfo(forward));
                    break;
                }
                case MomentItemType.Unsupported:
                {
                    var errorEmbed = new EmbedBuilder();
                    errorEmbed.WithTitle("Moment Type Error");
                    errorEmbed.WithColor(Color.Red);
                    errorEmbed.WithDescription("Moment type is not supported.");
                    embeds.Add(errorEmbed);
                    break;
                }
            }

            return [..embeds];
        }
    }
}