using CacheTower;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Graphic;
using RitsukageBot.Library.Graphic.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RitsukageBot.Modules.Command
{
    /// <summary>
    ///     Sample command module
    /// </summary>
    public class SampleCommandModule : ModuleBase<SocketCommandContext>
    {
        /// <summary>
        ///     Http client factory
        /// </summary>
        public required IHttpClientFactory HttpClientFactory { get; set; }

        /// <summary>
        ///     Cache provider
        /// </summary>
        public required ICacheStack CacheProvider { get; set; }

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<SampleCommandModule> Logger { get; set; }

        /// <summary>
        ///     Ping
        /// </summary>
        /// <returns></returns>
        [Command("ping")]
        [Summary("Replies with pong")]
        public Task PingAsync()
        {
            return ReplyAsync("Pong!");
        }

        /// <summary>
        ///     Echo
        /// </summary>
        /// <param name="message">The text to echo</param>
        /// <returns></returns>
        [Command("echo")]
        [Summary("Echoes a message")]
        public Task EchoAsync([Remainder] [Summary("The text to echo")] string message)
        {
            return ReplyAsync(message);
        }

        /// <summary>
        ///     Squares a number
        /// </summary>
        /// <param name="num">The number to square</param>
        /// <returns></returns>
        [Command("square")]
        [Summary("Squares a number")]
        public Task SquareAsync(
            [Summary("The number to square")] int num)
        {
            return ReplyAsync($"{num}^2 = {Math.Pow(num, 2)}");
        }

        /// <summary>
        ///     Returns info about the current user, or the user parameter, if one passed
        /// </summary>
        /// <param name="user">The (optional) user to get info from</param>
        /// <returns></returns>
        [Command("userinfo")]
        [Summary("Returns info about the current user, or the user parameter, if one passed")]
        [Alias("user", "whois")]
        public Task UserInfoAsync(
            [Summary("The (optional) user to get info from")]
            SocketUser? user = null)
        {
            var userInfo = user ?? Context.User;
            return ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator}");
        }

        /// <summary>
        ///     Invert color
        /// </summary>
        /// <param name="url"></param>
        [Command("invertcolor")]
        [Summary("Invert color")]
        public async Task InvertImageColorAsync(
            [Summary("The image url")] string? url = null)
        {
            if (url == null)
            {
                if (Context.Message.ReferencedMessage != null)
                {
                    if (Context.Message.ReferencedMessage.Attachments.Count == 0)
                    {
                        await ReplyAsync("No image found");
                        return;
                    }

                    url = Context.Message.ReferencedMessage.Attachments.First().Url;
                }
                else if (Context.Message.Attachments.Count == 0)
                {
                    await ReplyAsync("No image found");
                    return;
                }
                else
                {
                    url = Context.Message.Attachments.First().Url;
                }
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) await ReplyAsync("Invalid url");

            var resultCacheKey = $"image_invertcolor_{url}";
            var resultCache = await CacheProvider.GetAsync<byte[]>(resultCacheKey);
            if (resultCache?.Value is { Length: > 0 })
            {
                Logger.LogDebug("Found result cache for {url}", url);
                using var memoryStream = new MemoryStream(resultCache.Value);
                using var resultImage = await Image.LoadAsync<Rgba32>(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                await Context.Channel.SendFileAsync(memoryStream,
                    resultImage.Frames.Count > 1 ? "result.gif" : "result.png");
                return;
            }

            var imageCacheKey = $"image_{url}";
            var imageCache = await CacheProvider.GetAsync<byte[]>(imageCacheKey);
            Image<Rgba32>? image = null;

            if (imageCache?.Value is not { Length: > 0 })
            {
                Logger.LogDebug("Downloading image from {url}", url);
                var httpClient = HttpClientFactory.CreateClient();
                await using var stream = await httpClient.GetStreamAsync(url);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                await CacheProvider.SetAsync(imageCacheKey, memoryStream.ToArray(), TimeSpan.FromMinutes(5));
                memoryStream.Seek(0, SeekOrigin.Begin);
                image = await Image.LoadAsync<Rgba32>(memoryStream);
                Logger.LogDebug("Saved image to cache for {url}", url);
            }
            else
            {
                Logger.LogDebug("Found image cache for {url}", url);
            }

            if (image is not null)
            {
                Logger.LogDebug("Processing image for {url}", url);
                await using var processor = new ImageProcessor<Rgba32>(image);
                processor.AddStep(new InvertColor<Rgba32>());
                using var result = await processor.ApplyStepsAsync();
                using var memoryStream = new MemoryStream();
                if (result.Frames.Count > 1)
                {
                    result.FixGifRepeatCount();
                    await result.SaveAsGifAsync(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var bytes = memoryStream.ToArray();
                    await CacheProvider.SetAsync(resultCacheKey, bytes, TimeSpan.FromMinutes(5));
                    await Context.Channel.SendFileAsync(memoryStream, "result.gif");
                }
                else
                {
                    await result.SaveAsPngAsync(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var bytes = memoryStream.ToArray();
                    await CacheProvider.SetAsync(resultCacheKey, bytes, TimeSpan.FromMinutes(5));
                    await Context.Channel.SendFileAsync(memoryStream, "result.png");
                }

                Logger.LogDebug("Saved result to cache for {url}", url);
            }
        }
    }
}