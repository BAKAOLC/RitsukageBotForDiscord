﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Modules;
using RitsukageBot.Library.Modules.ModuleSupports;

namespace RitsukageBot.Services.HostedServices
{
    internal class DiscordBotService : IHostedService
    {
        private readonly DiscordSocketClient _client;

        private readonly DiscordEventLoggerModule _discordEventLoggerModule;
        private readonly EventModuleSupport _eventModuleSupport;
        private readonly ILogger<DiscordBotService> _logger;
        private readonly ScheduleModuleSupport _scheduleModuleSupport;
        private readonly ScriptingModuleSupport _scriptingModuleSupport;
        private readonly IServiceProvider _services;

        public DiscordBotService(ILogger<DiscordBotService> logger,
            IServiceProvider serviceProvider,
            DiscordSocketClient client)
        {
            _logger = logger;
            _services = serviceProvider;
            _client = client;
            _client.Log += LogAsync;
            _discordEventLoggerModule = new(serviceProvider);
            _scriptingModuleSupport = new(this, serviceProvider);
            _eventModuleSupport = new(serviceProvider);
            _scheduleModuleSupport = new(serviceProvider);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Discord bot service...");
            var token = _services.GetRequiredService<IConfiguration>().GetValue<string>("Discord:Token");
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogError("Discord bot token is not configured");
                return;
            }

            await _discordEventLoggerModule.InitAsync().ConfigureAwait(false);
            await _scriptingModuleSupport.InitAsync().ConfigureAwait(false);
            await _eventModuleSupport.InitAsync().ConfigureAwait(false);
            await _scheduleModuleSupport.InitAsync().ConfigureAwait(false);
            await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
            _logger.LogInformation("Discord bot service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Discord bot service...");
            await _scheduleModuleSupport.DisposeAsync().ConfigureAwait(false);
            await _eventModuleSupport.DisposeAsync().ConfigureAwait(false);
            await _scriptingModuleSupport.DisposeAsync().ConfigureAwait(false);
            await _discordEventLoggerModule.DisposeAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Discord bot service stopped");
        }

        public Task LogAsync(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug("{Source}: {Message}", message.Source, message.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation("{Source}: {Message}", message.Source, message.Message);
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(message.Exception, "{Source}: {Message}", message.Source, message.Message);
                    break;
                case LogSeverity.Error:
                    _logger.LogError(message.Exception, "{Source}: {Message}", message.Source, message.Message);
                    break;
                case LogSeverity.Critical:
                    _logger.LogCritical(message.Exception, "{Source}: {Message}", message.Source, message.Message);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}