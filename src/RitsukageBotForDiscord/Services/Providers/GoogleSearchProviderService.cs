using GoogleApi;
using GoogleApi.Entities.Search.Common;
using GoogleApi.Entities.Search.Web.Request;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Google search provider service
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration"></param>
    public class GoogleSearchProviderService(
        ILogger<GoogleSearchProviderService> logger,
        IConfiguration configuration)
    {
        /// <summary>
        ///     Web search
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<Item[]> WebSearch(string query)
        {
            var key = configuration["Google:ApiKey"];
            var searchEngineId = configuration["Google:SearchEngineId"];
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(searchEngineId))
                throw new InvalidOperationException("Google API key or search engine ID is missing");

            logger.LogInformation("Google search: {Query}", query);
            var request = new WebSearchRequest
            {
                Key = key,
                SearchEngineId = searchEngineId,
                Query = query,
            };
            var response = await GoogleSearch.WebSearch.QueryAsync(request).ConfigureAwait(false);
            return [.. response.Items];
        }
    }
}