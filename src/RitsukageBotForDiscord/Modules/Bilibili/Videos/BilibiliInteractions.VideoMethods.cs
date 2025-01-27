using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Models.Media;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Library.Bilibili.Utils;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class VideoInteractions
        {
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
                try
                {
                    var detail = await PlayerService.GetVideoPageDetailAsync(media).ConfigureAwait(false);
                    var embed = InformationEmbedBuilder.BuildVideoInfo(detail);
                    embed.WithColor(Color.Green);
                    var footerBuilder = new EmbedFooterBuilder();
                    footerBuilder.WithIconUrl("attachment://bilibili-icon.png");
                    footerBuilder.WithText("Bilibili");
                    embed.WithFooter(footerBuilder);
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: embed.Build()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get video information");
                    await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error")
                            .WithDescription("Failed to get video information: " + ex.Message).Build())
                        .ConfigureAwait(false);
                }
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