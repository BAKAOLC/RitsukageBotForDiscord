using System.Text;
using Discord;
using Richasy.BiliKernel.Models;
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
            if (!string.IsNullOrWhiteSpace(description)) embed.WithDescription(description.Replace("&amp;", "&"));

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
        /// <param name="fileAttachments"></param>
        /// <returns></returns>
        public static EmbedBuilder[] BuildMomentInfo(MomentInformation detail, out IEnumerable<FileAttachment> fileAttachments)
        {
            fileAttachments = [];
            if (detail.User is null) return [new EmbedBuilder().WithTitle("User Not Found")];
            var embed = new EmbedBuilder();

            var authorBuilder = new EmbedAuthorBuilder();
            authorBuilder.WithName(detail.User.Name);
            authorBuilder.WithUrl($"https://space.bilibili.com/{detail.User.Id}");
            if (detail.User.Avatar is not null)
                authorBuilder.WithIconUrl(detail.User.Avatar.SourceUri.ToString());
            embed.WithAuthor(authorBuilder);

            switch (detail.MomentType)
            {
                case MomentItemType.Video:
                case MomentItemType.Pgc:
                case MomentItemType.Article:
                case MomentItemType.Image:
                case MomentItemType.PlainText:
                case MomentItemType.Forward:
                case MomentItemType.Unsupported:
                    break;
            }

            embed.WithUrl($"https://www.bilibili.com/opus/{detail.Id}");

            return [embed];
        }
    }
}