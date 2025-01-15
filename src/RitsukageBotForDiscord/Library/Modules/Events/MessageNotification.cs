using Discord.WebSocket;

namespace RitsukageBot.Library.Modules.Events
{
    /// <summary>
    ///     Message notification.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="message"></param>
    public class MessageNotification(IServiceProvider services, SocketMessage message) : NotificationBase(services)
    {
        /// <summary>
        ///     Message.
        /// </summary>
        public SocketMessage Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
    }
}