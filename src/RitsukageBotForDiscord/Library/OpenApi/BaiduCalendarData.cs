using Newtonsoft.Json;

namespace RitsukageBot.Library.OpenApi
{
    /// <summary>
    ///     Baidu calendar day
    /// </summary>
    /// <param name="Animal"></param>
    /// <param name="Avoid"></param>
    /// <param name="CnDay"></param>
    /// <param name="Day"></param>
    /// <param name="Desc"></param>
    /// <param name="FestivalInfoList"></param>
    /// <param name="FestivalList"></param>
    /// <param name="GzDate"></param>
    /// <param name="GzMonth"></param>
    /// <param name="GzYear"></param>
    /// <param name="IsBigMonth"></param>
    /// <param name="Jiri"></param>
    /// <param name="LDate"></param>
    /// <param name="LMonth"></param>
    /// <param name="LunarDate"></param>
    /// <param name="LunarMonth"></param>
    /// <param name="LunarYear"></param>
    /// <param name="Month"></param>
    /// <param name="ODate"></param>
    /// <param name="Status"></param>
    /// <param name="Suit"></param>
    /// <param name="Timestamp"></param>
    /// <param name="Value"></param>
    /// <param name="Year"></param>
    /// <param name="YjJumpUrl"></param>
    /// <param name="YjFrom"></param>
    /// <param name="Term"></param>
    /// <param name="Type"></param>
    public record BaiduCalendarDay(
        [property: JsonProperty("animal")] string Animal,
        [property: JsonProperty("avoid")] string Avoid,
        [property: JsonProperty("cnDay")] string CnDay,
        [property: JsonProperty("day")] string Day,
        [property: JsonProperty("desc")] string Desc,
        [property: JsonProperty("festivalInfoList")]
        BaiduCalendarFestivalInfo[] FestivalInfoList,
        [property: JsonProperty("festivalList")]
        string FestivalList,
        [property: JsonProperty("gzDate")] string GzDate,
        [property: JsonProperty("gzMonth")] string GzMonth,
        [property: JsonProperty("gzYear")] string GzYear,
        [property: JsonProperty("isBigMonth")] string IsBigMonth,
        [property: JsonProperty("jiri")] string Jiri,
        [property: JsonProperty("lDate")] string LDate,
        [property: JsonProperty("lMonth")] string LMonth,
        [property: JsonProperty("lunarDate")] string LunarDate,
        [property: JsonProperty("lunarMonth")] string LunarMonth,
        [property: JsonProperty("lunarYear")] string LunarYear,
        [property: JsonProperty("month")] string Month,
        [property: JsonProperty("oDate")] DateTimeOffset ODate,
        [property: JsonProperty("status")] string Status,
        [property: JsonProperty("suit")] string Suit,
        [property: JsonProperty("timestamp")] string Timestamp,
        [property: JsonProperty("value")] string Value,
        [property: JsonProperty("year")] string Year,
        [property: JsonProperty("yjJumpUrl")] string YjJumpUrl,
        [property: JsonProperty("yj_from")] string YjFrom,
        [property: JsonProperty("term")] string Term,
        [property: JsonProperty("type")] string Type
    );

    /// <summary>
    ///     Baidu calendar data
    /// </summary>
    /// <param name="BaikeId"></param>
    /// <param name="BaikeName"></param>
    /// <param name="BaikeUrl"></param>
    /// <param name="Name"></param>
    public record BaiduCalendarFestivalInfo(
        [property: JsonProperty("baikeId")] string BaikeId,
        [property: JsonProperty("baikeName")] string BaikeName,
        [property: JsonProperty("baikeUrl")] string BaikeUrl,
        [property: JsonProperty("name")] string Name
    );
}