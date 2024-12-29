using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace RitsukageBot.Services
{
    internal class DiscordBotService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _command;
        private readonly InteractionService _interaction;
        private readonly ILogger<DiscordBotService> _logger;
        private readonly IServiceProvider _services;

        public DiscordBotService(ILogger<DiscordBotService> logger,
            IServiceProvider serviceProvider,
            DiscordSocketClient client,
            CommandService commandService,
            InteractionService interaction)
        {
            _logger = logger;
            _services = serviceProvider;
            _client = client;
            _command = commandService;
            _interaction = interaction;
            _client.Log += LogAsync;
            _command.Log += LogAsync;
            _interaction.Log += LogAsync;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Discord bot service...");
            var token = _services.GetRequiredService<IConfiguration>().GetValue<string>("Discord:Token");
            await InitMessageLoggerAsync().ConfigureAwait(false);
            await InitCommandsAsync().ConfigureAwait(false);
            await InitInteractionsAsync().ConfigureAwait(false);
            await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
            _logger.LogInformation("Discord bot service started.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Discord bot service...");
            _client.Dispose();
            _logger.LogInformation("Discord bot service stopped.");
            return Task.CompletedTask;
        }

        internal Task LogAsync(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug("{source}: {message}", message.Source, message.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation("{source}: {message}", message.Source, message.Message);
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(message.Exception, "{source}: {message}", message.Source, message.Message);
                    break;
                case LogSeverity.Error:
                    _logger.LogError(message.Exception, "{source}: {message}", message.Source, message.Message);
                    break;
                case LogSeverity.Critical:
                    _logger.LogCritical(message.Exception, "{source}: {message}", message.Source, message.Message);
                    break;
            }

            return Task.CompletedTask;
        }

        internal Task InitMessageLoggerAsync()
        {
            _client.MessageReceived += LogMessageAsync;
            return Task.CompletedTask;
        }

        internal Task LogMessageAsync(SocketMessage message)
        {
            if (message is not SocketUserMessage userMessage)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("{username}#{discriminator}: {message}", userMessage.Author.Username,
                userMessage.Author.Discriminator, userMessage.Content);
            return Task.CompletedTask;
        }

        internal async Task InitCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            await _command.AddModulesAsync(Assembly.GetEntryAssembly(), _services).ConfigureAwait(false);
        }

        internal async Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (messageParam is not SocketUserMessage message)
            {
                return;
            }

            var argPos = 0;

            if (!message.HasCharPrefix('!', ref argPos) || message.Author.IsBot)
            {
                return;
            }

            _logger.LogInformation("Command received: {command}", message.Content);
            var context = new SocketCommandContext(_client, message);
            await _command.ExecuteAsync(context, argPos, _services).ConfigureAwait(false);
        }

        internal async Task InitInteractionsAsync()
        {
            _client.InteractionCreated += HandleInteractionAsync;
            _client.Ready += RegisterCommandsAsync;
            await _interaction.AddModulesAsync(Assembly.GetEntryAssembly(), _services).ConfigureAwait(false);
        }

        internal async Task RegisterCommandsAsync()
        {
            await _interaction.RegisterCommandsGloballyAsync().ConfigureAwait(false);
        }

        internal async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interaction.ExecuteCommandAsync(context, _services).ConfigureAwait(false);
        }
    }
}