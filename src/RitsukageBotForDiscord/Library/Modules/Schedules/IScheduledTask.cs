using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Library.Modules.Schedules
{
    /// <summary>
    ///     Scheduled task.
    /// </summary>
    public interface IScheduledTask
    {
        /// <summary>
        ///     Configuration.
        /// </summary>
        ScheduleConfigurationBase Configuration { get; }

        /// <summary>
        ///     Schedule type.
        /// </summary>
        ScheduleType Type => Configuration.ScheduleType;

        /// <summary>
        ///     Services provider.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        ///     Discord client.
        /// </summary>
        DiscordSocketClient Client => GetRequiredService<DiscordSocketClient>();

        /// <summary>
        ///     Database provider.
        /// </summary>
        DatabaseProviderService Database => GetRequiredService<DatabaseProviderService>();

        /// <summary>
        ///     Get service of type T from the <see cref="IServiceProvider" />
        /// </summary>
        /// <typeparam name="T">The type of service object to get.</typeparam>
        /// <returns>A service object of type T.</returns>
        /// <exception cref="InvalidOperationException">There is no service of type T.</exception>
        T GetRequiredService<T>() where T : notnull
        {
            return Services.GetRequiredService<T>();
        }

        /// <summary>
        ///     Execute the task.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}