using Microsoft.Extensions.Configuration;
using Octokit;
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
        public GitHubClientProviderService(IConfiguration configuration, DatabaseProviderService databaseProviderService)
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

        /// <summary>
        ///     Get device flow response.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<OauthDeviceFlowResponse> GetDeviceFlowResponseAsync()
        {
            if (string.IsNullOrEmpty(Option.AppClientId))
                throw new InvalidOperationException("GitHub App Client ID is not set.");

            return await Client.Oauth.InitiateDeviceFlow(new(Option.AppClientId)).ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to get device flow response.");
        }

        /// <summary>
        ///     Wait for token.
        /// </summary>
        /// <param name="deviceFlowResponse"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<OauthToken> WaitForTokenAsync(OauthDeviceFlowResponse deviceFlowResponse)
        {
            return await Client.Oauth.CreateAccessTokenForDeviceFlow(Option.AppClientId, deviceFlowResponse).ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to get OAuth token.");
        }

        /// <summary>
        ///     Set credentials.
        /// </summary>
        /// <param name="token"></param>
        public async Task SetCredentials(string token)
        {
            Client.Credentials = new(token);
            var account = new GitHubAccountConfiguration
            {
                Id = 0,
                AccessToken = token,
            };
            await DatabaseProviderService.InsertOrReplaceAsync(account).ConfigureAwait(false);
        }
    }
}