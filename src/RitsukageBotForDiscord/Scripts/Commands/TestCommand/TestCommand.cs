using Discord.Commands;
using Microsoft.Extensions.Logging;

/// <summary>
///     Sample script command module
/// </summary>
public class SampleScriptCommandModule : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Logger
    /// </summary>
    public required ILogger<SampleScriptCommandModule> Logger { get; set; }

    [Command("test_script")]
    [Summary("Test command for script")]
    public Task TestScriptAsync()
    {
        Logger.LogInformation("Test command executed");
        return ReplyAsync("Test command executed");
    }
}