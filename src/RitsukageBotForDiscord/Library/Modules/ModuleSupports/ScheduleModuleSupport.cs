using System.Reflection;
using RitsukageBot.Library.Modules.Schedules;

namespace RitsukageBot.Library.Modules.ModuleSupports
{
    /// <summary>
    ///     Schedule module support.
    /// </summary>
    /// <param name="services"></param>
    public class ScheduleModuleSupport(IServiceProvider services) : IDiscordBotModule
    {
        private readonly List<ScheduleTask> _tasks = [];
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public Task InitAsync()
        {
            _cancellationTokenSource = new();
            var scheduleTaskTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => type.IsAssignableTo(typeof(ScheduleTask)) && !type.IsAbstract)
                .ToArray();
            foreach (var type in scheduleTaskTypes)
            {
                var task = (ScheduleTask)Activator.CreateInstance(type, services)!;
                _tasks.Add(task);
            }

            Task.Run(RunAsync, _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        private async Task RunAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var tasks = _tasks.Where(task => task.IsEnabled).ToArray();
                if (tasks.Length == 0)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                    continue;
                }

                var now = DateTimeOffset.Now;
                var taskNextExecutedTimes = tasks.Select(task => task.NextExecutedTime).ToArray();
                var minNextExecutedTime = taskNextExecutedTimes.Min();
                if (minNextExecutedTime < now) minNextExecutedTime = now;
                var minNextInterval = minNextExecutedTime - now;
                if (minNextInterval > TimeSpan.Zero)
                    await Task.Delay(minNextInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                if (_cancellationTokenSource.Token.IsCancellationRequested) return;
                tasks = _tasks.Where(task => task.IsEnabled && task.NextExecutedTime <= minNextExecutedTime).ToArray();
                foreach (var task in tasks)
                    await task.TriggerAsync(minNextExecutedTime, _cancellationTokenSource.Token).ConfigureAwait(false);
                RemoveAllInvalidTasks();
            }
        }

        /// <summary>
        ///     Add a task.
        /// </summary>
        /// <param name="task"></param>
        public void AddTask(ScheduleTask task)
        {
            _tasks.Add(task);
        }

        /// <summary>
        ///     Remove a task.
        /// </summary>
        /// <param name="task"></param>
        public void RemoveTask(ScheduleTask task)
        {
            _tasks.Remove(task);
        }

        /// <summary>
        ///     Remove a task by guid.
        /// </summary>
        /// <param name="guid"></param>
        public void RemoveTask(Guid guid)
        {
            _tasks.RemoveAll(task => task.Configuration.Guid == guid);
        }

        /// <summary>
        ///     Clear all tasks.
        /// </summary>
        public void ClearTasks()
        {
            _tasks.Clear();
        }

        /// <summary>
        ///     Remove all invalid tasks.
        /// </summary>
        public void RemoveAllInvalidTasks()
        {
            _tasks.RemoveAll(task => !task.IsEnabled || task.IsFinished);
        }

        ~ScheduleModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _cancellationTokenSource.Cancel();
            _tasks.Clear();
        }

        private async ValueTask DisposeAsyncCore()
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            _tasks.Clear();
        }
    }
}