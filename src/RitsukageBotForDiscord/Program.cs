﻿using System.Reflection;
using CacheTower.Serializers.NewtonsoftJson;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using RitsukageBot.Library.Networking;
using RitsukageBot.Library.Utils;
using RitsukageBot.Options;
using RitsukageBot.Services.HostedServices;
using RitsukageBot.Services.Providers;
using RunMode = Discord.Commands.RunMode;

Console.Title = "Ritsukage Bot";

using var host = Host.CreateDefaultBuilder()
    .ConfigureHostConfiguration(configure =>
    {
        configure.SetBasePath(Directory.GetCurrentDirectory());
        configure.AddJsonFile("appsettings.json", true);
    })
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddNLog();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<UnhandledExceptionHandlerService>();
        services.AddOptions();
        services.AddHttpClient().ConfigureHttpClientDefaults(x =>
            x.ConfigureHttpClient(y => y.DefaultRequestHeaders.Add("User-Agent", UserAgent.Default)));
        services.AddCacheStack(builder =>
        {
            var provider = context.Configuration.GetSection("Cache").Get<CacheOption>();
            if (provider is null || provider.CacheProvider.Length == 0)
                throw new InvalidOperationException("Cache provider is not set.");

            foreach (var cacheLayerOption in provider.CacheProvider)
                switch (cacheLayerOption.Type.ToLower())
                {
                    case "memory":
                        builder.AddMemoryCacheLayer();
                        break;
                    case "file":
                        builder.AddFileCacheLayer(new(cacheLayerOption.Path, new NewtonsoftJsonCacheSerializer(new()),
                            TimeSpan.FromMilliseconds(cacheLayerOption.SaveInterval)));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown cache provider: {cacheLayerOption.Type}");
                }

            builder.WithCleanupFrequency(TimeSpan.FromMilliseconds(provider.CleanUpFrequency));
        });
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.AddSingleton<DatabaseProviderService>();
        services.AddSingleton<GitHubClientProviderService>();
        services.AddSingleton<ImageCacheProviderService>();
        services.AddSingleton<BiliKernelProviderService>();
        services.AddSingleton<DiscordSocketConfig>(_ => new()
        {
            LogLevel = LogSeverity.Info,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.All
                             // Current bot does not need these intents:
                             & ~GatewayIntents.GuildPresences
                             & ~GatewayIntents.GuildScheduledEvents
                             & ~GatewayIntents.GuildInvites,
        });
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<CommandServiceConfig>(_ => new()
        {
            LogLevel = LogSeverity.Info,
            CaseSensitiveCommands = false,
            DefaultRunMode = RunMode.Async,
        });
        services.AddSingleton<CommandService>();
        services.AddSingleton<InteractionServiceConfig>(_ => new()
        {
            LogLevel = LogSeverity.Info,
            DefaultRunMode = Discord.Interactions.RunMode.Async,
        });
        services.AddSingleton<InteractionService>(x => new(x.GetRequiredService<DiscordSocketClient>(),
            x.GetRequiredService<InteractionServiceConfig>()));
        services.AddHostedService<DiscordBotService>();
#if !DEBUG // Auto update service is not needed in debug mode.
        services.AddHostedService<AutoUpdateService>();
#endif
    }).Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Ritsukage Bot...");
logger.LogInformation("Assembly User-Agent: {UserAgent}", UserAgent.AssemblyUserAgent);
logger.LogInformation("Network default User-Agent: {UserAgent}", UserAgent.Default);

NetworkUtility.SetHttpClientFactory(host.Services.GetRequiredService<IHttpClientFactory>());

await host.RunAsync().ConfigureAwait(false);