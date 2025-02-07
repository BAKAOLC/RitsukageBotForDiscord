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
using ZiggyCreatures.Caching.Fusion;

namespace RitsukageBot.Modules.Schedules
{
    internal class AutoBroadcastTimeScheduleTask : PeriodicScheduleTask
    {
        private readonly Dictionary<DateTimeOffset, string> _broadcastTimes = [];

        private readonly IFusionCache _cacheProvider;

        private readonly ChatClientProviderService _chatClientProviderService;

        private readonly IConfiguration _configuration;

        private readonly DatabaseProviderService _databaseProviderService;

        private readonly DiscordSocketClient _discordClient;

        private readonly ILogger<AutoBroadcastTimeScheduleTask> _logger;

        private DateTimeOffset? _generatingTime;

        public AutoBroadcastTimeScheduleTask(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _cacheProvider = serviceProvider.GetRequiredService<IFusionCache>();
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
            var modelIds = _chatClientProviderService.GetModels();
            if (modelIds.Length == 0) return;

            var prompt = _configuration.GetValue<string>("AI:Function:TimeBroadcast:Prompt");
            if (string.IsNullOrWhiteSpace(prompt)) return;
            var now = DateTimeOffset.Now;
            now = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
            if (_broadcastTimes.Remove(now, out var message)) await BroadcastTime(message).ConfigureAwait(false);

            if (_generatingTime is not null) return;
            // Generate for next 2 hours
            var nextHour = now.AddHours(1);
            if (_broadcastTimes.ContainsKey(nextHour))
            {
                nextHour = nextHour.AddHours(1);
                if (_broadcastTimes.ContainsKey(nextHour)) return;
            }

            _generatingTime = nextHour;
            await GenerateTimeMessage(nextHour, prompt, modelIds).ConfigureAwait(false);
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

        private async Task GenerateTimeMessage(DateTimeOffset targetTime, string prompt, string[] modelIds)
        {
            if (modelIds.Length == 0)
            {
                _logger.LogWarning("No model id available for generating time message for {TargetTime}", targetTime);
                return;
            }

            var cacheKey = $"ai_message:time_broadcast:{targetTime.ToUnixTimeSeconds()}";
            var cacheMessage = await _cacheProvider.GetOrDefaultAsync<string>(cacheKey).ConfigureAwait(false);
            if (cacheMessage is not null)
            {
                _broadcastTimes.Add(targetTime, cacheMessage);
                _logger.LogInformation("Generated time message for {TargetTime} from cache, content:\n{Content}",
                    targetTime, cacheMessage);
                return;
            }

            var messageList = new List<ChatMessage>();
            var roles = _chatClientProviderService.GetRoles();
            var role = roles[Random.Shared.Next(roles.Length)];
            if (_chatClientProviderService.GetRoleData(out var roleData, out var temperature, role))
                messageList.Add(roleData);

            var message = CreateTimeMessageRequireMessage(targetTime, prompt);
            messageList.Add(message);
            _logger.LogInformation("Generating time message for {TargetTime} with role: {Role}", targetTime, role);

            var modelId = modelIds[0];
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
                                               option =>
                                               {
                                                   // ReSharper disable once AccessToModifiedClosure
                                                   if (!string.IsNullOrWhiteSpace(modelId))
                                                       // ReSharper disable once AccessToModifiedClosure
                                                       option.ModelId = modelId;
                                                   option.Temperature = temperature;
                                               },
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

                var (_, content, _, thinkContent) = ChatClientProviderService.FormatResponse(sb.ToString());
                if (!isCompleted || string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning(
                        "Failed to generate time message for {TargetTime} with role: {Role} in model: {ModelId}",
                        targetTime, role, modelId);
                    if (DateTimeOffset.Now > targetTime) break;
                    messageList.Remove(message);
                    message = CreateTimeMessageRequireMessage(targetTime, prompt);
                    messageList.Add(message);
                    modelId = modelIds[Random.Shared.Next(modelIds.Length)];
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(thinkContent))
                    _logger.LogInformation(
                        "Think content for {TargetTime} with {ModelId} in role: {Role}:\n{ThinkContent}",
                        targetTime, modelId, role, thinkContent);
                _logger.LogInformation(
                    "Generated time message for {TargetTime} with {ModelId} in role: {Role}, content:\n{Content}",
                    targetTime, modelId, role, content);
                content = $"|| Generated by {modelId} with role: {role} ||\n{content}";
                _broadcastTimes.Add(targetTime, content);
                await _cacheProvider.SetAsync(cacheKey, content, TimeSpan.FromHours(1)).ConfigureAwait(false);
                _logger.LogInformation("Cached time message for {TargetTime}", targetTime);
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
                    ["randomSeed"] = Random.Shared.Next(),
                },
            };
            return new(ChatRole.User, jObject.ToString());
        }
    }
}