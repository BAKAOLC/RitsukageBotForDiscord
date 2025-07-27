using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Data;
using RitsukageBot.Library.Modules.Schedules;
using RitsukageBot.Modules.Schedules;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Services.HostedServices
{
    /// <summary>
    ///     AI scheduled task manager service
    /// </summary>
    public class AiScheduledTaskManagerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AiScheduledTaskManagerService> _logger;
        private readonly DatabaseProviderService _databaseProvider;
        private readonly Dictionary<string, AiPromptScheduleTask> _activeTasks = new();
        private readonly object _lock = new();

        public AiScheduledTaskManagerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<AiScheduledTaskManagerService>>();
            _databaseProvider = serviceProvider.GetRequiredService<DatabaseProviderService>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AI Scheduled Task Manager Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await LoadAndExecuteTasksAsync(stoppingToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AI scheduled task manager");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("AI Scheduled Task Manager Service stopped");
        }

        private async Task LoadAndExecuteTasksAsync(CancellationToken cancellationToken)
        {
            // Load all active tasks from database
            var allTasks = await _databaseProvider.QueryAsync<AiScheduledTask>(
                "SELECT * FROM AiScheduledTask WHERE is_enabled = 1 AND is_finished = 0"
            ).ConfigureAwait(false);

            var currentTime = DateTimeOffset.UtcNow;

            lock (_lock)
            {
                // Remove finished tasks from active tasks
                var finishedTaskIds = _activeTasks.Where(kv => kv.Value.Configuration.IsFinished)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var taskId in finishedTaskIds)
                {
                    _activeTasks.Remove(taskId);
                }

                // Add new tasks
                foreach (var taskData in allTasks)
                {
                    if (!_activeTasks.ContainsKey(taskData.Id))
                    {
                        try
                        {
                            var scheduleTask = new AiPromptScheduleTask(_serviceProvider, taskData);
                            _activeTasks[taskData.Id] = scheduleTask;
                            _logger.LogInformation("Loaded AI scheduled task: {TaskName} ({TaskId})", taskData.Name, taskData.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create AI scheduled task: {TaskName} ({TaskId})", taskData.Name, taskData.Id);
                        }
                    }
                }
            }

            // Execute tasks that are due
            var tasksToExecute = new List<AiPromptScheduleTask>();

            lock (_lock)
            {
                foreach (var task in _activeTasks.Values)
                {
                    if (ShouldExecuteTask(task, currentTime))
                    {
                        tasksToExecute.Add(task);
                    }
                }
            }

            // Execute tasks outside of lock to avoid blocking
            foreach (var task in tasksToExecute)
            {
                try
                {
                    await task.TriggerAsync(currentTime, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute AI scheduled task: {TaskId}", task.Configuration.Guid);
                }
            }
        }

        private static bool ShouldExecuteTask(AiPromptScheduleTask task, DateTimeOffset currentTime)
        {
            var config = task.Configuration;
            
            if (!config.IsEnabled || config.IsFinished)
                return false;

            // Check if it's time to execute
            if (currentTime < config.ScheduleTime)
                return false;

            switch (config.ScheduleType)
            {
                case ScheduleType.Once:
                    return config.ExecutedTimes == 0;

                case ScheduleType.Periodic:
                    if (config.ExecutedTimes == 0)
                        return true;
                    
                    if (config is PeriodicScheduleConfiguration periodic)
                    {
                        var timeSinceLastExecution = currentTime - config.LastExecutedTime;
                        return timeSinceLastExecution >= periodic.Interval;
                    }
                    return false;

                case ScheduleType.Countdown:
                    if (config is CountdownScheduleConfiguration countdown)
                    {
                        if (config.ExecutedTimes >= countdown.TargetTimes)
                            return false;
                        
                        if (config.ExecutedTimes == 0)
                            return true;
                        
                        var timeSinceLastCountdown = currentTime - config.LastExecutedTime;
                        return timeSinceLastCountdown >= countdown.Interval;
                    }
                    return false;

                case ScheduleType.UntilTime:
                    if (config is UntilTimeScheduleConfiguration untilTime)
                    {
                        if (currentTime >= untilTime.TargetTime)
                            return false;
                        
                        if (config.ExecutedTimes == 0)
                            return true;
                        
                        var timeSinceLastUntil = currentTime - config.LastExecutedTime;
                        return timeSinceLastUntil >= untilTime.Interval;
                    }
                    return false;

                default:
                    return false;
            }
        }

        public void RefreshTasks()
        {
            lock (_lock)
            {
                _activeTasks.Clear();
            }
            _logger.LogInformation("AI scheduled tasks refreshed");
        }

        public int GetActiveTaskCount()
        {
            lock (_lock)
            {
                return _activeTasks.Count;
            }
        }
    }
}