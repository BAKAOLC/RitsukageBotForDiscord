using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QWeatherApi;
using QWeatherApi.ApiContracts;
using ZiggyCreatures.Caching.Fusion;
using static QWeatherApi.ApiContracts.QGeolocationResponse;
using static QWeatherApi.ApiContracts.WeatherHourlyResponse;

namespace RitsukageBot.Services.Providers
{
    /// <param name="logger"></param>
    /// <param name="cacheProvider"></param>
    /// <param name="httpClientFactory"></param>
    public class QWeatherProviderService(
        ILogger<QWeatherProviderService> logger,
        IFusionCache cacheProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        /// <summary>
        ///     Cache key
        /// </summary>
        public const string CacheKey = "qweather_cache";

        private ApiHandlerOption CreateOption()
        {
            return new()
            {
                Domain = configuration.GetValue<string>("QWeather:Domain"),
                Token = configuration.GetValue<string>("QWeather:Token"),
            };
        }

        /// <summary>
        ///     Query location information by name.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<Location?> QueryLocation(string query)
        {
            var cacheKey = $"{CacheKey}:location:{query}";
            var location = await cacheProvider.GetOrSetAsync<string>(cacheKey, async cancellationToken =>
            {
                logger.LogDebug("Querying location for {Query}", query);
                var option = CreateOption();
                var api = new GeolocationApi<QGeolocationResponse>
                {
                    Request = new QGeolocationRequestByName
                    {
                        Name = query,
                    },
                };
                var requestMessage = await api.GenerateRequestMessageAsync(option);
                var httpClient = httpClientFactory.CreateClient();
                var responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken);
                var response = await api.ProcessResponseAsync(responseMessage, option);
                if (response?.Locations == null || response.Locations.Count == 0)
                    throw new InvalidOperationException($"No location found for query: {query}");
                var location = ToLocation(response.Locations[0]);
                var locationJson = JsonConvert.SerializeObject(location, Formatting.None);
                logger.LogDebug("{Query} found location: {@Location}", query, location);
                return locationJson;
            }, options =>
            {
                options.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                options.FactorySoftTimeout = TimeSpan.FromSeconds(20);
                options.Duration = TimeSpan.FromDays(1);
                options.FailSafeMaxDuration = options.Duration * 3;
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(location)) return JsonConvert.DeserializeObject<Location>(location);
            logger.LogWarning("No location found for query: {Query}", query);
            return null;
        }

        /// <summary>
        ///     Query current weather information by latitude and longitude.
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<WeatherNow?> QueryWeatherNow(double lat, double lon)
        {
            var cacheKey = $"{CacheKey}:weather:now:{lat}:{lon}";
            var weatherNow = await cacheProvider.GetOrSetAsync<string>(cacheKey, async cancellationToken =>
            {
                logger.LogDebug("Querying current weather for lat: {Lat}, lon: {Lon}", lat, lon);
                var option = CreateOption();
                var api = new WeatherNowApi
                {
                    Request = new(lon, lat),
                };
                var requestMessage = await api.GenerateRequestMessageAsync(option);
                var httpClient = httpClientFactory.CreateClient();
                var responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken);
                var response = await api.ProcessResponseAsync(responseMessage, option);
                if (response == null)
                    throw new InvalidOperationException($"No weather data found for lat: {lat}, lon: {lon}");
                var weather = ToWeatherNow(response.WeatherNow);
                var weatherJson = JsonConvert.SerializeObject(weather, Formatting.None);
                logger.LogDebug("Querying current weather for lat: {Lat}, lon: {Lon} got result: {@Weather}", lat, lon,
                    weather);
                return weatherJson;
            }, options =>
            {
                options.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                options.FactorySoftTimeout = TimeSpan.FromSeconds(20);
                options.Duration = TimeSpan.FromMinutes(30);
                options.FailSafeMaxDuration = options.Duration * 3;
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(weatherNow))
                return JsonConvert.DeserializeObject<WeatherNow>(weatherNow);
            logger.LogWarning("No current weather found for lat: {Lat}, lon: {Lon}", lat, lon);
            return null;
        }

        /// <summary>
        ///     Query hourly weather forecast for the next 168 hours (7 days) by latitude and longitude.
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<WeatherHourlyForecast[]> QueryWeather168Hours(double lat, double lon)
        {
            var cacheKey = $"{CacheKey}:weather:168:{lat}:{lon}";
            var weatherList = await cacheProvider.GetOrSetAsync<string>(cacheKey, async cancellationToken =>
            {
                logger.LogDebug("Querying 168-hour weather for lat: {Lat}, lon: {Lon}", lat, lon);
                var option = CreateOption();
                var api = new WeatherHourlyApi
                {
                    Request = new(lon, lat),
                };
                var requestMessage = await api.GenerateRequestMessageAsync(option);
                var httpClient = httpClientFactory.CreateClient();
                var responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken);
                var response = await api.ProcessResponseAsync(responseMessage, option);
                if (response == null)
                    throw new InvalidOperationException($"No weather data found for lat: {lat}, lon: {lon}");
                var weatherList = response.HourlyForecasts.Select(ToWeatherHourlyForecast).ToList();
                var weatherJson = JsonConvert.SerializeObject(weatherList, Formatting.None);
                logger.LogDebug("Querying 168-hour weather for lat: {Lat}, lon: {Lon} got result: {@Response}", lat,
                    lon, weatherList);
                return weatherJson;
            }, options =>
            {
                options.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                options.FactorySoftTimeout = TimeSpan.FromSeconds(20);
                options.Duration = TimeSpan.FromHours(1);
                options.FailSafeMaxDuration = options.Duration * 3;
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(weatherList))
            {
                var weatherArray = JsonConvert.DeserializeObject<WeatherHourlyForecast[]>(weatherList);
                if (weatherArray is { Length: > 0 })
                    return weatherArray;
            }

            logger.LogWarning("No 168-hour weather found for lat: {Lat}, lon: {Lon}", lat, lon);
            return [];
        }

        private static Location ToLocation(QGeolocationItem item)
        {
            return new(
                item.Name,
                item.Id,
                item.Lat,
                item.Lon,
                item.AdministrativeDistrict2,
                item.AdministrativeDistrict1,
                item.Country,
                item.TimeZone,
                item.UtcOffset,
                item.IsDaylightSavingTime,
                item.Type,
                item.Rank,
                item.FxLink);
        }

        private static WeatherNow ToWeatherNow(WeatherNowResponse.WeatherNowItem weatherNow)
        {
            return new(
                weatherNow.ObsTime,
                weatherNow.Temp,
                weatherNow.FeelsLike,
                weatherNow.Icon,
                weatherNow.Text,
                weatherNow.Wind360,
                weatherNow.WindDir,
                weatherNow.WindScale,
                weatherNow.WindSpeed,
                weatherNow.Humidity,
                weatherNow.Precipitation,
                weatherNow.Pressure,
                weatherNow.Vis,
                weatherNow.Cloud,
                weatherNow.Dew);
        }

        private static WeatherHourlyForecast ToWeatherHourlyForecast(
            HourlyForecastItem weatherHourly)
        {
            return new(
                weatherHourly.FxTime,
                weatherHourly.Temp,
                weatherHourly.Icon,
                weatherHourly.Text,
                weatherHourly.Wind360,
                weatherHourly.WindDir,
                weatherHourly.WindScale,
                weatherHourly.WindSpeed,
                weatherHourly.Humidity,
                weatherHourly.Pop,
                weatherHourly.Precipitation,
                weatherHourly.Pressure,
                weatherHourly.Cloud,
                weatherHourly.Dew);
        }

        /// <summary>
        ///     Location information for a specific location.
        /// </summary>
        /// <param name="Name">地区/城市名称</param>
        /// <param name="Id">地区/城市ID</param>
        /// <param name="Lat">纬度</param>
        /// <param name="Lon">经度</param>
        /// <param name="AdministrativeDistrict2">上级行政区划名称</param>
        /// <param name="AdministrativeDistrict1">一级行政区域名称</param>
        /// <param name="Country">所属国家</param>
        /// <param name="TimeZone">所在时区</param>
        /// <param name="UtcOffset">与UTC时间偏移的小时数</param>
        /// <param name="IsDaylightSavingTime">是否当前处于夏令时</param>
        /// <param name="Type">属性</param>
        /// <param name="Rank">评分</param>
        /// <param name="FxLink">天气预报网页链接</param>
        public record Location(
            string Name,
            string Id,
            double Lat,
            double Lon,
            string AdministrativeDistrict2,
            string AdministrativeDistrict1,
            string Country,
            string TimeZone,
            string UtcOffset,
            string IsDaylightSavingTime,
            string Type,
            int Rank,
            string FxLink);

        /// <summary>
        ///     Weather information for the current time.
        /// </summary>
        /// <param name="ObsTime">数据观测时间</param>
        /// <param name="Temp">温度（摄氏度）</param>
        /// <param name="FeelsLike">体感温度（摄氏度）</param>
        /// <param name="Icon">天气状况图标代码</param>
        /// <param name="Text">天气状况文字描述</param>
        /// <param name="Wind360">风向角度（角度）</param>
        /// <param name="WindDir">风向</param>
        /// <param name="WindScale">风力等级</param>
        /// <param name="WindSpeed">风速（公里/小时）</param>
        /// <param name="Humidity">相对湿度（百分比）</param>
        /// <param name="Precipitation">过去一小时降水量（毫米）</param>
        /// <param name="Pressure">大气压强（百帕）</param>
        /// <param name="Vis">能见度（公里）</param>
        /// <param name="Cloud">云量（百分比）</param>
        /// <param name="Dew">露点温度</param>
        public record WeatherNow(
            DateTime ObsTime,
            int Temp,
            int FeelsLike,
            string Icon,
            string Text,
            int Wind360,
            string WindDir,
            string WindScale,
            int WindSpeed,
            int Humidity,
            double Precipitation,
            int Pressure,
            int Vis,
            int Cloud,
            int Dew);

        /// <summary>
        ///     Weather forecast for the next hours.
        /// </summary>
        /// <param name="FxTime">预报时间</param>
        /// <param name="Temp">温度（摄氏度）</param>
        /// <param name="Icon">天气状况图标代码</param>
        /// <param name="Text">天气状况文字描述</param>
        /// <param name="Wind360">风向角度（角度）</param>
        /// <param name="WindDir">风向</param>
        /// <param name="WindScale">风力等级</param>
        /// <param name="WindSpeed">风速（公里/小时）</param>
        /// <param name="Humidity">相对湿度（百分比）</param>
        /// <param name="Pop">逐小时预报降水概率（百分比）</param>
        /// <param name="Precipitation">当前小时累计降水量（毫米）</param>
        /// <param name="Pressure">大气压强（百帕）</param>
        /// <param name="Cloud">云量（百分比）</param>
        /// <param name="Dew">露点温度</param>
        public record WeatherHourlyForecast(
            DateTime FxTime,
            int Temp,
            string Icon,
            string Text,
            int Wind360,
            string WindDir,
            string WindScale,
            int WindSpeed,
            int Humidity,
            int Pop,
            double Precipitation,
            int Pressure,
            int? Cloud,
            int? Dew);
    }
}