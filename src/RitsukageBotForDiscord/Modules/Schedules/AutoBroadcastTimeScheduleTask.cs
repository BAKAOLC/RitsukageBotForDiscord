using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Library.OpenApi;
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

            var message = await CreateTimeMessageRequireMessage(targetTime, prompt).ConfigureAwait(false);
            if (message is null) return;
            messageList.Add(message);
            _logger.LogInformation("Generating time message for {TargetTime} with role: {Role}", targetTime, role);

            var endpointConfig = _chatClientProviderService.GetFirstChatEndpoint();
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
                    var chatClient = _chatClientProviderService.GetChatClient(endpointConfig);
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
                            await foreach (var response in chatClient.CompleteStreamingAsync(messageList,
                                               new() { Temperature = temperature, MaxOutputTokens = 8192 },
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
                            isError = true;
                            cancellationTokenSource1.Cancel();
                            _logger.LogError(x.Exception,
                                "Error occurred while generating time message for {TargetTime} with {ModelId} from {Url} with role: {Role}",
                                targetTime, chatClient.Metadata.ModelId, chatClient.Metadata.ProviderUri, role);
                        }, cancellationTokenSource2.Token);
                }

                while (!isCompleted && !isError) await Task.Delay(1000).ConfigureAwait(false);

                var (_, content, _, thinkContent) = ChatClientProviderService.FormatResponse(sb.ToString());
                if (!isCompleted || string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning(
                        "Failed to generate time message for {TargetTime} with {ModelId} from {Url} with role: {Role}",
                        targetTime, endpointConfig.ModelId, endpointConfig.Endpoint, role);
                    if (DateTimeOffset.Now > targetTime) break;
                    messageList.Remove(message);
                    message = await CreateTimeMessageRequireMessage(targetTime, prompt).ConfigureAwait(false);
                    if (message is null) break;
                    messageList.Add(message);
                    var endpointConfigs = _chatClientProviderService.GetEndpointConfigs();
                    if (endpointConfigs.Length > 1)
                    {
                        var currentEndpoint = endpointConfig;
                        var otherClients = endpointConfigs.Where(x => x != currentEndpoint).ToArray();
                        if (otherClients.Length > 0)
                            endpointConfig = otherClients[Random.Shared.Next(otherClients.Length)];
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(thinkContent))
                    _logger.LogInformation(
                        "Think content for time message for {TargetTime} with {ModelId} from {Url} with role: {Role}:\n{Content}",
                        targetTime, endpointConfig.ModelId, endpointConfig.Endpoint, role,
                        thinkContent);
                _logger.LogInformation(
                    "Generated time message for {TargetTime} with {ModelId} from {Url} with role: {Role}:\n{Content}",
                    targetTime, endpointConfig.ModelId, endpointConfig.Endpoint, role, content);
                content = $"|| Generated by {endpointConfig.GetName()} with role: {role} ||\n{content}";
                _broadcastTimes.Add(targetTime, content);
                await _cacheProvider.SetAsync(cacheKey, content, options =>
                {
                    options.Duration = TimeSpan.FromDays(1);
                    options.FailSafeMaxDuration = TimeSpan.FromDays(2);
                }).ConfigureAwait(false);
                _logger.LogInformation("Cached time message for {TargetTime}", targetTime);
                break;
            }
        }

        private async Task<ChatMessage?> CreateTimeMessageRequireMessage(DateTimeOffset targetTime, string prompt)
        {
            var time = targetTime.ToOffset(TimeSpan.FromHours(8));
            var year = time.Year.ToString();
            var month = time.Month.ToString();
            var day = time.Day.ToString();
            var days = await OpenApi.GetCalendarAsync(time).ConfigureAwait(false);
            var today = days.FirstOrDefault(x => x.Year == year && x.Month == month && x.Day == day);
            var holiday = today?.FestivalInfoList is { Length: > 0 }
                ? string.Join(", ", today.FestivalInfoList.Select(x => x.Name))
                : "今日无节日";
            var workday = today?.Status switch
            {
                BaiduCalendarDayStatus.Holiday => "假期",
                BaiduCalendarDayStatus.Normal when today.CnDay is "六" or "日" => "假期",
                BaiduCalendarDayStatus.Workday => "工作日",
                BaiduCalendarDayStatus.Normal => "工作日",
                _ => "未知",
            };
            var todayString = today is not null
                ? $"""
                   北京时间：{time.Year}-{time.Month}-{time.Day} 星期{today.CnDay} {time.Hour}:{time.Minute}
                   农历：{today.LunarYear}年{today.LMonth}月{today.LDate}日
                   节日：{holiday}
                   今天是{workday}
                   宜：{today.Suit}
                   忌：{today.Avoid}
                   """
                : string.Empty;
            return await _chatClientProviderService.BuildUserChatMessage("##SYSTEM##", null, targetTime, prompt, new()
            {
                ["randomSeed"] = Random.Shared.Next(),
                ["calendar"] = todayString,
            }).ConfigureAwait(false);
        }
    }
}