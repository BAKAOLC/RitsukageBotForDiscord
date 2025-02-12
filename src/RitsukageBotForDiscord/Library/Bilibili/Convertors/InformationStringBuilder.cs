using System.Text;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Library.Bilibili.Convertors
{
    /// <summary>
    ///     Convert bilibili information to <see cref="string" />
    /// </summary>
    public static class InformationStringBuilder
    {
        /// <summary>
        ///     Build my info.
        /// </summary>
        /// <param name="myInfo"></param>
        /// <param name="myCommunityInfo"></param>
        /// <returns></returns>
        public static string BuildMyInfo(UserDetailProfile myInfo, UserCommunityInformation myCommunityInfo)
        {
            return BuildUserInfo(new(myInfo, myCommunityInfo));
        }

        /// <summary>
        ///     Build user info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static string BuildUserInfo(UserCard detail)
        {
            var builder = new StringBuilder();
            if (detail.Profile is null)
            {
                builder.AppendLine("User Not Found");
                return builder.ToString();
            }

            builder.AppendLine($"UID: {detail.Profile.User.Id}");
            builder.AppendLine($"Name: {detail.Profile.User.Name}");
            builder.AppendLine($"Is Hardcore: {detail.Profile.IsHardcore}");
            builder.AppendLine($"Is Vip: {detail.Profile.IsVip}");
            builder.AppendLine($"Level: {detail.Profile.Level}");
            if (detail.Community is not null)
            {
                builder.AppendLine($"Follows: {detail.Community.FollowCount}");
                builder.AppendLine($"Fans: {detail.Community.FansCount}");
            }

            builder.Append($"URL: https://space.bilibili.com/{detail.Profile.User.Id}");
            return builder.ToString();
        }

        /// <summary>
        ///     Build video info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static string BuildVideoInfo(VideoPlayerView detail)
        {
            var sb = new StringBuilder();
            sb.AppendLine(detail.Information.Identifier.Title);

            {
                var multiAuthors = detail.Information.Collaborators is not null;
                List<string> authors =
                [
                    detail.Information.Publisher.User.Name!,
                ];

                if (multiAuthors)
                    authors.AddRange(detail.Information.Collaborators!.Select(x => x.User.Name!));

                sb.AppendLine($"Author: {string.Join(", ", authors)}");
            }

            if (detail.Tags is not null)
                sb.AppendLine($"Tags: {string.Join(", ", detail.Tags.Select(x => x.Name))}");

            if (detail.Parts is { Count: > 1 })
            {
                sb.AppendLine($"Parts: {string.Join('\n', detail.Parts.Select((part, i) =>
                    $"P{i + 1}. {part.Identifier.Title}\n    Duration: {TimeSpan.FromSeconds(part.Duration):hh\\:mm\\:ss}"))}");
                sb.AppendLine($@"Total Duration: {TimeSpan.FromSeconds(detail.Information.Duration ?? 0):hh\:mm\:ss}");
            }
            else
            {
                sb.AppendLine($@"Duration: {TimeSpan.FromSeconds(detail.Information.Duration ?? 0):hh\:mm\:ss}");
            }

            if (detail.Information.CommunityInformation is not null)
            {
                if (detail.Information.CommunityInformation.PlayCount.HasValue)
                    sb.AppendLine($"Play: {detail.Information.CommunityInformation.PlayCount}");
                if (detail.Information.CommunityInformation.DanmakuCount.HasValue)
                    sb.AppendLine($"Danmaku: {detail.Information.CommunityInformation.DanmakuCount}");
                if (detail.Information.CommunityInformation.CommentCount.HasValue)
                    sb.AppendLine($"Reply: {detail.Information.CommunityInformation.CommentCount}");
                if (detail.Information.CommunityInformation.FavoriteCount.HasValue)
                    sb.AppendLine($"Favorite: {detail.Information.CommunityInformation.FavoriteCount}");
                if (detail.Information.CommunityInformation.CoinCount.HasValue)
                    sb.AppendLine($"Coin: {detail.Information.CommunityInformation.CoinCount}");
                if (detail.Information.CommunityInformation.ShareCount.HasValue)
                    sb.AppendLine($"Share: {detail.Information.CommunityInformation.ShareCount}");
                if (detail.Information.CommunityInformation.LikeCount.HasValue)
                    sb.AppendLine($"Like: {detail.Information.CommunityInformation.LikeCount}");
            }

            if (detail.Information.PublishTime is not null)
                sb.AppendLine($"Publish Time: {detail.Information.PublishTime}");

            var description = detail.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
            if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine(description.Replace("&amp;", "&"));

            sb.AppendLine().AppendLine($"URL: https://www.bilibili.com/video/{detail.Information.BvId}/");

            return sb.ToString();
        }

        /// <summary>
        ///     Build video info.
        /// </summary>
        /// <param name="detail"></param>
        /// <param name="momentInformation"></param>
        /// <returns></returns>
        public static string BuildVideoInfo(VideoInformation detail, MomentInformation? momentInformation = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(detail.Identifier.Title);

            {
                var multiAuthors = detail.Collaborators is not null;
                List<string> authors = [];

                if (momentInformation is null)
                    authors.Add(detail.Publisher.User.Name!);
                else if (momentInformation.User is not null) authors.Add(momentInformation.User.Name!);

                if (multiAuthors)
                    authors.AddRange(detail.Collaborators!.Select(x => x.User.Name!));

                sb.AppendLine($"Author: {string.Join(", ", authors)}");
            }
            if (detail.CommunityInformation is not null)
            {
                if (detail.CommunityInformation.PlayCount.HasValue)
                    sb.AppendLine($"Play: {detail.CommunityInformation.PlayCount}");
                if (detail.CommunityInformation.DanmakuCount.HasValue)
                    sb.AppendLine($"Danmaku: {detail.CommunityInformation.DanmakuCount}");
                if (detail.CommunityInformation.CommentCount.HasValue)
                    sb.AppendLine($"Reply: {detail.CommunityInformation.CommentCount}");
                if (detail.CommunityInformation.FavoriteCount.HasValue)
                    sb.AppendLine($"Favorite: {detail.CommunityInformation.FavoriteCount}");
                if (detail.CommunityInformation.CoinCount.HasValue)
                    sb.AppendLine($"Coin: {detail.CommunityInformation.CoinCount}");
                if (detail.CommunityInformation.ShareCount.HasValue)
                    sb.AppendLine($"Share: {detail.CommunityInformation.ShareCount}");
                if (detail.CommunityInformation.LikeCount.HasValue)
                    sb.AppendLine($"Like: {detail.CommunityInformation.LikeCount}");
            }

            if (detail.PublishTime is not null)
                sb.AppendLine($"Publish Time: {detail.PublishTime}");

            var description = detail.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
            if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine(description.Replace("&amp;", "&"));

            sb.AppendLine().AppendLine($"URL: https://www.bilibili.com/video/{detail.BvId}/");

            return sb.ToString();
        }

        /// <summary>
        ///     Build live info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static string BuildLiveInfo(LivePlayerView detail)
        {
            return BuildLiveInfo(detail.Information);
        }

        /// <summary>
        ///     Build live info.
        /// </summary>
        /// <param name="detail"></param>
        /// <returns></returns>
        public static string BuildLiveInfo(LiveInformation detail)
        {
            var sb = new StringBuilder();
            sb.AppendLine(detail.Identifier.Title);
            sb.AppendLine($"Author: {detail.User.Name}");
            var viewCount = detail.GetExtensionIfNotNull<int>(LiveExtensionDataId.ViewerCount);
            sb.AppendLine($"View Count: {viewCount}");

            var isLiving = detail.GetExtensionIfNotNull<bool>(LiveExtensionDataId.IsLiving);
            sb.AppendLine($"Is Living: {isLiving}");

            if (isLiving)
            {
                var startTime = detail.GetExtensionIfNotNull<DateTimeOffset>(LiveExtensionDataId.StartTime);
                sb.AppendLine($"Start Time: {startTime.ToTimeString()}");
            }

            sb.AppendLine($"URL: https://live.bilibili.com/{detail.Identifier.Id}");
            return sb.ToString();
        }

        /// <summary>
        ///     Build article info.
        /// </summary>
        /// <param name="detail"></param>
        /// <param name="momentInformation"></param>
        /// <returns></returns>
        public static string BuildArticleInfo(ArticleInformation detail, MomentInformation? momentInformation = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(detail.Identifier.Title);

            {
                if (detail.Publisher is not null)
                    sb.AppendLine($"Author: {detail.Publisher.Name}");
                else if (momentInformation?.User is not null) sb.AppendLine($"Author: {momentInformation.User.Name}");
            }

            if (detail.CommunityInformation is not null)
            {
                if (detail.CommunityInformation.ViewCount.HasValue)
                    sb.AppendLine($"View: {detail.CommunityInformation.ViewCount}");
                if (detail.CommunityInformation.CommentCount.HasValue)
                    sb.AppendLine($"Reply: {detail.CommunityInformation.CommentCount}");
                if (detail.CommunityInformation.FavoriteCount.HasValue)
                    sb.AppendLine($"Favorite: {detail.CommunityInformation.FavoriteCount}");
                if (detail.CommunityInformation.CoinCount.HasValue)
                    sb.AppendLine($"Coin: {detail.CommunityInformation.CoinCount}");
                if (detail.CommunityInformation.ShareCount.HasValue)
                    sb.AppendLine($"Share: {detail.CommunityInformation.ShareCount}");
                if (detail.CommunityInformation.LikeCount.HasValue)
                    sb.AppendLine($"Like: {detail.CommunityInformation.LikeCount}");
            }

            var subtitle = detail.GetExtensionIfNotNull<string>(ArticleExtensionDataId.Subtitle);
            if (!string.IsNullOrWhiteSpace(subtitle)) sb.AppendLine($"Subtitle: {subtitle}");

            var wordCount = detail.GetExtensionIfNotNull<int>(ArticleExtensionDataId.WordCount);
            if (wordCount > 0) sb.AppendLine($"Word Count: {wordCount.ToString()}");

            var partition = detail.GetExtensionIfNotNull<string>(ArticleExtensionDataId.Partition);
            if (!string.IsNullOrWhiteSpace(partition)) sb.AppendLine($"Partition: {partition}");

            var relatedPartitions = detail.GetExtensionIfNotNull<string[]>(ArticleExtensionDataId.RelatedPartitions);
            if (relatedPartitions is not null && relatedPartitions.Length > 0)
                sb.AppendLine($"Related Partitions: {string.Join(", ", relatedPartitions)}");

            if (detail.PublishDateTime is not null)
                sb.AppendLine($"Publish Time: {detail.PublishDateTime.Value}");

            if (detail.Identifier.Summary is not null)
                sb.AppendLine(detail.Identifier.Summary);

            sb.AppendLine($"https://www.bilibili.com/read/cv{detail.Identifier.Id}/");
            return sb.ToString();
        }
    }
}