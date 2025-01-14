using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;

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
            public async Task GetVideoInfoAsync(string id)
            {
                await DeferAsync().ConfigureAwait(false);

                ulong roomId;

                id = id.Trim();

                if (MatchLiveIdRegex.IsMatch(id))
                {
                    var match = MatchLiveIdRegex.Match(id);
                    id = match.Groups["id"].Value;
                }

                if (!ulong.TryParse(id, out roomId) || roomId == 0)
                {
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                        .WithDescription("Invalid video id.").Build()).ConfigureAwait(false);
                    return;
                }

                var media = new MediaIdentifier(roomId.ToString(), null, null);
                try
                {
                    var detail = await PlayerService.GetLivePageDetailAsync(media).ConfigureAwait(false);
                    var embed = InformationEmbedBuilder.BuildLiveInfo(detail);
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
                    Logger.LogError(ex, "Failed to get live information.");
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Failed to get live information: " + ex.Message).Build())
                        .ConfigureAwait(false);
                }
            }

            [GeneratedRegex(@"((https?://)?live\.bilibili\.com/)(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
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