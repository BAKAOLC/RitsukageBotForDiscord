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
            _logger.LogInformation("Guild {Guild}({GuildId}) created an audit log. (User: {User}({UserId}), Action: {Action}, Reason: {Reason})", arg2, arg2.Id, arg1.User, arg1.User?.Id, arg1.Action, arg1.Reason);
            return Task.CompletedTask;
        }

        private Task ClientOnGuildJoinRequestDeleted(Cacheable<SocketGuildUser, ulong> arg1, SocketGuild arg2)
        {
            _logger.LogInformation("Guild {Guild}({GuildId} deleted a join request. (User: {User}({UserId})", arg2, arg2.Id, arg1.Value, arg1.Value.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnJoinedGuild(SocketGuild arg)
        {
            _logger.LogInformation("Joined guild {Guild}({GuildId}).", arg, arg.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageReceived(SocketMessage arg)
        {
            var headTag = arg.Author.Id == _client.CurrentUser.Id ? "Message-Sent" : "Message-Received";
            if (arg.Channel is SocketGuildChannel guildChannel)
                _logger.LogInformation("[{Tag}] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content}", headTag, guildChannel.Guild, guildChannel.Guild.Id, arg.Channel, arg.Channel.Id, arg.Author, arg.Author.Id, arg.Content);
            else
                _logger.LogInformation("[{Tag}] [{Channel}({ChannelID})] {User}({UserId}): {Content}", headTag, arg.Channel, arg.Channel.Id, arg.Author, arg.Author.Id, arg.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            if (arg3 is SocketGuildChannel guildChannel)
                _logger.LogInformation("[Message-Updated] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content} -> {NewContent}", guildChannel.Guild, guildChannel.Guild.Id, arg3, arg3.Id, arg2.Author, arg2.Author.Id, arg2.Content, arg2.Content);
            else
                _logger.LogInformation("[Message-Updated] [{Channel}({ChannelID})] {User}({UserId}): {Content} -> {NewContent}", arg3, arg3.Id, arg2.Author, arg2.Author.Id, arg2.Content, arg2.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
        {
            if (arg2.Value is SocketGuildChannel guildChannel)
                _logger.LogInformation("[Message-Deleted] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content}", guildChannel.Guild, guildChannel.Guild.Id, arg2.Value, arg2.Value.Id, arg1.Value.Author, arg1.Value.Author.Id, arg1.Value.Content);
            else
                _logger.LogInformation("[Message-Deleted] [{Channel}({ChannelID})] {User}({UserId}): {Content}", arg2.Value, arg2.Value.Id, arg1.Value.Author, arg1.Value.Author.Id, arg1.Value.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, Cacheable<IMessageChannel, ulong> arg2)
        {
            foreach (var message in arg1)
                if (arg2.Value is SocketGuildChannel guildChannel)
                    _logger.LogInformation("[Message-Deleted] [{Guild}({GuildId})] [{Channel}({ChannelID})] {User}({UserId}): {Content}", guildChannel.Guild, guildChannel.Guild.Id, arg2.Value, arg2.Value.Id, message.Value.Author, message.Value.Author.Id, message.Value.Content);
                else
                    _logger.LogInformation("[Message-Deleted] [{Channel}({ChannelID})] {User}({UserId}): {Content}", arg2.Value, arg2.Value.Id, message.Value.Author, message.Value.Author.Id, message.Value.Content);
            return Task.CompletedTask;
        }

        private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User}({UserId}) banned from guild {Guild}({GuildId}).", arg1, arg1.Id, arg2, arg2.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserJoined(SocketGuildUser arg)
        {
            _logger.LogInformation("User {User}({UserId}) joined guild {Guild}({GuildId}).", arg, arg.Id, arg.Guild, arg.Guild.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
        {
            _logger.LogInformation("User {User}({UserId}) left guild {Guild}({GuildId}).", arg2, arg2.Id, arg1, arg1.Id);
            return Task.CompletedTask;
        }

        private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
        {
            _logger.LogInformation("User {User}({UserId}) unbanned from guild {Guild}({GuildId}).", arg1, arg1.Id, arg2, arg2.Id);
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