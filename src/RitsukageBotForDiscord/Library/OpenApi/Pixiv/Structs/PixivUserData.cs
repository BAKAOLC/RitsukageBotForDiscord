using Newtonsoft.Json;

namespace RitsukageBot.Library.OpenApi.Pixiv.Structs
{
    /// <summary>
    ///     Pixiv用户信息响应
    /// </summary>
    /// <param name="Error"></param>
    /// <param name="Message"></param>
    /// <param name="Body"></param>
    public record PixivUserResponse(
        [property: JsonProperty("error")] bool Error,
        [property: JsonProperty("message")] string Message,
        [property: JsonProperty("body")] PixivUserData Body
    );

    /// <summary>
    ///     Pixiv用户数据
    /// </summary>
    /// <param name="UserId"></param>
    /// <param name="Name"></param>
    /// <param name="Image"></param>
    /// <param name="ImageBig"></param>
    /// <param name="Premium"></param>
    /// <param name="IsFollowed"></param>
    /// <param name="IsMypixiv"></param>
    /// <param name="IsBlocking"></param>
    /// <param name="Background"></param>
    /// <param name="SketchLiveId"></param>
    /// <param name="Partial"></param>
    /// <param name="SketchLives"></param>
    /// <param name="Commission"></param>
    public record PixivUserData(
        [property: JsonProperty("userId")] string UserId,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("image")] string Image,
        [property: JsonProperty("imageBig")] string ImageBig,
        [property: JsonProperty("premium")] bool Premium,
        [property: JsonProperty("isFollowed")] bool IsFollowed,
        [property: JsonProperty("isMypixiv")] bool IsMypixiv,
        [property: JsonProperty("isBlocking")] bool IsBlocking,
        [property: JsonProperty("background")] PixivUserBackground Background,
        [property: JsonProperty("sketchLiveId")] string? SketchLiveId,
        [property: JsonProperty("partial")] int Partial,
        [property: JsonProperty("sketchLives")] object[] SketchLives,
        [property: JsonProperty("commission")] PixivUserCommission Commission
    );

    /// <summary>
    ///     Pixiv用户背景信息
    /// </summary>
    /// <param name="Repeat"></param>
    /// <param name="Color"></param>
    /// <param name="Url"></param>
    /// <param name="IsPrivate"></param>
    public record PixivUserBackground(
        [property: JsonProperty("repeat")] string? Repeat,
        [property: JsonProperty("color")] string? Color,
        [property: JsonProperty("url")] string Url,
        [property: JsonProperty("isPrivate")] bool IsPrivate
    );

    /// <summary>
    ///     Pixiv用户委托信息
    /// </summary>
    /// <param name="AcceptRequest"></param>
    /// <param name="IsSubscribedReopenNotification"></param>
    public record PixivUserCommission(
        [property: JsonProperty("acceptRequest")] bool AcceptRequest,
        [property: JsonProperty("isSubscribedReopenNotification")] bool IsSubscribedReopenNotification
    );
}


