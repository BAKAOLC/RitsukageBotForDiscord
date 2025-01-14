namespace RitsukageBot.Library.Modules
{
    /// <summary>
    ///     Discord bot module.
    /// </summary>
    public interface IDiscordBotModule : IDisposable, IAsyncDisposable
    {
        /// <summary>
        ///     Initialize the module.
        /// </summary>
        /// <returns></returns>
        public Task InitAsync();

        /// <summary>
        ///     Reinitialize the module.
        /// </summary>
        /// <returns></returns>
        public Task ReInitAsync();
    }
}