using System.Globalization;
using System.Text;
using Discord;
using Richasy.BiliKernel.Models.Media;
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
            var embed = new EmbedBuilder();
            embed.WithTitle("Bilibili User Info");
            if (myInfo.User.Avatar is not null) embed.WithThumbnailUrl(myInfo.User.Avatar.SourceUri.ToString());
            embed.AddField("Username", myInfo.User.Name);
            embed.AddField("UID", myInfo.User.Id);
            if (myInfo.Level.HasValue) embed.AddField("Level", myInfo.Level);
            embed.AddField("Is Hardcore", myInfo.IsHardcore ?? false);
            embed.AddField("Is Vip", myInfo.IsVip ?? false);
            if (!string.IsNullOrWhiteSpace(myInfo.Introduce))
                embed.WithDescription(myInfo.Introduce);

            embed.AddField("Coins",
                myCommunityInfo.CoinCount.HasValue
                    ? myCommunityInfo.CoinCount.Value.ToString(CultureInfo.CurrentCulture)
                    : "Unknown");
            embed.AddField("Follows",
                myCommunityInfo.FollowCount.HasValue
                    ? myCommunityInfo.FollowCount.Value.ToString(CultureInfo.CurrentCulture)
                    : "Unknown");
            embed.AddField("Fans",
                myCommunityInfo.FansCount.HasValue
                    ? myCommunityInfo.FansCount.Value.ToString(CultureInfo.CurrentCulture)
                    : "Unknown");
            embed.AddField("Moments",
                myCommunityInfo.MomentCount.HasValue
                    ? myCommunityInfo.MomentCount.Value.ToString(CultureInfo.CurrentCulture)
                    : "Unknown");

            return embed;
        }

        /// <summary>
        ///     Build video info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static EmbedBuilder BuildVideoInfo(VideoPlayerView detail)
        {
            const string textFormatVideoBvId = "[{0}](https://www.bilibili.com/video/{0}/)";
            const string textFormatVideoAvId = "[av{0}](https://www.bilibili.com/video/av{0}/)";
            const string textFormatAuthor = "[{0}](https://space.bilibili.com/{1})";
            const string textFormatTag = "[{0}](https://search.bilibili.com/all?keyword={1})";

            var embed = new EmbedBuilder();
            embed.WithTitle(detail.Information.Identifier.Title);

            // cover
            if (detail.Information.Identifier.Cover is not null)
                embed.WithImageUrl(detail.Information.Identifier.Cover.SourceUri.ToString());

            // video id
            embed.AddField("BvId", string.Format(textFormatVideoBvId, detail.Information.BvId), true);
            embed.AddField("AvId", string.Format(textFormatVideoAvId, detail.Information.Identifier.Id), true);

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
                    string.Format(textFormatAuthor, detail.Information.Publisher.User.Name,
                        detail.Information.Publisher.User.Id),
                };
                if (multiAuthors)
                    authors.AddRange(detail.Information.Collaborators!.Select(collaborator =>
                        string.Format(textFormatAuthor, collaborator.User.Name, collaborator.User.Id)));

                embed.AddField(multiAuthors ? "Authors" : "Author", string.Join(", ", authors));
            }

            // tags
            if (detail.Tags is not null) embed.AddField("Tags", string.Join(", ", detail.Tags.Select(tag => string.Format(textFormatTag, tag.Name, tag.Name.UrlEncode()))));

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

            embed.WithUrl($"https://www.bilibili.com/video/{detail.Information.BvId}/");
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

            embed.WithUrl($"https://live.bilibili.com/{detail.Information.Identifier.Id}/");

            return embed;
        }
    }
}