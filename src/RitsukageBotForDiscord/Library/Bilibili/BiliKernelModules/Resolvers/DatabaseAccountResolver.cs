using System.Text.Json;
using System.Text.Json.Serialization;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Authorization;
using RitsukageBot.Library.Data;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Library.Bilibili.BiliKernelModules.Resolvers
{
    /// <summary>
    ///     Database account resolver.
    /// </summary>
    /// <param name="database"></param>
    public class DatabaseAccountResolver(DatabaseProviderService database) : IBiliCookiesResolver, IBiliTokenResolver
    {
        private IDictionary<string, string>? _cacheCookies;
        private BiliToken? _cacheToken;

        /// <inheritdoc />
        public IDictionary<string, string> GetCookies()
        {
            if (_cacheCookies != null) return _cacheCookies;

            // ReSharper disable once AsyncApostle.AsyncWait
            _cacheCookies = GetCookiesFromDatabaseAsync().Result;
            return _cacheCookies ?? new Dictionary<string, string>();
        }

        /// <inheritdoc />
        public string GetCookieString()
        {
            var cookies = GetCookies();
            var cookieList = cookies.Select(item => $"{item.Key}={item.Value}");
            return string.Join("; ", cookieList);
        }

        /// <inheritdoc />
        public void SaveCookies(IDictionary<string, string> cookies)
        {
            _cacheCookies = cookies;

            // ReSharper disable once AsyncApostle.AsyncWait
            SaveCookiesToDatabaseAsync(cookies).Wait();
        }

        /// <inheritdoc />
        public void RemoveCookies()
        {
            _cacheCookies = null;

            // ReSharper disable once AsyncApostle.AsyncWait
            RemoveCookiesFromDatabaseAsync().Wait();
        }

        /// <inheritdoc />
        public BiliToken? GetToken()
        {
            if (_cacheToken != null) return _cacheToken;

            // ReSharper disable once AsyncApostle.AsyncWait
            _cacheToken = GetTokenFromDatabaseAsync().Result;
            return _cacheToken;
        }

        /// <inheritdoc />
        public void RemoveToken()
        {
            _cacheToken = null;

            // ReSharper disable once AsyncApostle.AsyncWait
            RemoveTokenFromDatabaseAsync().Wait();
        }

        /// <inheritdoc />
        public void SaveToken(BiliToken token)
        {
            _cacheToken = token;

            // ReSharper disable once AsyncApostle.AsyncWait
            SaveTokenToDatabaseAsync(token).Wait();
        }

        private async Task SaveTokenToDatabaseAsync(BiliToken token)
        {
            var data = JsonSerializer.Serialize(token, TokenSerializeContext.Default.BiliToken);
            var (_, account) = await database.GetOrCreateAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
            account.Token = data;
            await database.InsertOrUpdateAsync(account).ConfigureAwait(false);
        }

        private async Task<BiliToken?> GetTokenFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
                return account.Token is null
                    ? null
                    : JsonSerializer.Deserialize<BiliToken>(account.Token, TokenSerializeContext.Default.BiliToken);
            }
            catch
            {
                return null;
            }
        }

        private async Task RemoveTokenFromDatabaseAsync()
        {
            var (_, account) = await database.GetOrCreateAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
            account.Token = null;
            await database.InsertOrUpdateAsync(account).ConfigureAwait(false);
        }

        private async Task SaveCookiesToDatabaseAsync(IDictionary<string, string> cookies)
        {
            var data = JsonSerializer.Serialize(cookies, CookieSerializeContext.Default.DictionaryStringString);
            var (_, account) = await database.GetOrCreateAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
            account.Cookies = data;
            await database.InsertOrUpdateAsync(account).ConfigureAwait(false);
        }

        private async Task<IDictionary<string, string>?> GetCookiesFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
                return account.Cookies is null
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(account.Cookies,
                        CookieSerializeContext.Default.DictionaryStringString);
            }
            catch
            {
                return null;
            }
        }

        private async Task RemoveCookiesFromDatabaseAsync()
        {
            var (_, account) = await database.GetOrCreateAsync<BilibiliAccountConfiguration>(0).ConfigureAwait(false);
            account.Cookies = null;
            await database.InsertOrUpdateAsync(account).ConfigureAwait(false);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class CookieSerializeContext : JsonSerializerContext
    {
    }

    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(BiliToken))]
    internal sealed partial class TokenSerializeContext : JsonSerializerContext
    {
    }
}