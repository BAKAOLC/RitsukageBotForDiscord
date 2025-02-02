using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Schedules
{
    internal class AutoBroadcastTimeScheduleTask(IServiceProvider serviceProvider)
        : PeriodicScheduleTask(serviceProvider)
    {
        private readonly Dictionary<DateTimeOffset, string> _broadcastTimes = [];

        private readonly ChatClientProviderService _chatClientProviderService =
            serviceProvider.GetRequiredService<ChatClientProviderService>();

        private readonly DatabaseProviderService _databaseProviderService =
            serviceProvider.GetRequiredService<DatabaseProviderService>();

        private readonly DiscordSocketClient _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();

        private readonly ILogger<AutoBroadcastTimeScheduleTask> _logger =
            serviceProvider.GetRequiredService<ILogger<AutoBroadcastTimeScheduleTask>>();

        private DateTimeOffset? _generatingTime;

        public override ScheduleConfigurationBase Configuration { get; } = new PeriodicScheduleConfiguration
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(1),
        };

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!_chatClientProviderService.IsEnabled()) return;
            var now = DateTimeOffset.Now;
            now = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
            if (_broadcastTimes.Remove(now, out var message)) await BroadcastTime(message).ConfigureAwait(false);

            if (_generatingTime is not null) return;
            var nextHour = now.AddHours(1);
            if (_broadcastTimes.ContainsKey(nextHour)) return;
            _generatingTime = nextHour;
            await GenerateTimeMessage(nextHour).ConfigureAwait(false);
            _generatingTime = null;
        }

        private async Task BroadcastTime(string message)
        {
            var channels = await _databaseProviderService
                .QueryAsync<DiscordChannelConfiguration>(
                    "SELECT * FROM DiscordChannelConfiguration WHERE automatically_ai_broadcast_time = 1")
                .ConfigureAwait(false);

            foreach (var discordChannel in channels.Select(channel => _discordClient.GetChannel(channel.Id))
                         .OfType<ISocketMessageChannel>())
                await discordChannel.SendMessageAsync(message).ConfigureAwait(false);
        }

        private async Task GenerateTimeMessage(DateTimeOffset targetTime)
        {
            var messageList = new List<ChatMessage>();
            if (_chatClientProviderService.GetRoleData() is { } roleData)
                messageList.Add(roleData);
            var message = CreateTimeMessageRequireMessage(targetTime);
            messageList.Add(message);
            _logger.LogInformation("Generated time message for {TargetTime}", targetTime);

            while (true)
            {
                var haveContent = false;
                var isCompleted = false;
                var isError = false;
                var sb = new StringBuilder();
                var lockObject = new Lock();
                {
                    var cancellationTokenSource1 = new CancellationTokenSource();
                    var cancellationTokenSource2 = new CancellationTokenSource();
                    _ = Task.Delay(TimeSpan.FromMinutes(1), cancellationTokenSource1.Token)
                        .ContinueWith(x =>
                        {
                            if (x.IsFaulted) return;
                            lock (lockObject)
                            {
                                if (haveContent) return;
                                isError = true;
                                cancellationTokenSource2.Cancel();
                            }
                        }, cancellationTokenSource1.Token);
                    _ = Task.Run(async () =>
                        {
                            await foreach (var response in _chatClientProviderService.CompleteStreamingAsync(
                                               messageList,
                                               false,
                                               cancellationTokenSource2.Token))
                                lock (lockObject)
                                {
                                    if (string.IsNullOrWhiteSpace(response.ToString()))
                                        continue;
                                    sb.Append(response);
                                    haveContent = true;
                                }

                            isCompleted = true;
                            await cancellationTokenSource1.CancelAsync().ConfigureAwait(false);
                        }, cancellationTokenSource2.Token)
                        .ContinueWith(x =>
                        {
                            if (!x.IsFaulted) return;
                            cancellationTokenSource1.Cancel();
                            _logger.LogError(x.Exception, "Error while processing the chat with AI tools");
                        }, cancellationTokenSource2.Token);
                }

                while (!isCompleted && !isError) await Task.Delay(1000).ConfigureAwait(false);

                if (!isCompleted) continue;
                _broadcastTimes.Add(targetTime, sb.ToString());
                break;
            }
        }


        private static ChatMessage CreateTimeMessageRequireMessage(DateTimeOffset targetTime)
        {
            var jObject = new JObject
            {
                ["name"] = "##SYSTEM##",
                ["message"] = "请进行一次报时",
                ["data"] = new JObject
                {
                    ["time"] = targetTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                },
            };
            return new(ChatRole.User, jObject.ToString());
        }
    }
}