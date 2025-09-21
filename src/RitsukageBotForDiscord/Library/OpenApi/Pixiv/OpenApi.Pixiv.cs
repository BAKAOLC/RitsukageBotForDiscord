using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RitsukageBot.Library.OpenApi.Pixiv.Structs;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Library.OpenApi.Pixiv
{
    /// <summary>
    ///     OpenApi.Pixiv
    /// </summary>
    public static class OpenApiPixiv
    {
        /// <summary>
        ///     获取Pixiv用户信息
        /// </summary>
        /// <param name="instance">OpenApi实例</param>
        /// <param name="userId">用户ID</param>
        /// <param name="httpClient">HTTP客户端</param>
        /// <returns>用户信息</returns>
        public static async Task<PixivUserData?> GetPixivUserAsync(this OpenApi instance, string userId,
            HttpClient? httpClient = null)
        {
            var cacheKey = $"Pixiv_User_{userId}";
            if (instance.CacheProvider is not null)
            {
                var recordInfo = await instance.CacheProvider.GetOrDefaultAsync<string>(cacheKey).ConfigureAwait(false);
                if (recordInfo is not null)
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PixivUserResponse>(recordInfo);
                    if (cachedResponse is not null && !cachedResponse.Error)
                        return cachedResponse.Body;
                }
            }

            httpClient ??= NetworkUtility.GetHttpClient();

            httpClient.DefaultRequestHeaders.Referrer = new("https://www.pixiv.net/");

            var cookieString = instance.Configuration?.GetValue<string>("OpenApi:PixivCookie");
            if (!string.IsNullOrWhiteSpace(cookieString))
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);

            try
            {
                var response = await httpClient.GetAsync($"https://www.pixiv.net/ajax/user/{userId}")
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var pixivResponse = JsonConvert.DeserializeObject<PixivUserResponse>(responseString);

                if (pixivResponse is not null && !pixivResponse.Error)
                {
                    if (instance.CacheProvider is not null)
                        await instance.CacheProvider.SetAsync(cacheKey, responseString, new()
                        {
                            Duration = TimeSpan.FromHours(6),
                            FailSafeMaxDuration = TimeSpan.FromDays(1),
                        }).ConfigureAwait(false);

                    return pixivResponse.Body;
                }
            }
            catch (Exception ex)
            {
                instance.Logger?.LogError(
                    "Failed to get Pixiv user info (ID: {UserId}): {Message}", userId, ex.Message);
            }

            return null;
        }

        /// <summary>
        ///     获取Pixiv插画信息
        /// </summary>
        /// <param name="instance">OpenApi实例</param>
        /// <param name="illustId">插画ID</param>
        /// <param name="httpClient">HTTP客户端</param>
        /// <returns>插画信息</returns>
        public static async Task<PixivIllustData?> GetPixivIllustAsync(this OpenApi instance, string illustId,
            HttpClient? httpClient = null)
        {
            var cacheKey = $"Pixiv_Illust_{illustId}";
            if (instance.CacheProvider is not null)
            {
                var recordInfo = await instance.CacheProvider.GetOrDefaultAsync<string>(cacheKey).ConfigureAwait(false);
                if (recordInfo is not null)
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PixivIllustResponse>(recordInfo);
                    if (cachedResponse is not null && !cachedResponse.Error)
                        return cachedResponse.Body;
                }
            }

            httpClient ??= NetworkUtility.GetHttpClient();

            httpClient.DefaultRequestHeaders.Referrer = new("https://www.pixiv.net/");

            var cookieString = instance.Configuration?.GetValue<string>("OpenApi:PixivCookie");
            if (!string.IsNullOrWhiteSpace(cookieString))
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);

            try
            {
                var response = await httpClient.GetAsync($"https://www.pixiv.net/ajax/illust/{illustId}")
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var pixivResponse = JsonConvert.DeserializeObject<PixivIllustResponse>(responseString);

                if (pixivResponse is not null && !pixivResponse.Error)
                {
                    if (instance.CacheProvider is not null)
                        await instance.CacheProvider.SetAsync(cacheKey, responseString, new()
                        {
                            Duration = TimeSpan.FromHours(12),
                            FailSafeMaxDuration = TimeSpan.FromDays(3),
                        }).ConfigureAwait(false);

                    return pixivResponse.Body;
                }
            }
            catch (Exception ex)
            {
                instance.Logger?.LogError(
                    "Failed to get Pixiv illust info (ID: {IllustId}): {Message}", illustId, ex.Message);
            }

            return null;
        }

        /// <summary>
        ///     Returns a proxied Pixiv image URL using the configured proxy template, or the original image URL if no proxy is set.
        /// </summary>
        /// <param name="instance">OpenApi instance</param>
        /// <param name="imageUrl">Original Pixiv image URL</param>
        /// <returns>Proxied image URL or original image URL</returns>
        public static string GetPixivImageProxyUrl(this OpenApi instance, string imageUrl)
        {
            var proxyTemplate = instance.Configuration?.GetValue<string>("OpenApi:PixivImageProxy");
            return string.IsNullOrWhiteSpace(proxyTemplate) ? imageUrl : string.Format(proxyTemplate, imageUrl);
        }
    }
}