using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class UserInteractions
        {
            private static readonly Regex MatchUserIdRegex = GetMatchUserIdRegex();

            /// <summary>
            ///     Get user information
            /// </summary>
            /// <param name="id"></param>
            [SlashCommand("info", "Get user information")]
            public async Task GetUserInfoAsync(string id)
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
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                        .WithDescription("Invalid video id.").Build()).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var detail = await UserService.GetUserInformationAsync(userId.ToString()).ConfigureAwait(false);
                    var embed = InformationEmbedBuilder.BuildUserInfo(detail);
                    embed.WithColor(Color.Green);
                    var footerBuilder = new EmbedFooterBuilder();
                    footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
                    footerBuilder.WithText("Bilibili");
                    embed.WithFooter(footerBuilder);
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(), "bilibili-icon.png",
                        embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get user information.");
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Failed to get user information: " + ex.Message).Build())
                        .ConfigureAwait(false);
                }
            }

            [GeneratedRegex(@"((https?://)?space\.bilibili\.com/)(?<id>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)]
            private static partial Regex GetMatchUserIdRegex();
        }
    }

    public partial class BilibiliInteractionButton
    {
        public partial class UserInteractionsButton
        {
        }
    }
}