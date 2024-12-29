using Discord.Commands;
using Discord.WebSocket;

namespace RitsukageBot.Modules.Command
{
    /// <summary>
    ///     Sample command module
    /// </summary>
    public class SampleCommandModule : ModuleBase<SocketCommandContext>
    {
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
        public Task EchoAsync([Remainder][Summary("The text to echo")] string message)
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
    }
}