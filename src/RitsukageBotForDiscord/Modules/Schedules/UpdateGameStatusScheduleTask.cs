using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Modules.Schedules;

namespace RitsukageBot.Modules.Schedules
{
    internal class UpdateGameStatusScheduleTask(IServiceProvider serviceProvider)
        : PeriodicScheduleTask(serviceProvider)
    {
        private readonly IConfiguration _configuration = serviceProvider.GetRequiredService<IConfiguration>();
        private readonly DiscordSocketClient _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();

        private readonly ILogger<UpdateGameStatusScheduleTask> _logger =
            serviceProvider.GetRequiredService<ILogger<UpdateGameStatusScheduleTask>>();

        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(5),
        };

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var statusList = _configuration.GetSection("CustomValues:GameStatus").Get<StatusConfig[]>();
            if (statusList == null || statusList.Length == 0)
            {
                _logger.LogWarning("No game status available");
                return;
            }

            var newStatus = Random.Shared.GetItems(statusList, 1).FirstOrDefault();
            if (newStatus is null)
            {
                _logger.LogWarning("Got empty game status");
                return;
            }

            _logger.LogDebug("Setting game status to {Status}", newStatus);
            var type = newStatus.Type switch
            {
                "Playing" => ActivityType.Playing,
                "Streaming" => ActivityType.Streaming,
                "Listening" => ActivityType.Listening,
                "Watching" => ActivityType.Watching,
                "Competing" => ActivityType.Competing,
                _ => ActivityType.Playing,
            };
            await _discordClient.SetGameAsync(newStatus.Name, type: type).ConfigureAwait(false);
        }

        public record StatusConfig(string Type, string Name);
    }
}