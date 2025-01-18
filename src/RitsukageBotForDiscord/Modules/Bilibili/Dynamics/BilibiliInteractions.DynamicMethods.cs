using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class DynamicInteractions
        {
            private static readonly Regex MatchUserIdRegex = GetMatchUserIdRegex();
            private static readonly Regex MatchDynamicIdRegex = GetMatchDynamicIdRegex();

            /// <summary>
            ///     Get dynamic information
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("info", "Get dynamic information")]
            public async Task GetDynamicInfoAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchDynamicIdRegex.IsMatch(id))
                {
                    var match = MatchDynamicIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var dynamicId) || dynamicId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid dynamic id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }

                try
                {
                    var detail = await MomentService.GetMomentInformation(id).ConfigureAwait(false);
                    var embeds = InformationEmbedBuilder.BuildMomentInfo(detail);
                    embeds[^1].WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embeds: embeds.Select(x => x.Build()).ToArray()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get dynamic information.");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get dynamic information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            /// <summary>
            ///     Follow a user
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("follow", "Follow a user")]
            public async Task FollowDynamicAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchUserIdRegex.IsMatch(id))
                {
                    var match = MatchUserIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var userId) || userId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid user id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var userIdStr = userId.ToString();
                var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
                var config = await table.FirstOrDefaultAsync(x => x.Type == WatcherType.Dynamic
                    && x.Target == userIdStr
                    && x.ChannelId == Context.Channel.Id);
                if (config is not null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("This user is already followed.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var detail = await UserService.GetUserInformationAsync(userId.ToString()).ConfigureAwait(false);
                    if (detail.Profile is null || detail.Community is null)
                    {
                        var errorEmbed = new EmbedBuilder()
                            .WithColor(Color.Red)
                            .WithTitle("Error")
                            .WithDescription("Failed to get user information.")
                            .WithBilibiliLogoIconFooter();
                        await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                            embed: errorEmbed.Build()).ConfigureAwait(false);
                        return;
                    }

                    var userRelation = detail.Community.Relation;
                    Logger.LogInformation("User {UserId} is {Relation}", detail.Profile.User.Id, userRelation);
                    if (userRelation != UserRelationStatus.Following &&
                        userRelation != UserRelationStatus.Friends &&
                        userRelation != UserRelationStatus.SpeciallyFollowed)
                    {
                        Logger.LogInformation("Follow user {UserId}", detail.Profile.User.Id);
                        await RelationshipService.FollowUserAsync(detail.Profile.User.Id).ConfigureAwait(false);
                    }


                    config = new()
                    {
                        Type = WatcherType.Dynamic,
                        Target = userIdStr,
                        ChannelId = Context.Channel.Id,
                    };

                    await DatabaseProviderService.InsertAsync(config).ConfigureAwait(false);

                    const string text = "This user is now followed. You will receive notifications when they post a new dynamic.";
                    var embed = InformationEmbedBuilder.BuildUserInfo(detail).WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        text, embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get user information.");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get user information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            /// <summary>
            ///     Unfollow a live
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("unfollow", "Unfollow a live")]
            public async Task UnfollowLiveAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchUserIdRegex.IsMatch(id))
                {
                    var match = MatchUserIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var userId) || userId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid user id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var userIdStr = userId.ToString();
                var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
                var config = await table.FirstOrDefaultAsync(x => x.Type == WatcherType.Dynamic
                    && x.Target == userIdStr
                    && x.ChannelId == Context.Channel.Id);
                if (config is null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("This user is not followed.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }


                try
                {
                    var detail = await UserService.GetUserInformationAsync(userId.ToString()).ConfigureAwait(false);
                    await DatabaseProviderService.DeleteAsync(config).ConfigureAwait(false);

                    const string text = "This user is now unfollowed. You will no longer receive notifications when they post a new dynamic.";
                    var embed = InformationEmbedBuilder.BuildUserInfo(detail).WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        text, embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get user information.");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get user information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), BilibiliIconData.TagLogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            [GeneratedRegex(@"((https?://)?space\.bilibili\.com/)(?<id>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)]
            private static partial Regex GetMatchUserIdRegex();

            [GeneratedRegex(@"((https?://)?(www\.bilibili\.com/opus/)|(t\.bilibili\.com/))(?<id>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)]
            private static partial Regex GetMatchDynamicIdRegex();
        }
    }

    public partial class BilibiliInteractionButton
    {
        public partial class DynamicInteractionsButton
        {
        }
    }
}