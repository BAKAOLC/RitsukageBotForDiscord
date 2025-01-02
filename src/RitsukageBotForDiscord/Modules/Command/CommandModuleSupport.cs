using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Services;

namespace RitsukageBot.Modules.Command
{
    internal sealed class CommandModuleSupport(DiscordBotService discordBotService, IServiceProvider services) : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly CommandService _command = services.GetRequiredService<CommandService>();
        private readonly ILogger<CommandModuleSupport> _logger = services.GetRequiredService<ILogger<CommandModuleSupport>>();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public Task InitAsync()
        {
            _command.Log += discordBotService.LogAsync;
            _client.MessageReceived += HandleCommandAsync;
            return _command.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        internal Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (messageParam is not SocketUserMessage message) return Task.CompletedTask;
            var argPos = 0;
            if (!message.HasCharPrefix('!', ref argPos) || message.Author.IsBot) return Task.CompletedTask;
            var context = new SocketCommandContext(_client, message);
            _logger.LogInformation("User {UserId} executed command {Command}", context.User.Id, context.Message.Content);
            return _command.ExecuteAsync(context, argPos, services);
        }

        ~CommandModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            foreach (var module in _command.Modules)
            {
                _command.RemoveModuleAsync(module);
            }

            _client.MessageReceived -= HandleCommandAsync;
            _command.Log -= discordBotService.LogAsync;
        }

        private async ValueTask DisposeAsyncCore()
        {
            foreach (var module in _command.Modules)
            {
                await _command.RemoveModuleAsync(module).ConfigureAwait(false);
            }

            _client.MessageReceived -= HandleCommandAsync;
            _command.Log -= discordBotService.LogAsync;
        }
    }
}