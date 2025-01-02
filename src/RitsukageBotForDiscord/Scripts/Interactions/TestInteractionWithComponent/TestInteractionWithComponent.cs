using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

public class SampleScriptInteractionWithComponentModule : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
{
    public required ILogger<SampleScriptInteractionWithComponentModule> Logger { get; set; }

    [SlashCommand("test_component", "Test interaction with component")]
    public Task TestComponentReplyAsync()
    {
        Logger.LogInformation("Test interaction with component");
        var builder = new ComponentBuilder()
            .WithButton("Primary Button", "test_primary_button")
            .WithButton("Secondary Button", "test_secondary_button", ButtonStyle.Secondary)
            .WithButton("Success Button", "test_success_button", ButtonStyle.Success)
            .WithButton("Danger Button", "test_danger_button", ButtonStyle.Danger)
            .WithButton("Link Button", style: ButtonStyle.Link, url: "https://github.com/BAKAOLC/RitsukageBotForDiscord");

        return RespondAsync("Test interaction with component", components: builder.Build());
    }
}

public class SampleScriptInteractionWithComponentButtonModule : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    /// <summary>
    ///     Logger
    /// </summary>
    public required ILogger<SampleScriptInteractionWithComponentButtonModule> Logger { get; set; }

    [ComponentInteraction("test_primary_button")]
    public Task TestPrimaryButtonAsync()
    {
        Logger.LogInformation("Primary button clicked");
        return Context.Interaction.UpdateAsync(x =>
        {
            x.Content = "Primary button clicked";
            x.Components = null;
        });
    }

    [ComponentInteraction("test_secondary_button")]
    public Task TestSecondaryButtonAsync()
    {
        Logger.LogInformation("Secondary button clicked");
        return Context.Interaction.UpdateAsync(x =>
        {
            x.Content = "Secondary button clicked";
            x.Components = null;
        });
    }

    [ComponentInteraction("test_success_button")]
    public Task TestSuccessButtonAsync()
    {
        Logger.LogInformation("Success button clicked");
        return Context.Interaction.UpdateAsync(x =>
        {
            x.Content = "Success button clicked";
            x.Components = null;
        });
    }

    [ComponentInteraction("test_danger_button")]
    public Task TestDangerButtonAsync()
    {
        Logger.LogInformation("Danger button clicked");
        return Context.Interaction.UpdateAsync(x =>
        {
            x.Content = "Danger button clicked";
            x.Components = null;
        });
    }
}