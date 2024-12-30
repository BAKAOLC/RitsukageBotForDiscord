using Discord.Commands;
using Discord.WebSocket;
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
        public IHttpClientFactory HttpClientFactory { get; set; }

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
            [Summary("The image url")] string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) await ReplyAsync("Invalid url");

            var httpClient = HttpClientFactory.CreateClient();
            var stream = await httpClient.GetStreamAsync(url);
            var image = await Image.LoadAsync<Rgba32>(stream);
            await using var processor = new ImageProcessor<Rgba32>(image);
            processor.AddStep(new InvertColor<Rgba32>());
            using var result = await processor.ApplyStepsAsync();
            await using var memoryStream = new MemoryStream();
            if (result.Frames.Count > 1)
            {
                result.FixGifRepeatCount();
                await result.SaveAsGifAsync(memoryStream);
            }
            else
            {
                await result.SaveAsPngAsync(memoryStream);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(memoryStream, "result.png");
        }
    }
}