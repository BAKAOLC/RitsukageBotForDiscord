using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RitsukageBot.Services;

namespace RitsukageBot.Modules.Interaction
{
    internal sealed class InteractionModuleSupport(DiscordBotService discordBotService, IServiceProvider services) : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly InteractionService _interaction = services.GetRequiredService<InteractionService>();

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

        public async Task InitAsync()
        {
            _client.InteractionCreated += HandleInteractionAsync;
            _client.Ready += RegisterCommandsAsync;
            _interaction.Log += discordBotService.LogAsync;
            await _interaction.AddModulesAsync(Assembly.GetEntryAssembly(), services);
            if (_client.ConnectionState == ConnectionState.Connected)
                await RegisterCommandsAsync();
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        internal Task RegisterCommandsAsync()
        {
            return _interaction.RegisterCommandsGloballyAsync();
        }

        internal Task HandleInteractionAsync(SocketInteraction interaction1)
        {
            var context = new SocketInteractionContext(_client, interaction1);
            return _interaction.ExecuteCommandAsync(context, services);
        }

        ~InteractionModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            foreach (var module in _interaction.Modules)
            {
                _interaction.RemoveModuleAsync(module);
            }

            _client.InteractionCreated -= HandleInteractionAsync;
            _client.Ready -= RegisterCommandsAsync;
            _interaction.Log -= discordBotService.LogAsync;
        }

        private async ValueTask DisposeAsyncCore()
        {
            foreach (var module in _interaction.Modules)
            {
                await _interaction.RemoveModuleAsync(module).ConfigureAwait(false);
            }

            _client.InteractionCreated -= HandleInteractionAsync;
            _client.Ready -= RegisterCommandsAsync;
            _interaction.Log -= discordBotService.LogAsync;
        }
    }
}