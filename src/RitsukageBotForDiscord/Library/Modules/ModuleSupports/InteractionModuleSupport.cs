using System.Reflection;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RitsukageBot.Services.HostedServices;

namespace RitsukageBot.Library.Modules.ModuleSupports
{
    internal sealed class InteractionModuleSupport(DiscordBotService discordBotService, IServiceProvider services)
        : IDiscordBotModule
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly InteractionService _interaction = services.GetRequiredService<InteractionService>();

        private readonly ILogger<InteractionModuleSupport> _logger =
            services.GetRequiredService<ILogger<InteractionModuleSupport>>();

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

        public async Task InitAsync()
        {
            _client.InteractionCreated += HandleInteractionCreatedAsync;
            _client.SlashCommandExecuted += HandleSlashCommandExecutedAsync;
            _client.ButtonExecuted += HandleButtonExecutedAsync;
            _client.MessageCommandExecuted += HandleMessageCommandExecutedAsync;
            _client.UserCommandExecuted += HandleUserCommandExecutedAsync;
            _client.SelectMenuExecuted += HandleSelectMenuExecutedAsync;
            _client.AutocompleteExecuted += HandleAutocompleteExecutedAsync;
            _client.Ready += RegisterCommandsAsync;
            _interaction.Log += discordBotService.LogAsync;
            await _interaction.AddModulesAsync(Assembly.GetEntryAssembly(), services).ConfigureAwait(false);
            if (_client.ConnectionState == ConnectionState.Connected)
                await RegisterCommandsAsync().ConfigureAwait(false);
        }

        public async Task ReInitAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            await InitAsync().ConfigureAwait(false);
        }

        internal Task RegisterCommandsAsync()
        {
            return _interaction.RegisterCommandsGloballyAsync();
        }

        internal static IInteractionContext CreateGeneric(DiscordSocketClient client, SocketInteraction interaction)
        {
            return interaction switch
            {
                SocketModal modal => new SocketInteractionContext<SocketModal>(client, modal),
                SocketUserCommand user => new SocketInteractionContext<SocketUserCommand>(client, user),
                SocketSlashCommand slash => new SocketInteractionContext<SocketSlashCommand>(client, slash),
                SocketMessageCommand message => new SocketInteractionContext<SocketMessageCommand>(client, message),
                SocketMessageComponent component => new SocketInteractionContext<SocketMessageComponent>(client,
                    component),
                SocketAutocompleteInteraction autocomplete =>
                    new SocketInteractionContext<SocketAutocompleteInteraction>(client, autocomplete),
                _ => throw new InvalidOperationException("This interaction type is unsupported! Please report this."),
            };
        }

        internal Task HandleInteractionCreatedAsync(SocketInteraction interaction)
        {
            return Task.CompletedTask;
        }

        internal Task HandleSlashCommandExecutedAsync(SocketSlashCommand command)
        {
            var context = new SocketInteractionContext<SocketSlashCommand>(_client, command);
            _logger.LogInformation("User {UserId} executed slash command {InteractionEntitlements}", context.User.Id,
                FormatSlashCommandData(context.Interaction.Data));
            return _interaction.ExecuteCommandAsync(context, services);
        }

        internal Task HandleButtonExecutedAsync(SocketMessageComponent component)
        {
            var context = CreateGeneric(_client, component);
            _logger.LogInformation("User {UserId} executed button {InteractionEntitlements}", context.User.Id,
                context.Interaction.Entitlements);
            return _interaction.ExecuteCommandAsync(context, services);
        }

        internal Task HandleMessageCommandExecutedAsync(SocketMessageCommand command)
        {
            var context = CreateGeneric(_client, command);
            _logger.LogInformation("User {UserId} executed message command {InteractionEntitlements}", context.User.Id,
                context.Interaction.Entitlements);
            return _interaction.ExecuteCommandAsync(context, services);
        }

        internal Task HandleUserCommandExecutedAsync(SocketUserCommand command)
        {
            var context = CreateGeneric(_client, command);
            _logger.LogInformation("User {UserId} executed user command {InteractionEntitlements}", context.User.Id,
                context.Interaction.Entitlements);
            return _interaction.ExecuteCommandAsync(context, services);
        }

        internal Task HandleSelectMenuExecutedAsync(SocketMessageComponent menu)
        {
            var context = CreateGeneric(_client, menu);
            _logger.LogInformation("User {UserId} executed select menu {InteractionEntitlements}", context.User.Id,
                context.Interaction.Entitlements);
            return _interaction.ExecuteCommandAsync(context, services);
        }

        private async Task HandleAutocompleteExecutedAsync(SocketAutocompleteInteraction arg)
        {
            var context = CreateGeneric(_client, arg);
            _logger.LogInformation("User {UserId} executed autocomplete {InteractionEntitlements}", context.User.Id,
                context.Interaction.Entitlements);
            await _interaction.ExecuteCommandAsync(context, services).ConfigureAwait(false);
        }

        internal static string FormatSlashCommandData(SocketSlashCommandData data)
        {
            var sb = new StringBuilder();
            sb.Append(data.Name);

            var child = data.Options?.ElementAtOrDefault(0);

            while (child?.Type is ApplicationCommandOptionType.SubCommandGroup
                   or ApplicationCommandOptionType.SubCommand)
            {
                sb.Append(' ').Append(child.Name);
                if (child.Value is not null) sb.Append(": ").Append(child.Value);
                child = child.Options?.ElementAtOrDefault(0);
            }

            return sb.ToString();
        }

        ~InteractionModuleSupport()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            foreach (var module in _interaction.Modules) _interaction.RemoveModuleAsync(module);

            _client.UserCommandExecuted -= HandleUserCommandExecutedAsync;
            _client.MessageCommandExecuted -= HandleMessageCommandExecutedAsync;
            _client.ButtonExecuted -= HandleButtonExecutedAsync;
            _client.SlashCommandExecuted -= HandleSlashCommandExecutedAsync;
            _client.InteractionCreated -= HandleInteractionCreatedAsync;
            _client.Ready -= RegisterCommandsAsync;
            _interaction.Log -= discordBotService.LogAsync;
        }

        private async ValueTask DisposeAsyncCore()
        {
            foreach (var module in _interaction.Modules)
                await _interaction.RemoveModuleAsync(module).ConfigureAwait(false);

            _client.UserCommandExecuted -= HandleUserCommandExecutedAsync;
            _client.MessageCommandExecuted -= HandleMessageCommandExecutedAsync;
            _client.ButtonExecuted -= HandleButtonExecutedAsync;
            _client.SlashCommandExecuted -= HandleSlashCommandExecutedAsync;
            _client.InteractionCreated -= HandleInteractionCreatedAsync;
            _client.Ready -= RegisterCommandsAsync;
            _interaction.Log -= discordBotService.LogAsync;
        }
    }
}