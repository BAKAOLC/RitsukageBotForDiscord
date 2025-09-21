using Newtonsoft.Json;

namespace RitsukageBot.Library.OpenApi.Pixiv.Structs
{
    /// <summary>
    ///     Pixiv插画信息响应
    /// </summary>
    /// <param name="Error"></param>
    /// <param name="Message"></param>
    /// <param name="Body"></param>
    public record PixivIllustResponse(
        [property: JsonProperty("error")] bool Error,
        [property: JsonProperty("message")] string Message,
        [property: JsonProperty("body")] PixivIllustData Body
    );

    /// <summary>
    ///     Pixiv插画数据
    /// </summary>
    /// <param name="IllustId"></param>
    /// <param name="IllustTitle"></param>
    /// <param name="IllustComment"></param>
    /// <param name="Id"></param>
    /// <param name="Title"></param>
    /// <param name="Description"></param>
    /// <param name="IllustType"></param>
    /// <param name="CreateDate"></param>
    /// <param name="UploadDate"></param>
    /// <param name="Restrict"></param>
    /// <param name="XRestrict"></param>
    /// <param name="Sl"></param>
    /// <param name="Urls"></param>
    /// <param name="Tags"></param>
    /// <param name="Alt"></param>
    /// <param name="UserId"></param>
    /// <param name="UserName"></param>
    /// <param name="UserAccount"></param>
    /// <param name="UserIllusts"></param>
    /// <param name="LikeData"></param>
    /// <param name="Width"></param>
    /// <param name="Height"></param>
    /// <param name="PageCount"></param>
    /// <param name="BookmarkCount"></param>
    /// <param name="LikeCount"></param>
    /// <param name="CommentCount"></param>
    /// <param name="ResponseCount"></param>
    /// <param name="ViewCount"></param>
    /// <param name="BookStyle"></param>
    /// <param name="IsHowto"></param>
    /// <param name="IsOriginal"></param>
    /// <param name="ImageResponseOutData"></param>
    /// <param name="ImageResponseData"></param>
    /// <param name="ImageResponseCount"></param>
    /// <param name="PollData"></param>
    /// <param name="SeriesNavData"></param>
    /// <param name="DescriptionBoothId"></param>
    /// <param name="DescriptionYoutubeId"></param>
    /// <param name="ComicPromotion"></param>
    /// <param name="FanboxPromotion"></param>
    /// <param name="ContestBanners"></param>
    /// <param name="IsBookmarkable"></param>
    /// <param name="BookmarkData"></param>
    /// <param name="ContestData"></param>
    /// <param name="ZoneConfig"></param>
    /// <param name="ExtraData"></param>
    /// <param name="TitleCaptionTranslation"></param>
    /// <param name="IsUnlisted"></param>
    /// <param name="Request"></param>
    /// <param name="CommentOff"></param>
    /// <param name="AiType"></param>
    /// <param name="ReuploadDate"></param>
    /// <param name="LocationMask"></param>
    /// <param name="CommissionLinkHidden"></param>
    /// <param name="IsLoginOnly"></param>
    /// <param name="NoLoginData"></param>
    public record PixivIllustData(
        [property: JsonProperty("illustId")] string IllustId,
        [property: JsonProperty("illustTitle")] string IllustTitle,
        [property: JsonProperty("illustComment")] string IllustComment,
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("title")] string Title,
        [property: JsonProperty("description")] string Description,
        [property: JsonProperty("illustType")] int IllustType,
        [property: JsonProperty("createDate")] string CreateDate,
        [property: JsonProperty("uploadDate")] string UploadDate,
        [property: JsonProperty("restrict")] int Restrict,
        [property: JsonProperty("xRestrict")] int XRestrict,
        [property: JsonProperty("sl")] int Sl,
        [property: JsonProperty("urls")] PixivIllustUrls Urls,
        [property: JsonProperty("tags")] PixivIllustTags Tags,
        [property: JsonProperty("alt")] string Alt,
        [property: JsonProperty("userId")] string UserId,
        [property: JsonProperty("userName")] string UserName,
        [property: JsonProperty("userAccount")] string UserAccount,
        [property: JsonProperty("userIllusts")] Dictionary<string, PixivUserIllust?> UserIllusts,
        [property: JsonProperty("likeData")] bool LikeData,
        [property: JsonProperty("width")] int Width,
        [property: JsonProperty("height")] int Height,
        [property: JsonProperty("pageCount")] int PageCount,
        [property: JsonProperty("bookmarkCount")] int BookmarkCount,
        [property: JsonProperty("likeCount")] int LikeCount,
        [property: JsonProperty("commentCount")] int CommentCount,
        [property: JsonProperty("responseCount")] int ResponseCount,
        [property: JsonProperty("viewCount")] int ViewCount,
        [property: JsonProperty("bookStyle")] string BookStyle,
        [property: JsonProperty("isHowto")] bool IsHowto,
        [property: JsonProperty("isOriginal")] bool IsOriginal,
        [property: JsonProperty("imageResponseOutData")] object[] ImageResponseOutData,
        [property: JsonProperty("imageResponseData")] object[] ImageResponseData,
        [property: JsonProperty("imageResponseCount")] int ImageResponseCount,
        [property: JsonProperty("pollData")] object? PollData,
        [property: JsonProperty("seriesNavData")] object? SeriesNavData,
        [property: JsonProperty("descriptionBoothId")] object? DescriptionBoothId,
        [property: JsonProperty("descriptionYoutubeId")] object? DescriptionYoutubeId,
        [property: JsonProperty("comicPromotion")] object? ComicPromotion,
        [property: JsonProperty("fanboxPromotion")] object? FanboxPromotion,
        [property: JsonProperty("contestBanners")] object[] ContestBanners,
        [property: JsonProperty("isBookmarkable")] bool IsBookmarkable,
        [property: JsonProperty("bookmarkData")] object? BookmarkData,
        [property: JsonProperty("contestData")] object? ContestData,
        [property: JsonProperty("zoneConfig")] object? ZoneConfig,
        [property: JsonProperty("extraData")] object? ExtraData,
        [property: JsonProperty("titleCaptionTranslation")] PixivTitleCaptionTranslation TitleCaptionTranslation,
        [property: JsonProperty("isUnlisted")] bool IsUnlisted,
        [property: JsonProperty("request")] object? Request,
        [property: JsonProperty("commentOff")] int CommentOff,
        [property: JsonProperty("aiType")] int AiType,
        [property: JsonProperty("reuploadDate")] object? ReuploadDate,
        [property: JsonProperty("locationMask")] bool LocationMask,
        [property: JsonProperty("commissionLinkHidden")] bool CommissionLinkHidden,
        [property: JsonProperty("isLoginOnly")] bool IsLoginOnly,
        [property: JsonProperty("noLoginData")] object? NoLoginData
    );

    /// <summary>
    ///     Pixiv插画URL信息
    /// </summary>
    /// <param name="Mini"></param>
    /// <param name="Thumb"></param>
    /// <param name="Small"></param>
    /// <param name="Regular"></param>
    /// <param name="Original"></param>
    public record PixivIllustUrls(
        [property: JsonProperty("mini")] string? Mini,
        [property: JsonProperty("thumb")] string? Thumb,
        [property: JsonProperty("small")] string? Small,
        [property: JsonProperty("regular")] string? Regular,
        [property: JsonProperty("original")] string? Original
    );

    /// <summary>
    ///     Pixiv插画标签信息
    /// </summary>
    /// <param name="AuthorId"></param>
    /// <param name="IsLocked"></param>
    /// <param name="Tags"></param>
    /// <param name="Writable"></param>
    public record PixivIllustTags(
        [property: JsonProperty("authorId")] string AuthorId,
        [property: JsonProperty("isLocked")] bool IsLocked,
        [property: JsonProperty("tags")] PixivTag[] Tags,
        [property: JsonProperty("writable")] bool Writable
    );

    /// <summary>
    ///     Pixiv标签
    /// </summary>
    /// <param name="Tag"></param>
    /// <param name="Locked"></param>
    /// <param name="Deletable"></param>
    /// <param name="UserId"></param>
    /// <param name="UserName"></param>
    /// <param name="Translation"></param>
    public record PixivTag(
        [property: JsonProperty("tag")] string Tag,
        [property: JsonProperty("locked")] bool Locked,
        [property: JsonProperty("deletable")] bool Deletable,
        [property: JsonProperty("userId")] string? UserId,
        [property: JsonProperty("userName")] string? UserName,
        [property: JsonProperty("translation")] Dictionary<string, string>? Translation
    );

    /// <summary>
    ///     Pixiv用户插画信息
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="Title"></param>
    /// <param name="IllustType"></param>
    /// <param name="XRestrict"></param>
    /// <param name="Restrict"></param>
    /// <param name="Sl"></param>
    /// <param name="Url"></param>
    /// <param name="Description"></param>
    /// <param name="Tags"></param>
    /// <param name="UserId"></param>
    /// <param name="UserName"></param>
    /// <param name="Width"></param>
    /// <param name="Height"></param>
    /// <param name="PageCount"></param>
    /// <param name="IsBookmarkable"></param>
    /// <param name="BookmarkData"></param>
    /// <param name="Alt"></param>
    /// <param name="TitleCaptionTranslation"></param>
    /// <param name="CreateDate"></param>
    /// <param name="UpdateDate"></param>
    /// <param name="IsUnlisted"></param>
    /// <param name="IsMasked"></param>
    /// <param name="AiType"></param>
    /// <param name="ProfileImageUrl"></param>
    public record PixivUserIllust(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("title")] string Title,
        [property: JsonProperty("illustType")] int IllustType,
        [property: JsonProperty("xRestrict")] int XRestrict,
        [property: JsonProperty("restrict")] int Restrict,
        [property: JsonProperty("sl")] int Sl,
        [property: JsonProperty("url")] string Url,
        [property: JsonProperty("description")] string Description,
        [property: JsonProperty("tags")] string[] Tags,
        [property: JsonProperty("userId")] string UserId,
        [property: JsonProperty("userName")] string UserName,
        [property: JsonProperty("width")] int Width,
        [property: JsonProperty("height")] int Height,
        [property: JsonProperty("pageCount")] int PageCount,
        [property: JsonProperty("isBookmarkable")] bool IsBookmarkable,
        [property: JsonProperty("bookmarkData")] object? BookmarkData,
        [property: JsonProperty("alt")] string Alt,
        [property: JsonProperty("titleCaptionTranslation")] PixivTitleCaptionTranslation TitleCaptionTranslation,
        [property: JsonProperty("createDate")] string CreateDate,
        [property: JsonProperty("updateDate")] string UpdateDate,
        [property: JsonProperty("isUnlisted")] bool IsUnlisted,
        [property: JsonProperty("isMasked")] bool IsMasked,
        [property: JsonProperty("aiType")] int AiType,
        [property: JsonProperty("profileImageUrl")] string? ProfileImageUrl
    );

    /// <summary>
    ///     Pixiv标题描述翻译
    /// </summary>
    /// <param name="WorkTitle"></param>
    /// <param name="WorkCaption"></param>
    public record PixivTitleCaptionTranslation(
        [property: JsonProperty("workTitle")] string? WorkTitle,
        [property: JsonProperty("workCaption")] string? WorkCaption
    );
}


