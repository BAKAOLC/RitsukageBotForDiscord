using Discord.Commands;
using Microsoft.Extensions.Logging;

public class SampleScriptCommandModule : ModuleBase<SocketCommandContext>
{
    public required ILogger<SampleScriptCommandModule> Logger { get; set; }

    [Command("test_script")]
    [Summary("Test command for script")]
    public Task TestScriptAsync()
    {
        Logger.LogInformation("Test command executed");
        return ReplyAsync("Test command executed");
    }
}