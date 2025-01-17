using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RitsukageBot.Library.Modules.Events;

namespace RitsukageBot.Library.Modules.ModuleSupports
{
    internal sealed class EventModuleSupport(IServiceProvider services)
        : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly IServiceScopeFactory _serviceScope = services.GetRequiredService<IServiceScopeFactory>();
        private CancellationTokenSource _cancellationTokenSource = new();

        private IMediator Mediator
        {
            get
            {
                var scope = _serviceScope.CreateScope();
                return scope.ServiceProvider.GetRequiredService<IMediator>();
            }
        }

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
            _cancellationTokenSource = new();
            _client.MessageReceived += HandleMessageAsync;
            return Task.CompletedTask;
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        private Task HandleMessageAsync(SocketMessage message)
        {
            return Mediator.Publish(new MessageNotification(services, message), _cancellationTokenSource.Token);
        }

        ~EventModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _client.MessageReceived -= HandleMessageAsync;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        private async ValueTask DisposeAsyncCore()
        {
            _client.MessageReceived -= HandleMessageAsync;
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            _cancellationTokenSource.Dispose();
        }
    }
}