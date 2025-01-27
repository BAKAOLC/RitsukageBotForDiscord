using System.Text;
using Richasy.BiliKernel.Models.User;

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

            if (detail.Profile.User.Avatar is not null) builder.AppendLine($"Avatar: {detail.Profile.User.Avatar}");
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
    }
}