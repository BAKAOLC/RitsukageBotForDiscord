using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace RitsukageBot.Modules.Interaction
{
    /// <summary>
    ///     Sample interaction module
    /// </summary>
    [Group("sample", "sample group description")]
    public class SampleInteractionModule : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Ping
        /// </summary>
        /// <returns></returns>
        [SlashCommand("ping", "Replies with pong")]
        public Task PingAsync()
        {
            return RespondAsync("Pong!");
        }

        /// <summary>
        ///     Echo
        /// </summary>
        /// <param name="input">The text to echo</param>
        /// <returns></returns>
        [SlashCommand("echo", "Echoes a message")]
        public Task EchoAsync(string input)
        {
            return RespondAsync(input);
        }

        /// <summary>
        ///     Autocomplete example
        /// </summary>
        /// <param name="parameterWithAutocompletion"></param>
        /// <returns></returns>
        [SlashCommand("command_name", "command_description")]
        public Task ExampleCommandAsync(
            [Summary("parameter_name")] [Autocomplete(typeof(ExampleAutocompleteHandler))]
            string parameterWithAutocompletion)
        {
            return RespondAsync($"Your choice: {parameterWithAutocompletion}");
        }

        /// <summary>
        ///     Autocomplete handler example
        /// </summary>
        public class ExampleAutocompleteHandler : AutocompleteHandler
        {
            /// <summary>
            ///     Generate suggestions for autocomplete
            /// </summary>
            /// <param name="context"></param>
            /// <param name="autocompleteInteraction"></param>
            /// <param name="parameter"></param>
            /// <param name="services"></param>
            /// <returns></returns>
            public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
            {
                // Generate suggestions
                IEnumerable<AutocompleteResult> results =
                [
                    new("Name1", "value111"),
                    new("Name2", "value2"),
                ];

                // max - 25 suggestions at a time (API limit)
                return Task.FromResult(AutocompletionResult.FromSuccess(results.Take(25)));
            }
        }
    }
}