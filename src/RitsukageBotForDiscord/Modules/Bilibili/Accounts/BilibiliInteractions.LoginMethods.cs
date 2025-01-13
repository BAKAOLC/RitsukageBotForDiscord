using System.Globalization;
using Discord;
using Discord.Interactions;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Authorizers;

namespace RitsukageBot.Modules.Bilibili
{
    public partial class BilibiliInteractions
    {
        /// <summary>
        ///     Login
        /// </summary>
        [RequireOwner] // Only the owner can use this command
        [SlashCommand("login", "Login to Bilibili")]
        public async Task LoginAsync()
        {
            await DeferAsync(true);
            var isLogin = await VerifyBotLoginAsync().ConfigureAwait(false);
            if (isLogin)
            {
                var embedBuilder = await GetBotLoginInfoAsync().ConfigureAwait(false);
                var componentBuilder = new ComponentBuilder();
                componentBuilder.WithButton(new ButtonBuilder().WithCustomId($"{TagCustomId}:bot:logout").WithLabel("Logout").WithStyle(ButtonStyle.Danger));
                await FollowupAsync(embed: embedBuilder.Build(), components: componentBuilder.Build()).ConfigureAwait(false);
                return;
            }

            await RequestBotLoginAsync().ConfigureAwait(false);
        }

        private async Task<bool> VerifyBotLoginAsync()
        {
            var tokenResolver = Kernel.GetRequiredService<IAuthenticationService>();
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
            var profileService = Kernel.GetRequiredService<IMyProfileService>();
            var myInfo = await profileService.GetMyProfileAsync().ConfigureAwait(false);
            var myCommunityInfo = await profileService.GetMyCommunityInformationAsync().ConfigureAwait(false);

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Green);
            embed.WithTitle("Bilibili Login Info");
            if (myInfo.User.Avatar is not null) embed.WithThumbnailUrl(myInfo.User.Avatar.SourceUri.ToString());
            embed.AddField("Username", myInfo.User.Name);
            embed.AddField("UID", myInfo.User.Id);
            if (myInfo.Level.HasValue) embed.AddField("Level", myInfo.Level);
            embed.AddField("Is Hardcore", myInfo.IsHardcore ?? false);
            embed.AddField("Is Vip", myInfo.IsVip ?? false);
            if (!string.IsNullOrWhiteSpace(myInfo.Introduce))
                embed.WithDescription(myInfo.Introduce);

            embed.AddField("Coins", myCommunityInfo.CoinCount.HasValue ? myCommunityInfo.CoinCount.Value.ToString(CultureInfo.CurrentCulture) : "Unknown");
            embed.AddField("Follows", myCommunityInfo.FollowCount.HasValue ? myCommunityInfo.FollowCount.Value.ToString(CultureInfo.CurrentCulture) : "Unknown");
            embed.AddField("Fans", myCommunityInfo.FansCount.HasValue ? myCommunityInfo.FansCount.Value.ToString(CultureInfo.CurrentCulture) : "Unknown");
            embed.AddField("Moments", myCommunityInfo.MomentCount.HasValue ? myCommunityInfo.MomentCount.Value.ToString(CultureInfo.CurrentCulture) : "Unknown");

            return embed;
        }

        private async Task RequestBotLoginAsync()
        {
            try
            {
                var tokenResolver = Kernel.GetRequiredService<IAuthenticationService>();
                if (tokenResolver is not TvAuthenticationService service)
                    throw new InvalidOperationException("The authentication service is not a modified version of the TV authentication service.");
                await service.SignInAsync();
                var qrCode = service.GetQrCode() ?? throw new InvalidOperationException("The QR code is null.");
                var qrCodeImage = service.GetQrCodeImage() ?? throw new InvalidOperationException("The QR code image is null.");
                var embed = new EmbedBuilder();
                embed.WithColor(Color.Orange);
                embed.WithTitle("Bilibili Login");
                embed.WithDescription("Please scan the QR code to login.");
                embed.WithImageUrl("attachment://qr_code.png");
                await FollowupWithFileAsync(new MemoryStream(qrCodeImage), "qr_code.png", embed: embed.Build()).ConfigureAwait(false);
                await service.WaitQrCodeScanAsync(qrCode).ConfigureAwait(false);
                if (await VerifyBotLoginAsync().ConfigureAwait(false))
                {
                    var embedBuilder = await GetBotLoginInfoAsync().ConfigureAwait(false);
                    var componentBuilder = new ComponentBuilder();
                    componentBuilder.WithButton(new ButtonBuilder().WithCustomId($"{TagCustomId}:bot:logout").WithLabel("Logout").WithStyle(ButtonStyle.Danger));
                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Attachments = null;
                        x.Embeds = new[] { embedBuilder.Build() };
                        x.Components = componentBuilder.Build();
                    }).ConfigureAwait(false);
                    return;
                }

                embed = new();
                embed.WithColor(Color.Red);
                embed.WithTitle("Bilibili Login");
                embed.WithDescription("Login failed.");
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Attachments = null;
                    x.Embeds = new[] { embed.Build() };
                    x.Components = null;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: new EmbedBuilder().WithColor(Color.Red).WithTitle("Error").WithDescription(ex.Message).Build()).ConfigureAwait(false);
            }
        }
    }

    public partial class BilibiliInteractionButton
    {
        /// <summary>
        ///     Logout
        /// </summary>
        [ComponentInteraction($"{BilibiliInteractions.TagCustomId}:bot:logout")]
        public async Task LogoutAsync()
        {
            var tokenResolver = Kernel.GetRequiredService<IAuthenticationService>();
            await tokenResolver.SignOutAsync().ConfigureAwait(false);
            await Context.Interaction.UpdateAsync(x =>
            {
                x.Embeds = new[] { new EmbedBuilder().WithColor(Color.Green).WithTitle("Logout").WithDescription("Logout successfully.").Build() };
                x.Components = null;
            });
        }
    }
}