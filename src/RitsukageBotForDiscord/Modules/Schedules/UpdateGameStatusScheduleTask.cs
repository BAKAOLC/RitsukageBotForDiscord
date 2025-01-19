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
            var statusList = _configuration.GetSection("CustomValues:GameStatus").Get<string[]>();
            if (statusList == null || statusList.Length == 0)
            {
                _logger.LogWarning("No game status available.");
                return;
            }

            var newStatus = Random.Shared.GetItems(statusList, 1).FirstOrDefault();
            if (string.IsNullOrEmpty(newStatus))
            {
                _logger.LogWarning("Got empty game status.");
                return;
            }

            _logger.LogDebug("Setting game status to {Status}.", newStatus);
            await _discordClient.SetGameAsync(newStatus).ConfigureAwait(false);
        }
    }
}