using MediatR;

namespace RitsukageBot.Library.Modules.Events
{
    /// <summary>
    ///     Notification base.
    /// </summary>
    /// <param name="services"></param>
    public abstract class NotificationBase(IServiceProvider services) : INotification
    {
        /// <summary>
        ///     Services.
        /// </summary>
        public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
    }
}