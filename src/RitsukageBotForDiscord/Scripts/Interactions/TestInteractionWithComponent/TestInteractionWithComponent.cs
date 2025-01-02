using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

/// <summary>
///     Sample script command module
/// </summary>
public class SampleScriptInteractionWithComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    ///     Logger
    /// </summary>
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