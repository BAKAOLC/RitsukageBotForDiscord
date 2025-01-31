using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RitsukageBot.Library.Modules
{
    internal class DiscordEventLoggerModule(IServiceProvider services) : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();

        private readonly ILogger<DiscordEventLoggerModule> _logger =
            services.GetRequiredService<ILogger<DiscordEventLoggerModule>>();

        public Task InitAsync()
        {
            _client.AuditLogCreated += ClientOnAuditLogCreatedAsync;
            _client.GuildJoinRequestDeleted += ClientOnGuildJoinRequestDeletedAsync;
            _client.JoinedGuild += ClientOnJoinedGuildAsync;
            _client.MessageReceived += ClientOnMessageReceivedAsync;
            _client.MessageDeleted += ClientOnMessageDeletedAsync;
            _client.MessageUpdated += ClientOnMessageUpdatedAsync;
            _client.MessagesBulkDeleted += ClientOnMessagesBulkDeletedAsync;
            _client.UserBanned += ClientOnUserBannedAsync;
            _client.UserJoined += ClientOnUserJoinedAsync;
            _client.UserLeft += ClientOnUserLeftAsync;
            _client.UserUnbanned += ClientOnUserUnbannedAsync;
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

        private Task ClientOnAuditLogCreatedAsync(SocketAuditLogEntry arg1, SocketGuild arg2)
        {
            _logger.LogInformation(
                "Guild {Guild}({GuildId}) created an audit log. (User: {User}({UserId}), Action: {Action}, Reason: {Reason})",
                arg2, arg2.Id, arg1.User, arg1.User?.Id, arg1.Action, arg1.Reason);
            return Task.CompletedTask;
        }

        private Task ClientOnGuildJoinRequestDeletedAsync(Cacheable<SocketGuildUser, ulong> arg1, SocketGuild arg2)
        {
            if (arg1.HasValue)
                _logger.LogInformation("Guild {Guild}({GuildId} deleted a join request. (User: {User}({UserId})", arg2,
                    arg2.Id, arg1.Value, arg1.Value.Id);
            else
                _logger.LogInformation("Guild {Guild}({GuildId} deleted a join request. (User: (Unknown))", arg2,
                    arg2.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnJoinedGuildAsync(SocketGuild arg)
        {
            _logger.LogInformation("Joined guild {Guild}({GuildId})", arg, arg.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageReceivedAsync(SocketMessage arg)
        {
            var headTag = arg.Author.Id == _client.CurrentUser.Id ? "Message-Sent" : "Message-Received";
            if (arg.Channel is SocketGuildChannel guildChannel)
                _logger.LogInformation(
                    "[{Tag}] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content}", headTag,
                    guildChannel.Guild, guildChannel.Guild.Id, arg.Channel, arg.Channel.Id, arg.Author, arg.Author.Id,
                    arg.Content);
            else
                _logger.LogInformation("[{Tag}] [{Channel}({ChannelID})] {User}({UserId}): {Content}", headTag,
                    arg.Channel, arg.Channel.Id, arg.Author, arg.Author.Id, arg.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageUpdatedAsync(Cacheable<IMessage, ulong> arg1, SocketMessage arg2,
            ISocketMessageChannel arg3)
        {
            if (arg3 is SocketGuildChannel guildChannel)
            {
                if (arg1.HasValue)
                    _logger.LogInformation(
                        "[Message-Updated] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content} -> {NewContent}",
                        guildChannel.Guild, guildChannel.Guild.Id, arg3, arg3.Id, arg2.Author, arg2.Author.Id,
                        arg1.Value.Content, arg2.Content);
                else
                    _logger.LogInformation(
                        "[Message-Updated] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): (Unknown) -> {NewContent}",
                        guildChannel.Guild, guildChannel.Guild.Id, arg3, arg3.Id, arg2.Author, arg2.Author.Id,
                        arg2.Content);
            }
            else
            {
                if (arg1.HasValue)
                    _logger.LogInformation(
                        "[Message-Updated] [{Channel}({ChannelID})] {User}({UserId}): {Content} -> {NewContent}", arg3,
                        arg3.Id, arg2.Author, arg2.Author.Id, arg2.Content, arg2.Content);
                else
                    _logger.LogInformation(
                        "[Message-Updated] [{Channel}({ChannelID})] {User}({UserId}): (Unknown) -> {NewContent}", arg3,
                        arg3.Id, arg2.Author, arg2.Author.Id, arg2.Content);
            }

            return Task.CompletedTask;
        }

        private Task ClientOnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1,
            Cacheable<IMessageChannel, ulong> arg2)
        {
            if (arg2.Value is SocketGuildChannel guildChannel)
            {
                if (arg1.HasValue)
                    _logger.LogInformation(
                        "[Message-Deleted] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content}",
                        guildChannel.Guild, guildChannel.Guild.Id, arg2.Value, arg2.Value.Id, arg1.Value.Author,
                        arg1.Value.Author.Id, arg1.Value.Content);
                else
                    _logger.LogInformation("[Message-Deleted] [{Guild}({GuildId})] [{Channel}({ChannelID})] (Unknown)",
                        guildChannel.Guild, guildChannel.Guild.Id, arg2.Value, arg2.Value.Id);
            }
            else
            {
                if (arg1.HasValue)
                    _logger.LogInformation("[Message-Deleted] [{Channel}({ChannelID})] {User}({UserId}): {Content}",
                        arg2.Value, arg2.Value.Id, arg1.Value.Author, arg1.Value.Author.Id, arg1.Value.Content);
                else
                    _logger.LogInformation("[Message-Deleted] [{Channel}({ChannelID})] (Unknown)", arg2.Value,
                        arg2.Value.Id);
            }

            return Task.CompletedTask;
        }

        private async Task ClientOnMessagesBulkDeletedAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1,
            Cacheable<IMessageChannel, ulong> arg2)
        {
            foreach (var message in arg1)
                await ClientOnMessageDeletedAsync(message, arg2).ConfigureAwait(false);
        }

        private Task ClientOnUserBannedAsync(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User}({UserId}) banned from guild {Guild}({GuildId})", arg1, arg1.Id, arg2,
                arg2.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserJoinedAsync(SocketGuildUser arg)
        {
            _logger.LogInformation("User {User}({UserId}) joined guild {Guild}({GuildId})", arg, arg.Id, arg.Guild,
                arg.Guild.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserLeftAsync(SocketGuild arg1, SocketUser arg2)
        {
            _logger.LogInformation("User {User}({UserId}) left guild {Guild}({GuildId})", arg2, arg2.Id, arg1,
                arg1.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserUnbannedAsync(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User}({UserId}) unbanned from guild {Guild}({GuildId})", arg1, arg1.Id, arg2,
                arg2.Id);
            return Task.CompletedTask;
        }

        ~DiscordEventLoggerModule()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _client.AuditLogCreated -= ClientOnAuditLogCreatedAsync;
            _client.GuildJoinRequestDeleted -= ClientOnGuildJoinRequestDeletedAsync;
            _client.JoinedGuild -= ClientOnJoinedGuildAsync;
            _client.MessageReceived -= ClientOnMessageReceivedAsync;
            _client.MessageDeleted -= ClientOnMessageDeletedAsync;
            _client.MessageUpdated -= ClientOnMessageUpdatedAsync;
            _client.MessagesBulkDeleted -= ClientOnMessagesBulkDeletedAsync;
            _client.UserBanned -= ClientOnUserBannedAsync;
            _client.UserJoined -= ClientOnUserJoinedAsync;
            _client.UserLeft -= ClientOnUserLeftAsync;
            _client.UserUnbanned -= ClientOnUserUnbannedAsync;
        }

        private ValueTask DisposeAsyncCore()
        {
            _client.AuditLogCreated -= ClientOnAuditLogCreatedAsync;
            _client.GuildJoinRequestDeleted -= ClientOnGuildJoinRequestDeletedAsync;
            _client.JoinedGuild -= ClientOnJoinedGuildAsync;
            _client.MessageReceived -= ClientOnMessageReceivedAsync;
            _client.MessageDeleted -= ClientOnMessageDeletedAsync;
            _client.MessageUpdated -= ClientOnMessageUpdatedAsync;
            _client.MessagesBulkDeleted -= ClientOnMessagesBulkDeletedAsync;
            _client.UserBanned -= ClientOnUserBannedAsync;
            _client.UserJoined -= ClientOnUserJoinedAsync;
            _client.UserLeft -= ClientOnUserLeftAsync;
            _client.UserUnbanned -= ClientOnUserUnbannedAsync;
            return ValueTask.CompletedTask;
        }
    }
}