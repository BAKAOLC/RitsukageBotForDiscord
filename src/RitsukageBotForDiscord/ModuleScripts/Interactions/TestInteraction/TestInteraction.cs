using Discord.Interactions;
using Microsoft.Extensions.Logging;

public class SampleScriptInteractionModule : InteractionModuleBase<SocketInteractionContext>
{
    public required ILogger<SampleScriptInteractionModule> Logger { get; set; }

    [SlashCommand("test_script", "Test interaction command for script")]
    public Task TestScriptAsync()
    {
        Logger.LogInformation("Test command executed");
        return RespondAsync("Test command executed");
    }
}