using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Services.HostedServices;

namespace RitsukageBot.Library.Modules
{
    internal class DiscordEventLoggerModule(DiscordBotService discordBotService, IServiceProvider services) : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly ILogger<DiscordEventLoggerModule> _logger = services.GetRequiredService<ILogger<DiscordEventLoggerModule>>();

        public Task InitAsync()
        {
            _client.AuditLogCreated += ClientOnAuditLogCreated;
            _client.GuildJoinRequestDeleted += ClientOnGuildJoinRequestDeleted;
            _client.JoinedGuild += ClientOnJoinedGuild;
            _client.MessageReceived += ClientOnMessageReceived;
            _client.MessageDeleted += ClientOnMessageDeleted;
            _client.MessageUpdated += ClientOnMessageUpdated;
            _client.MessagesBulkDeleted += ClientOnMessagesBulkDeleted;
            _client.UserBanned += ClientOnUserBanned;
            _client.UserJoined += ClientOnUserJoined;
            _client.UserLeft += ClientOnUserLeft;
            _client.UserUnbanned += ClientOnUserUnbanned;
            return Task.CompletedTask;
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        private Task ClientOnAuditLogCreated(SocketAuditLogEntry arg1, SocketGuild arg2)
        {
            _logger.LogInformation("Guild {Guild} created an audit log. (User: {User}, Action: {Action}, Reason: {Reason})", arg2, arg1.User, arg1.Action, arg1.Reason);
            return Task.CompletedTask;
        }

        private Task ClientOnGuildJoinRequestDeleted(Cacheable<SocketGuildUser, ulong> arg1, SocketGuild arg2)
        {
            _logger.LogInformation("Guild {Guild} deleted a join request. (User: {User})", arg2, arg1.Value);
            return Task.CompletedTask;
        }

        private Task ClientOnJoinedGuild(SocketGuild arg)
        {
            _logger.LogInformation("Joined guild {Guild}.", arg);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageReceived(SocketMessage arg)
        {
            _logger.LogInformation("[Message-Received] [{Channel}] {User}: {Content}", arg.Channel, arg.Author, arg.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            _logger.LogInformation("[Message-Updated] [{Channel}] {User}: {Content} -> {NewContent}", arg3, arg2.Author, arg1.Value.Content, arg2.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
        {
            _logger.LogInformation("[Message-Deleted] [{Channel}] {User}: {Content}", arg2.Value, arg1.Value.Author, arg1.Value.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, Cacheable<IMessageChannel, ulong> arg2)
        {
            foreach (var message in arg1) _logger.LogInformation("[Message-Deleted] [{Channel}] {User}: {Content}", arg2.Value, message.Value.Author, message.Value.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User} banned from guild {Guild}.", arg1, arg2);
            return Task.CompletedTask;
        }

        private Task ClientOnUserJoined(SocketGuildUser arg)
        {
            _logger.LogInformation("User {User} joined guild {Guild}.", arg, arg.Guild);
            return Task.CompletedTask;
        }

        private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
        {
            _logger.LogInformation("User {User} left guild {Guild}.", arg2, arg1);
            return Task.CompletedTask;
        }

        private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User} unbanned from guild {Guild}.", arg1, arg2);
            return Task.CompletedTask;
        }

        ~DiscordEventLoggerModule()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _client.AuditLogCreated -= ClientOnAuditLogCreated;
            _client.GuildJoinRequestDeleted -= ClientOnGuildJoinRequestDeleted;
            _client.JoinedGuild -= ClientOnJoinedGuild;
            _client.MessageReceived -= ClientOnMessageReceived;
            _client.MessageDeleted -= ClientOnMessageDeleted;
            _client.MessageUpdated -= ClientOnMessageUpdated;
            _client.MessagesBulkDeleted -= ClientOnMessagesBulkDeleted;
            _client.UserBanned -= ClientOnUserBanned;
            _client.UserJoined -= ClientOnUserJoined;
            _client.UserLeft -= ClientOnUserLeft;
            _client.UserUnbanned -= ClientOnUserUnbanned;
        }

        private ValueTask DisposeAsyncCore()
        {
            _client.AuditLogCreated -= ClientOnAuditLogCreated;
            _client.GuildJoinRequestDeleted -= ClientOnGuildJoinRequestDeleted;
            _client.JoinedGuild -= ClientOnJoinedGuild;
            _client.MessageReceived -= ClientOnMessageReceived;
            _client.MessageDeleted -= ClientOnMessageDeleted;
            _client.MessageUpdated -= ClientOnMessageUpdated;
            _client.MessagesBulkDeleted -= ClientOnMessagesBulkDeleted;
            _client.UserBanned -= ClientOnUserBanned;
            _client.UserJoined -= ClientOnUserJoined;
            _client.UserLeft -= ClientOnUserLeft;
            _client.UserUnbanned -= ClientOnUserUnbanned;
            return ValueTask.CompletedTask;
        }
    }
}