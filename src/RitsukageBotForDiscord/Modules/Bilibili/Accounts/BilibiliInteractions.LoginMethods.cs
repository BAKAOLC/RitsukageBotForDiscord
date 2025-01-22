using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Authorizers;
using RitsukageBot.Library.Bilibili.DiscordBridges;
using RitsukageBot.Library.Bilibili.Utils;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        public partial class AccountInteractions
        {
            /// <summary>
            ///     Login
            /// </summary>
            [RequireOwner] // Only the owner can use this command
            [SlashCommand("login", "Login to Bilibili")]
            public async Task LoginAsync()
            {
                await DeferAsync(true).ConfigureAwait(false);
                var isLogin = await VerifyBotLoginAsync().ConfigureAwait(false);
                if (isLogin)
                {
                    var embedBuilder = await GetBotLoginInfoAsync().ConfigureAwait(false);
                    var componentBuilder = new ComponentBuilder();
                    componentBuilder.WithButton(new ButtonBuilder()
                        .WithCustomId($"{TagCustomId}:account:bot:logout")
                        .WithLabel("Logout")
                        .WithStyle(ButtonStyle.Danger));
                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                            BilibiliIconData.LogoIconFileName,
                            embed: embedBuilder.WithBilibiliLogoIconFooter().Build(),
                            components: componentBuilder.Build())
                        .ConfigureAwait(false);
                    return;
                }

                await RequestBotLoginAsync().ConfigureAwait(false);
            }

            private async Task<bool> VerifyBotLoginAsync()
            {
                var tokenResolver = BiliKernelProvider.GetRequiredService<IAuthenticationService>();
                try
                {
                    await tokenResolver.EnsureTokenAsync().ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    // ignored
                }

                return false;
            }

            private async Task<EmbedBuilder> GetBotLoginInfoAsync()
            {
                var profileService = BiliKernelProvider.GetRequiredService<IMyProfileService>();
                var myInfo = await profileService.GetMyProfileAsync().ConfigureAwait(false);
                var myCommunityInfo = await profileService.GetMyCommunityInformationAsync().ConfigureAwait(false);

                var embed = InformationEmbedBuilder.BuildMyInfo(myInfo, myCommunityInfo);
                embed.WithColor(Color.Green);
                return embed;
            }

            private async Task RequestBotLoginAsync()
            {
                try
                {
                    Logger.LogDebug("Requesting Bilibili login.");
                    var tokenResolver = BiliKernelProvider.GetRequiredService<IAuthenticationService>();
                    if (tokenResolver is not TvAuthenticationService service)
                        throw new InvalidOperationException(
                            "The authentication service is not a modified version of the TV authentication service.");
                    await service.SignInAsync().ConfigureAwait(false);
                    Logger.LogDebug("Generating QR code.");
                    var qrCode = service.GetQrCode() ?? throw new InvalidOperationException("The QR code is null.");
                    var qrCodeImage = service.GetQrCodeImage() ??
                        throw new InvalidOperationException("The QR code image is null.");
                    var embed = new EmbedBuilder();
                    embed.WithColor(Color.Orange);
                    embed.WithTitle("Bilibili Login");
                    embed.WithDescription("Please scan the QR code to login.");
                    embed.WithImageUrl("attachment://qr_code.png");
                    embed.WithBilibiliLogoIconFooter();
                    using var qrCodeStream = new MemoryStream(qrCodeImage);
                    await using var logoStream = BilibiliIconData.GetLogoIconStream();
                    await FollowupWithFilesAsync([
                            new(qrCodeStream, "qr_code.png"),
                            new(logoStream, BilibiliIconData.LogoIconFileName),
                        ],
                        embed: embed.Build()).ConfigureAwait(false);
                    Logger.LogDebug("Waiting for Bilibili login.");
                    await service.WaitQrCodeScanAsync(qrCode).ConfigureAwait(false);
                    if (await VerifyBotLoginAsync().ConfigureAwait(false))
                    {
                        var embedBuilder = await GetBotLoginInfoAsync().ConfigureAwait(false);
                        var componentBuilder = new ComponentBuilder();
                        componentBuilder.WithButton(new ButtonBuilder()
                            .WithCustomId($"{TagCustomId}:account:bot:logout").WithLabel("Logout")
                            .WithStyle(ButtonStyle.Danger));
                        await ModifyOriginalResponseAsync(x =>
                        {
                            x.Attachments = null;
                            x.Embeds = new[] { embedBuilder.WithBilibiliLogoIconFooter().Build() };
                            x.Components = componentBuilder.Build();
                        }).ConfigureAwait(false);
                        return;
                    }

                    embed = new();
                    embed.WithColor(Color.Red);
                    embed.WithTitle("Bilibili Login");
                    embed.WithDescription("Login failed.");
                    embed.WithBilibiliLogoIconFooter();
                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Attachments = null;
                        x.Embeds = new[] { embed.Build() };
                        x.Components = null;
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error")
                        .WithDescription(ex.Message)
                        .WithBilibiliLogoIconFooter();

                    await FollowupWithFileAsync(BilibiliIconData.GetLogoIconStream(),
                        BilibiliIconData.LogoIconFileName,
                        embed: errorEmbed.Build()).ConfigureAwait(false);
                }
            }
        }
    }

    public partial class BilibiliInteractionButton
    {
        public partial class AccountInteractionsButton
        {
            /// <summary>
            ///     Logout
            /// </summary>
            [ComponentInteraction($"{BilibiliInteractions.TagCustomId}:account:bot:logout")]
            public async Task LogoutAsync()
            {
                var tokenResolver = BiliKernelProvider.GetRequiredService<IAuthenticationService>();
                await tokenResolver.SignOutAsync().ConfigureAwait(false);
                await Context.Interaction.UpdateAsync(x =>
                {
                    x.Embeds = new[]
                    {
                        new EmbedBuilder()
                            .WithColor(Color.Green)
                            .WithTitle("Logout")
                            .WithDescription("Logout successfully.")
                            .WithBilibiliLogoIconFooter()
                            .Build(),
                    };
                    x.Components = null;
                }).ConfigureAwait(false);
            }
        }
    }
}