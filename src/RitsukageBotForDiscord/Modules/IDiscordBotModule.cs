namespace RitsukageBot.Modules
{
    internal interface IDiscordBotModule : IDisposable, IAsyncDisposable
    {
        public Task InitAsync();

        public Task ReInitAsync();
    }
}