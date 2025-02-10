using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Utils;

namespace RitsukageBot.Library.OpenApi
{
    public partial class OpenApi
    {
        /// <summary>
        ///     Get calendar information from Baidu
        ///     Return the calendar information from the previous month to the next month
        /// </summary>
        /// <param name="date"></param>
        /// <param name="httpClient"></param>
        /// <returns></returns>
        public static async Task<BaiduCalendarDay[]> GetCalendarAsync(DateTimeOffset date,
            HttpClient? httpClient = null)
        {
            httpClient ??= NetworkUtility.GetHttpClient();
            var dateStr = date.ToLocalTime().ToString("yyyy年M月");
            var response = await httpClient.GetAsync(
                    $"https://opendata.baidu.com/data/inner?tn=reserved_all_res_tn&type=json&resource_id=52109&query={dateStr.UrlEncode()}&apiType=yearMonthData")
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jsonData = JObject.Parse(responseString);
            if (!jsonData.TryGetValue("Result", out var result)
                || result is not JArray { First: JObject firstResultData } ||
                !firstResultData.TryGetValue("DisplayData", out var displayData)
                || displayData is not JObject displayDataData ||
                !displayDataData.TryGetValue("resultData", out var resultDataData)
                || resultDataData is not JObject resultDataDataData ||
                !resultDataDataData.TryGetValue("tplData", out var tplData)
                || tplData is not JObject tplDataData || !tplDataData.TryGetValue("data", out var data)
                || data is not JObject dataData || !dataData.TryGetValue("almanac", out var almanac))
                return [];
            var calendar = almanac.ToObject<BaiduCalendarDay[]>();
            return calendar is not { Length: > 0 } ? [] : calendar;
        }
    }
}