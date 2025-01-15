using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Octokit;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Github
{
    /// <summary>
    ///     GitHub interactions.
    /// </summary>
    [Group("github", "github interactions")]
    public class GithubInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Logger.
        /// </summary>
        public required ILogger<GithubInteractions> Logger { get; set; }

        /// <summary>
        ///     GitHub client provider.
        /// </summary>
        public required GitHubClientProviderService GitHubClientProvider { get; set; }

        /// <summary>
        ///     Login to GitHub.
        /// </summary>
        [RequireOwner]
        [SlashCommand("login", "Login to GitHub.")]
        public async Task LoginAsync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            if (await CheckLoginAsync())
            {
                var account = await GitHubClientProvider.Client.User.Current().ConfigureAwait(false);
                await FollowupAsync(embed: BuildUserInfoEmbed(account).Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                Logger.LogInformation("Requesting GitHub login.");
                var deviceFlowResponse = await GitHubClientProvider.GetDeviceFlowResponseAsync().ConfigureAwait(false);
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("GitHub Login")
                    .WithColor(Color.Orange)
                    .WithDescription("Please visit the following URL and enter the code.")
                    .WithUrl(deviceFlowResponse.VerificationUri)
                    .AddField("Verification URL", deviceFlowResponse.VerificationUri)
                    .AddField("Code", deviceFlowResponse.UserCode, true)
                    .AddField("Expires In", TimeSpan.FromSeconds(deviceFlowResponse.ExpiresIn).ToString(@"hh\:mm\:ss"),
                        true);
                await FollowupAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
                Logger.LogInformation("Waiting for GitHub login token.");
                var token = await GitHubClientProvider.WaitForTokenAsync(deviceFlowResponse).ConfigureAwait(false);
                await GitHubClientProvider.SetCredentials(token.AccessToken).ConfigureAwait(false);
                var account = await GitHubClientProvider.Client.User.Current().ConfigureAwait(false);
                Logger.LogInformation("Successfully logged in to GitHub as {Account}.", account.Login);
                await ModifyOriginalResponseAsync(x =>
                    {
                        x.Embed = BuildUserInfoEmbed(account)
                            .WithTitle("GitHub User")
                            .WithColor(Color.Green)
                            .Build();
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to login to GitHub.");
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = new EmbedBuilder()
                        .WithTitle("GitHub Login")
                        .WithColor(Color.Red)
                        .WithDescription("Failed to login to GitHub.")
                        .AddField("Exception Message", ex.Message)
                        .Build();
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Get user information.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        [SlashCommand("user", "Get user information.")]
        public async Task GetUserAsync(string username)
        {
            await DeferAsync(true).ConfigureAwait(false);
            try
            {
                var account = await GitHubClientProvider.Client.User.Get(username).ConfigureAwait(false);
                await FollowupAsync(embed: BuildUserInfoEmbed(account)
                    .WithTitle("GitHub User")
                    .WithColor(Color.Green).Build()).ConfigureAwait(false);
            }
            catch (NotFoundException)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("GitHub User")
                    .WithColor(Color.Red)
                    .WithDescription("User not found.")
                    .AddField("Username", username);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        private async Task<bool> CheckLoginAsync()
        {
            try
            {
                await GitHubClientProvider.Client.User.Current().ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static EmbedBuilder BuildUserInfoEmbed(User account)
        {
            var embed = new EmbedBuilder();

            // embed.WithTitle(account.Login);
            embed.AddField("Name", account.Name, true);
            embed.AddField("Followers", account.Followers, true);
            embed.AddField("Following", account.Following, true);
            embed.AddField("Email", account.Email, true);
            embed.AddField("Created At", account.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"), true);
            embed.WithUrl(account.HtmlUrl);
            embed.WithThumbnailUrl(account.AvatarUrl);

            var footer = new EmbedFooterBuilder();
            footer.WithText("GitHub");
            footer.WithIconUrl("https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png");
            embed.WithFooter(footer);

            return embed;
        }
    }
}