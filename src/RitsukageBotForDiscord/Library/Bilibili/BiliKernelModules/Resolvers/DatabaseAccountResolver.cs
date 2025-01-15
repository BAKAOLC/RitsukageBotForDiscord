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
            SaveCookiesToDatabaseAsync(cookies).Wait();
        }

        /// <inheritdoc />
        public void RemoveCookies()
        {
            _cacheCookies = null;
            RemoveCookiesFromDatabaseAsync().Wait();
        }

        /// <inheritdoc />
        public BiliToken? GetToken()
        {
            if (_cacheToken != null) return _cacheToken;

            _cacheToken = GetTokenFromDatabaseAsync().Result;
            return _cacheToken;
        }

        /// <inheritdoc />
        public void RemoveToken()
        {
            _cacheToken = null;
            RemoveTokenFromDatabaseAsync().Wait();
        }

        /// <inheritdoc />
        public void SaveToken(BiliToken token)
        {
            _cacheToken = token;
            SaveTokenToDatabaseAsync(token).Wait();
        }

        private async Task SaveTokenToDatabaseAsync(BiliToken token)
        {
            var data = JsonSerializer.Serialize(token, TokenSerializeContext.Default.BiliToken);
            BilibiliAccountConfiguration account;
            try
            {
                account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                account.Token = data;
                await database.UpdateAsync(account);
            }
            catch (Exception)
            {
                account = new()
                {
                    Id = 0,
                    Token = data,
                };
                await database.InsertAsync(account);
            }
        }

        private async Task<BiliToken?> GetTokenFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                return account.Token is null ? null : JsonSerializer.Deserialize<BiliToken>(account.Token, TokenSerializeContext.Default.BiliToken);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task RemoveTokenFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                account.Token = null;
                await database.UpdateAsync(account);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task SaveCookiesToDatabaseAsync(IDictionary<string, string> cookies)
        {
            var data = JsonSerializer.Serialize(cookies, CookieSerializeContext.Default.DictionaryStringString);
            BilibiliAccountConfiguration account;
            try
            {
                account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                account.Cookies = data;
                await database.UpdateAsync(account);
            }
            catch (Exception)
            {
                account = new()
                {
                    Id = 0,
                    Cookies = data,
                };
                await database.InsertAsync(account);
            }
        }

        private async Task<IDictionary<string, string>?> GetCookiesFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                return account.Cookies is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(account.Cookies, CookieSerializeContext.Default.DictionaryStringString);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task RemoveCookiesFromDatabaseAsync()
        {
            try
            {
                var account = await database.GetAsync<BilibiliAccountConfiguration>(0);
                account.Cookies = null;
                await database.UpdateAsync(account);
            }
            catch (Exception)
            {
                // ignored
            }
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