using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Modules.Schedules
{
    /// <summary>
    ///     Dynamic AI scheduled task
    /// </summary>
    public class AiPromptScheduleTask : ScheduleTask
    {
        private readonly AiScheduledTask _taskData;
        private readonly ILogger<AiPromptScheduleTask> _logger;
        private readonly ChatClientProviderService _chatClientProvider;
        private readonly DatabaseProviderService _databaseProvider;
        private readonly DiscordSocketClient _discordClient;
        private readonly ScheduleConfigurationBase _configuration;

        public AiPromptScheduleTask(IServiceProvider serviceProvider, AiScheduledTask taskData) : base(serviceProvider)
        {
            _taskData = taskData;
            _logger = serviceProvider.GetRequiredService<ILogger<AiPromptScheduleTask>>();
            _chatClientProvider = serviceProvider.GetRequiredService<ChatClientProviderService>();
            _databaseProvider = serviceProvider.GetRequiredService<DatabaseProviderService>();
            _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
            
            _configuration = CreateConfiguration(taskData);
        }

        public override ScheduleConfigurationBase Configuration => _configuration;

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing AI scheduled task: {TaskName} ({TaskId})", _taskData.Name, _taskData.Id);

                if (!_chatClientProvider.IsEnabled())
                {
                    _logger.LogWarning("AI chat client is not enabled, skipping task: {TaskName}", _taskData.Name);
                    return;
                }

                // Build message list for AI
                var messageList = new List<ChatMessage>();
                var temperature = 1.0f; // Default temperature
                
                // Add role data if specified
                if (!string.IsNullOrWhiteSpace(_taskData.AiRole) && 
                    _chatClientProvider.GetRoleData(out var roleData, out temperature, _taskData.AiRole))
                {
                    messageList.Add(roleData);
                }

                // Create user message with the prompt
                var userMessage = await _chatClientProvider.BuildUserChatMessage(
                    "##SCHEDULED_TASK##", 
                    _taskData.UserId, 
                    DateTimeOffset.UtcNow, 
                    _taskData.Prompt
                ).ConfigureAwait(false);

                if (userMessage is null)
                {
                    _logger.LogError("Failed to build user chat message for task: {TaskName}", _taskData.Name);
                    return;
                }

                messageList.Add(userMessage);

                // Get AI response
                var endpointConfig = _chatClientProvider.GetFirstChatEndpoint();
                var chatClient = _chatClientProvider.GetChatClient(endpointConfig);
                
                var response = await chatClient.CompleteAsync(messageList, new ChatOptions
                {
                    Temperature = temperature,
                    MaxOutputTokens = 2048
                }, cancellationToken).ConfigureAwait(false);

                var responseContent = response.Message.Text;
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("AI response was empty for task: {TaskName}", _taskData.Name);
                    return;
                }

                // Send response to designated channel or DM
                if (_taskData.ChannelId.HasValue)
                {
                    var channel = _discordClient.GetChannel(_taskData.ChannelId.Value) as ISocketMessageChannel;
                    if (channel is not null)
                    {
                        var formattedMessage = $"**Scheduled AI Task: {_taskData.Name}**\n{responseContent}";
                        await channel.SendMessageAsync(formattedMessage).ConfigureAwait(false);
                        _logger.LogInformation("Sent AI task response to channel {ChannelId}", _taskData.ChannelId.Value);
                    }
                    else
                    {
                        _logger.LogWarning("Channel {ChannelId} not found for task: {TaskName}", _taskData.ChannelId.Value, _taskData.Name);
                    }
                }
                else
                {
                    // Send as DM to the user who created the task
                    var user = _discordClient.GetUser(_taskData.UserId);
                    if (user is not null)
                    {
                        try
                        {
                            var formattedMessage = $"**Scheduled AI Task: {_taskData.Name}**\n{responseContent}";
                            await user.SendMessageAsync(formattedMessage).ConfigureAwait(false);
                            _logger.LogInformation("Sent AI task response via DM to user {UserId}", _taskData.UserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send DM to user {UserId} for task: {TaskName}", _taskData.UserId, _taskData.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("User {UserId} not found for task: {TaskName}", _taskData.UserId, _taskData.Name);
                    }
                }

                // Update task data
                _taskData.LastExecutedTime = DateTimeOffset.UtcNow;
                _taskData.ExecutedTimes++;
                _taskData.UpdatedTime = DateTimeOffset.UtcNow;

                // Check if task should be finished
                if (ShouldFinishTask())
                {
                    _taskData.IsFinished = true;
                    _logger.LogInformation("Task {TaskName} marked as finished", _taskData.Name);
                }

                // Save to database
                await _databaseProvider.InsertOrUpdateAsync(_taskData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI scheduled task: {TaskName} ({TaskId})", _taskData.Name, _taskData.Id);
            }
        }

        private ScheduleConfigurationBase CreateConfiguration(AiScheduledTask taskData)
        {
            return taskData.ScheduleType switch
            {
                "OneTime" => new OneTimeScheduleConfiguration
                {
                    Guid = Guid.Parse(taskData.Id),
                    IsEnabled = taskData.IsEnabled,
                    IsFinished = taskData.IsFinished,
                    ScheduleTime = taskData.ScheduleTime,
                    LastExecutedTime = taskData.LastExecutedTime ?? DateTimeOffset.MinValue,
                    ExecutedTimes = taskData.ExecutedTimes
                },
                "Periodic" => new PeriodicScheduleConfiguration
                {
                    Guid = Guid.Parse(taskData.Id),
                    IsEnabled = taskData.IsEnabled,
                    IsFinished = taskData.IsFinished,
                    ScheduleTime = taskData.ScheduleTime,
                    LastExecutedTime = taskData.LastExecutedTime ?? DateTimeOffset.MinValue,
                    ExecutedTimes = taskData.ExecutedTimes,
                    Interval = taskData.IntervalSeconds.HasValue ? TimeSpan.FromSeconds(taskData.IntervalSeconds.Value) : TimeSpan.FromHours(1)
                },
                "Countdown" => new CountdownScheduleConfiguration
                {
                    Guid = Guid.Parse(taskData.Id),
                    IsEnabled = taskData.IsEnabled,
                    IsFinished = taskData.IsFinished,
                    ScheduleTime = taskData.ScheduleTime,
                    LastExecutedTime = taskData.LastExecutedTime ?? DateTimeOffset.MinValue,
                    ExecutedTimes = taskData.ExecutedTimes,
                    Interval = taskData.IntervalSeconds.HasValue ? TimeSpan.FromSeconds(taskData.IntervalSeconds.Value) : TimeSpan.FromHours(1),
                    TargetTimes = taskData.TargetTimes ?? 1
                },
                "UntilTime" => new UntilTimeScheduleConfiguration
                {
                    Guid = Guid.Parse(taskData.Id),
                    IsEnabled = taskData.IsEnabled,
                    IsFinished = taskData.IsFinished,
                    ScheduleTime = taskData.ScheduleTime,
                    LastExecutedTime = taskData.LastExecutedTime ?? DateTimeOffset.MinValue,
                    ExecutedTimes = taskData.ExecutedTimes,
                    Interval = taskData.IntervalSeconds.HasValue ? TimeSpan.FromSeconds(taskData.IntervalSeconds.Value) : TimeSpan.FromHours(1),
                    TargetTime = taskData.TargetTime ?? DateTimeOffset.MaxValue,
                    ForceExecuteWhenTargetTime = true
                },
                _ => throw new ArgumentException($"Unknown schedule type: {taskData.ScheduleType}")
            };
        }

        private bool ShouldFinishTask()
        {
            return _taskData.ScheduleType switch
            {
                "OneTime" => true,
                "Countdown" => _taskData.TargetTimes.HasValue && _taskData.ExecutedTimes >= _taskData.TargetTimes.Value,
                "UntilTime" => _taskData.TargetTime.HasValue && DateTimeOffset.UtcNow >= _taskData.TargetTime.Value,
                _ => false
            };
        }
    }
}