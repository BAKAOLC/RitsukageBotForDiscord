using System.Reflection;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoSmart.Caching.Sqlite;
using NLog.Extensions.Logging;
using RitsukageBot.Library.Networking;
using RitsukageBot.Library.OpenApi;
using RitsukageBot.Library.Utils;
using RitsukageBot.Services.HostedServices;
using RitsukageBot.Services.Providers;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using RunMode = Discord.Commands.RunMode;

Console.Title = "Ritsukage Bot";

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

using var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration(configure =>
    {
        configure.SetBasePath(Directory.GetCurrentDirectory());
        configure.AddJsonFile("appsettings.json", true, true);
        configure.AddJsonFile("appsettings.runtime.json", true, true);
        configure.AddEnvironmentVariables();
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
        {
            var cachePath = context.Configuration.GetValue<string>("Cache");
            if (!string.IsNullOrWhiteSpace(cachePath))
                services.AddSingleton<IDistributedCache, SqliteCache>(_ => new(new() { CachePath = cachePath }));
            else
                services.AddDistributedMemoryCache();
            var fusionCache = services.AddFusionCache();
            fusionCache.WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(5);
                options.FailSafeActivationLogLevel = LogLevel.Debug;
                options.SerializationErrorsLogLevel = LogLevel.Warning;
                options.DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.DistributedCacheErrorsLogLevel = LogLevel.Error;
                options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.FactoryErrorsLogLevel = LogLevel.Error;
            });
            fusionCache.WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),
                FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(1500),
                DistributedCacheSoftTimeout = TimeSpan.FromSeconds(1),
                DistributedCacheHardTimeout = TimeSpan.FromSeconds(5),
                AllowBackgroundDistributedCacheOperations = true,
                JitterMaxDuration = TimeSpan.FromSeconds(5),
            });
            fusionCache.WithSerializer(new FusionCacheNewtonsoftJsonSerializer());
            fusionCache.WithDistributedCache(x => x.GetRequiredService<IDistributedCache>());
        }
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.AddSingleton<DatabaseProviderService>();
        services.AddSingleton<GitHubClientProviderService>();
        services.AddSingleton<ImageCacheProviderService>();
        services.AddSingleton<BiliKernelProviderService>();
        services.AddSingleton<GoogleSearchProviderService>();
        services.AddSingleton<QWeatherProviderService>();
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
        services.AddSingleton<ChatClientProviderService>();
#if !DEBUG // Auto update service is not needed in debug mode.
        services.AddHostedService<AutoUpdateService>();
#endif
    }).Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Ritsukage Bot...");
logger.LogInformation("Assembly User-Agent: {UserAgent}", UserAgent.AssemblyUserAgent);
logger.LogInformation("Network default User-Agent: {UserAgent}", UserAgent.Default);

NetworkUtility.SetHttpClientFactory(host.Services.GetRequiredService<IHttpClientFactory>());
OpenApi.SetCacheProvider(host.Services.GetRequiredService<IFusionCache>());

await host.RunAsync(HostCancellationToken.Token).ConfigureAwait(false);