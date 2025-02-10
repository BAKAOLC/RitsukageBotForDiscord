namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Network utility.
    /// </summary>
    public static class NetworkUtility
    {
        private static IHttpClientFactory? _httpClientFactory;
        private static HttpClient? _httpClient;

        /// <summary>
        ///     Set the HTTP client factory.
        /// </summary>
        /// <param name="httpClientFactory"></param>
        public static void SetHttpClientFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        ///     Get the HTTP client.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static HttpClient GetHttpClient()
        {
            return _httpClient ??= _httpClientFactory?.CreateClient() ??
                                   throw new InvalidOperationException("HTTP client factory is not set.");
        }

        /// <summary>
        ///     Solve the short link.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string?> SolveShortLinkAsync(string url)
        {
            var httpClient = GetHttpClient();
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var location = response.Headers.Location;
            return location?.ToString() ?? url;
        }
    }
}