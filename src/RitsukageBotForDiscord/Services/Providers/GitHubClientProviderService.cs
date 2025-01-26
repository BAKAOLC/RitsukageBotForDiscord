using Microsoft.Extensions.Configuration;
using Octokit;
using Octokit.Caching;
using RitsukageBot.Library.Data;
using RitsukageBot.Options;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     GitHub client provider service.
    /// </summary>
    public class GitHubClientProviderService
    {
        private readonly Lazy<GitHubClient> _client;

        /// <summary>
        ///     Initialize a new instance of <see cref="GitHubClientProviderService" />.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="databaseProviderService"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public GitHubClientProviderService(IConfiguration configuration,
            DatabaseProviderService databaseProviderService)
        {
            var option = configuration.GetSection("GitHub").Get<GitHubOption>();
            if (option is null || string.IsNullOrEmpty(option.ProductHeader))
                throw new InvalidOperationException("GitHub configuration is not set.");

            Option = option;
            DatabaseProviderService = databaseProviderService;
            _client = new(() =>
            {
                var client = new GitHubClient(new ProductHeaderValue(Option.ProductHeader));
                try
                {
                    // ReSharper disable once AsyncApostle.AsyncWait
                    var account = databaseProviderService.GetAsync<GitHubAccountConfiguration>(0).Result;
                    client.Credentials = new(account.AccessToken);
                }
                catch
                {
                    // ignored
                }

                return client;
            });
        }

        private GitHubOption Option { get; }

        private DatabaseProviderService DatabaseProviderService { get; }

        /// <summary>
        ///     GitHub client.
        /// </summary>
        public GitHubClient Client => _client.Value;

        /// <inheritdoc cref="GitHubClient.Credentials" />
        public Credentials Credentials
        {
            get => Client.Credentials;
            set => Client.Credentials = value;
        }

        /// <inheritdoc cref="GitHubClient.ResponseCache" />
        public IResponseCache ResponseCache
        {
            set => Client.ResponseCache = value;
        }

        /// <inheritdoc cref="GitHubClient.BaseAddress" />
        public Uri BaseAddress => Client.BaseAddress;

        /// <inheritdoc cref="GitHubClient.Connection" />
        public IConnection Connection => Client.Connection;

        /// <inheritdoc cref="GitHubClient.Authorization" />
        public IAuthorizationsClient Authorization => Client.Authorization;

        /// <inheritdoc cref="GitHubClient.Activity" />
        public IActivitiesClient Activity => Client.Activity;

        /// <inheritdoc cref="GitHubClient.Emojis" />
        public IEmojisClient Emojis => Client.Emojis;

        /// <inheritdoc cref="GitHubClient.Issue" />
        public IIssuesClient Issue => Client.Issue;

        /// <inheritdoc cref="GitHubClient.Migration" />
        public IMigrationClient Migration => Client.Migration;

        /// <inheritdoc cref="GitHubClient.Oauth" />
        public IOauthClient Oauth => Client.Oauth;

        /// <inheritdoc cref="GitHubClient.Organization" />
        public IOrganizationsClient Organization => Client.Organization;

        /// <inheritdoc cref="GitHubClient.Packages" />
        public IPackagesClient Packages => Client.Packages;

        /// <inheritdoc cref="GitHubClient.PullRequest" />
        public IPullRequestsClient PullRequest => Client.PullRequest;

        /// <inheritdoc cref="GitHubClient.Repository" />
        public IRepositoriesClient Repository => Client.Repository;

        /// <inheritdoc cref="GitHubClient.Gist" />
        public IGistsClient Gist => Client.Gist;

        /// <inheritdoc cref="GitHubClient.User" />
        public IUsersClient User => Client.User;

        /// <inheritdoc cref="GitHubClient.Git" />
        public IGitDatabaseClient Git => Client.Git;

        /// <inheritdoc cref="GitHubClient.GitHubApps" />
        public IGitHubAppsClient GitHubApps => Client.GitHubApps;

        /// <inheritdoc cref="GitHubClient.Search" />
        public ISearchClient Search => Client.Search;

        /// <inheritdoc cref="GitHubClient.Enterprise" />
        public IEnterpriseClient Enterprise => Client.Enterprise;

        /// <inheritdoc cref="GitHubClient.Reaction" />
        public IReactionsClient Reaction => Client.Reaction;

        /// <inheritdoc cref="GitHubClient.Check" />
        public IChecksClient Check => Client.Check;

        /// <inheritdoc cref="GitHubClient.Meta" />
        public IMetaClient Meta => Client.Meta;

        /// <inheritdoc cref="GitHubClient.RateLimit" />
        public IRateLimitClient RateLimit => Client.RateLimit;

        /// <inheritdoc cref="GitHubClient.Licenses" />
        public ILicensesClient Licenses => Client.Licenses;

        /// <inheritdoc cref="GitHubClient.GitIgnore" />
        public IGitIgnoreClient GitIgnore => Client.GitIgnore;

        /// <inheritdoc cref="GitHubClient.Markdown" />
        public IMarkdownClient Markdown => Client.Markdown;

        /// <inheritdoc cref="GitHubClient.Actions" />
        public IActionsClient Actions => Client.Actions;

        /// <inheritdoc cref="GitHubClient.Codespaces" />
        public ICodespacesClient Codespaces => Client.Codespaces;

        /// <inheritdoc cref="GitHubClient.Copilot" />
        public ICopilotClient Copilot => Client.Copilot;

        /// <inheritdoc cref="GitHubClient.DependencyGraph" />
        public IDependencyGraphClient DependencyGraph => Client.DependencyGraph;

        /// <inheritdoc cref="GitHubClient.SetRequestTimeout" />
        public void SetRequestTimeout(TimeSpan timeout)
        {
            Client.SetRequestTimeout(timeout);
        }

        /// <inheritdoc cref="GitHubClient.GetLastApiInfo" />
        public ApiInfo GetLastApiInfo()
        {
            return Client.GetLastApiInfo();
        }

        /// <summary>
        ///     Get device flow response.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<OauthDeviceFlowResponse> GetDeviceFlowResponseAsync()
        {
            if (string.IsNullOrEmpty(Option.AppClientId))
                throw new InvalidOperationException("GitHub App Client ID is not set.");

            return await Oauth.InitiateDeviceFlow(new(Option.AppClientId)).ConfigureAwait(false) ??
                   throw new InvalidOperationException("Failed to get device flow response.");
        }

        /// <summary>
        ///     Wait for token.
        /// </summary>
        /// <param name="deviceFlowResponse"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<OauthToken> WaitForTokenAsync(OauthDeviceFlowResponse deviceFlowResponse)
        {
            return await Oauth.CreateAccessTokenForDeviceFlow(Option.AppClientId, deviceFlowResponse)
                .ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to get OAuth token.");
        }

        /// <summary>
        ///     Set credentials.
        /// </summary>
        /// <param name="token"></param>
        public async Task SetCredentialsAsync(string token)
        {
            Credentials = new(token);
            var account = new GitHubAccountConfiguration
            {
                Id = 0,
                AccessToken = token,
            };
            await DatabaseProviderService.InsertOrReplaceAsync(account).ConfigureAwait(false);
        }
    }
}