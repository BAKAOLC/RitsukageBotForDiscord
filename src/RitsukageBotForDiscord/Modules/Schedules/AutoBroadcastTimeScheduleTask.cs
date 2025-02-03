using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Schedules
{
    internal class AutoBroadcastTimeScheduleTask : PeriodicScheduleTask
    {
        private readonly Dictionary<DateTimeOffset, string> _broadcastTimes = [];

        private readonly ChatClientProviderService _chatClientProviderService;

        private readonly IConfiguration _configuration;

        private readonly DatabaseProviderService _databaseProviderService;

        private readonly DiscordSocketClient _discordClient;

        private readonly ILogger<AutoBroadcastTimeScheduleTask> _logger;

        private DateTimeOffset? _generatingTime;

        public AutoBroadcastTimeScheduleTask(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _chatClientProviderService = serviceProvider.GetRequiredService<ChatClientProviderService>();
            _configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _databaseProviderService = serviceProvider.GetRequiredService<DatabaseProviderService>();
            _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
            _logger = serviceProvider.GetRequiredService<ILogger<AutoBroadcastTimeScheduleTask>>();
            {
                var enabled = _configuration.GetValue<bool>("AI:Function:TimeBroadcast:Enabled");
                Configuration = new PeriodicScheduleConfiguration
                {
                    IsEnabled = enabled,
                    Interval = TimeSpan.FromMinutes(1),
                };
            }
        }

        public override ScheduleConfigurationBase Configuration { get; }

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!_chatClientProviderService.IsEnabled()) return;
            var prompt = _configuration.GetValue<string>("AI:Function:TimeBroadcast:Prompt");
            if (string.IsNullOrWhiteSpace(prompt)) return;
            var now = DateTimeOffset.Now;
            now = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
            if (_broadcastTimes.Remove(now, out var message)) await BroadcastTime(message).ConfigureAwait(false);

            if (_generatingTime is not null) return;
            var nextHour = now.AddHours(1);
            if (_broadcastTimes.ContainsKey(nextHour)) return;
            _generatingTime = nextHour;
            await GenerateTimeMessage(nextHour, prompt).ConfigureAwait(false);
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

        private async Task GenerateTimeMessage(DateTimeOffset targetTime, string prompt)
        {
            var messageList = new List<ChatMessage>();
            if (_chatClientProviderService.GetRoleData() is { } roleData)
                messageList.Add(roleData);
            var message = CreateTimeMessageRequireMessage(targetTime, prompt);
            messageList.Add(message);
            _logger.LogInformation("Generating time message for {TargetTime}", targetTime);

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
                            _logger.LogError(x.Exception,
                                "Error occurred while generating time message for {TargetTime}",
                                targetTime);
                        }, cancellationTokenSource2.Token);
                }

                while (!isCompleted && !isError) await Task.Delay(1000).ConfigureAwait(false);

                if (!isCompleted)
                {
                    if (DateTimeOffset.Now > targetTime)
                    {
                        _logger.LogWarning("Failed to generate time message for {TargetTime}", targetTime);
                        break;
                    }

                    continue;
                }

                var (_, content, _) = ChatClientProviderService.FormatResponse(sb.ToString());
                _logger.LogInformation("Generated time message for {TargetTime} with content:\n{Content}", targetTime,
                    content);
                _broadcastTimes.Add(targetTime, content);
                break;
            }
        }


        private static ChatMessage CreateTimeMessageRequireMessage(DateTimeOffset targetTime, string prompt)
        {
            var jObject = new JObject
            {
                ["name"] = "##SYSTEM##",
                ["message"] = prompt,
                ["data"] = new JObject
                {
                    ["time"] = targetTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                },
            };
            return new(ChatRole.User, jObject.ToString());
        }
    }
}