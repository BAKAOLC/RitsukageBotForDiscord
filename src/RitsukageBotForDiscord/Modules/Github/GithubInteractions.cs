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
            if (await CheckLoginAsync().ConfigureAwait(false))
            {
                var account = await GitHubClientProvider.User.Current().ConfigureAwait(false);
                await FollowupAsync(embed: BuildUserInfoEmbed(account).Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                Logger.LogDebug("Requesting GitHub login");
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
                Logger.LogDebug("Waiting for GitHub login token");
                var token = await GitHubClientProvider.WaitForTokenAsync(deviceFlowResponse).ConfigureAwait(false);
                await GitHubClientProvider.SetCredentialsAsync(token.AccessToken).ConfigureAwait(false);
                var account = await GitHubClientProvider.User.Current().ConfigureAwait(false);
                Logger.LogDebug("Successfully logged in to GitHub as {Account}", account.Login);
                await ModifyOriginalResponseAsync(x =>
                    {
                        x.Embed = BuildUserInfoEmbed(account)
                            .WithColor(Color.Green)
                            .Build();
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to login to GitHub");
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
            await DeferAsync().ConfigureAwait(false);
            User? account;
            try
            {
                Logger.LogDebug("Searching for user {Username}", username);
                var result = await GitHubClientProvider.Search.SearchUsers(new(username)).ConfigureAwait(false);
                if (result.Items.Count == 0)
                {
                    Logger.LogDebug("User {Username} not found", username);
                    account = null;
                }
                else
                {
                    Logger.LogDebug("Search result: {Result}", result.Items.Count);
                    foreach (var item in result.Items)
                        Logger.LogDebug("User: {Login}, Followers: {Followers}, Following: {Following}",
                            item.Login, item.Name, item.Followers);
                    account = result.Items.ToArray().FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to search user");
                var embed = new EmbedBuilder()
                    .WithTitle("GitHub User")
                    .WithColor(Color.Red)
                    .WithDescription("Failed to search user.")
                    .AddField("Exception Message", ex.Message);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            if (account is null)
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithDescription("User not found.")
                    .AddField("Username", username);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            try
            {
                Logger.LogDebug("Getting user information for {Login}", account.Login);
                account = await GitHubClientProvider.User.Get(account.Login).ConfigureAwait(false);
                Logger.LogDebug(
                    "User: {Login}, Name: {Name}, Followers: {Followers}, Following: {Following}, Email: {Email}, Created At: {CreatedAt}, URL: {Url}, Avatar URL: {AvatarUrl}",
                    account.Login, account.Name, account.Followers, account.Following, account.Email, account.CreatedAt,
                    account.HtmlUrl, account.AvatarUrl);
                await FollowupAsync(embed: BuildUserInfoEmbed(account).WithColor(Color.Green).Build())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to get user information");
                var embed = new EmbedBuilder()
                    .WithTitle("GitHub User")
                    .WithColor(Color.Red)
                    .WithDescription("Failed to get user information.")
                    .AddField("Exception Message", ex.Message);
                await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            }
        }

        private async Task<bool> CheckLoginAsync()
        {
            try
            {
                await GitHubClientProvider.User.Current().ConfigureAwait(false);
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

            embed.WithTitle(account.Login);
            if (!string.IsNullOrWhiteSpace(account.Name))
                embed.AddField("Name", account.Name, true);
            embed.AddField("Followers", account.Followers, true);
            embed.AddField("Following", account.Following, true);
            if (!string.IsNullOrWhiteSpace(account.Email))
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