using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Bilibili.Utils;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Enums.Bilibili;

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
            public async Task GetLiveInfoAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchLiveIdRegex.IsMatch(id))
                {
                    var match = MatchLiveIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var roomId) || roomId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid live id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var media = new MediaIdentifier(roomId.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetLivePageDetailAsync(media).ConfigureAwait(false);
                    var embed = InformationEmbedBuilder.BuildLiveInfo(detail);
                    embed.WithColor(Color.Green);
                    embed.WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get live information");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get live information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            /// <summary>
            ///     Follow a live
            /// </summary>
            /// <param name="id"></param>
            [RequireUserPermission(GuildPermission.Administrator
                                   | GuildPermission.ManageGuild
                                   | GuildPermission.ManageChannels)]
            [RequireUserPermission(ChannelPermission.ManageChannels)]
            [SlashCommand("follow", "Follow a live")]
            public async Task FollowLiveAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchLiveIdRegex.IsMatch(id))
                {
                    var match = MatchLiveIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var roomId) || roomId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid live id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var roomIdStr = roomId.ToString();
                var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
                var config = await table.FirstOrDefaultAsync(x => x.Type == WatcherType.Live
                                                                  && x.Target == roomIdStr
                                                                  && x.ChannelId == Context.Channel.Id)
                    .ConfigureAwait(false);
                if (config is not null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("This live is already followed.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }


                var media = new MediaIdentifier(roomId.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetLivePageDetailAsync(media).ConfigureAwait(false);
                    var user = await UserService.GetUserInformationAsync(detail.Information.User.Id)
                        .ConfigureAwait(false);
                    if (user.Profile is null || user.Community is null)
                    {
                        var errorEmbed = new EmbedBuilder()
                            .WithColor(Color.Red)
                            .WithTitle("Error")
                            .WithDescription("Failed to get user information.")
                            .WithBilibiliLogoIconFooter();
                        await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                            BilibiliIconData.LogoIconFileName,
                            embed: errorEmbed.Build()).ConfigureAwait(false);
                        return;
                    }

                    var userRelation = user.Community.Relation;
                    Logger.LogDebug("User {UserId} is {Relation}", user.Profile.User.Id, userRelation);
                    if (userRelation != UserRelationStatus.Following &&
                        userRelation != UserRelationStatus.Friends &&
                        userRelation != UserRelationStatus.SpeciallyFollowed)
                    {
                        Logger.LogDebug("Follow user {UserId}", detail.Information.User.Id);
                        await RelationshipService.FollowUserAsync(detail.Information.User.Id).ConfigureAwait(false);
                    }

                    config = new()
                    {
                        Type = WatcherType.Live,
                        Target = roomIdStr,
                        ChannelId = Context.Channel.Id,
                    };

                    await DatabaseProviderService.InsertAsync(config).ConfigureAwait(false);

                    const string text =
                        "This live is now followed. You will receive notifications when the live starts.";
                    var embed = InformationEmbedBuilder.BuildLiveInfo(detail).WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        text, embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get live information");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get live information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            /// <summary>
            ///     Unfollow a live
            /// </summary>
            /// <param name="id"></param>
            [RequireUserPermission(GuildPermission.Administrator
                                   | GuildPermission.ManageGuild
                                   | GuildPermission.ManageChannels)]
            [RequireUserPermission(ChannelPermission.ManageChannels)]
            [SlashCommand("unfollow", "Unfollow a live")]
            public async Task UnfollowLiveAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                id = id.Trim();

                if (MatchLiveIdRegex.IsMatch(id))
                {
                    var match = MatchLiveIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out var roomId) || roomId == 0)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Invalid live id.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var roomIdStr = roomId.ToString();
                var table = DatabaseProviderService.Table<BilibiliWatcherConfiguration>();
                var config = await table.FirstOrDefaultAsync(x => x.Type == WatcherType.Live
                                                                  && x.Target == roomIdStr
                                                                  && x.ChannelId == Context.Channel.Id)
                    .ConfigureAwait(false);
                if (config is null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("This live is not followed.")
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                var media = new MediaIdentifier(roomId.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetLivePageDetailAsync(media).ConfigureAwait(false);
                    await DatabaseProviderService.DeleteAsync(config).ConfigureAwait(false);

                    const string text =
                        "This live is now unfollowed. You will no longer receive notifications when the live starts.";
                    var embed = InformationEmbedBuilder.BuildLiveInfo(detail).WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        text, embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get live information");
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription("Failed to get live information: " + ex.Message)
                        .WithBilibiliLogoIconFooter();
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }

            [GeneratedRegex(@"((https?://)?live\.bilibili\.com/)(?<id>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)]
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