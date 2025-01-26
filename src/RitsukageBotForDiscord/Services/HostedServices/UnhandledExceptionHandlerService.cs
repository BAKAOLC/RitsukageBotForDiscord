using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RitsukageBot.Services.HostedServices
{
    internal class UnhandledExceptionHandlerService(ILogger<UnhandledExceptionEventHandler> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
            logger.LogInformation("Unhandled exception handler started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            AppDomain.CurrentDomain.UnhandledException -= LogUnhandledException;
            logger.LogInformation("Unhandled exception handler stopped");
            return Task.CompletedTask;
        }

        private void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.LogError((Exception)e.ExceptionObject, "Unhandled exception occurred");
        }
    }
}