using System.Reflection;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Bot Information Commands
    /// </summary>
    [Group("bot", "Bot Information Commands")]
    public class BotInformationInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;

        private static readonly string AssemblyAuthor = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(false)
            .OfType<AssemblyCompanyAttribute>()
            .FirstOrDefault()?.Company ?? "Unknown";

        private static readonly string AssemblyRepositoryUrl = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(false)
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "RepositoryUrl")?.Value ?? string.Empty;

        /// <summary>
        ///     Get Bot Information
        /// </summary>
        [SlashCommand("info", "Get Bot Information")]
        public async Task GetBotInformationAsync()
        {
            await DeferAsync().ConfigureAwait(false);

            var embed = new EmbedBuilder();
            embed.WithTitle(FormatName(AssemblyName));
            embed.AddField("Version", GitVersionInformation.InformationalVersion);
            if (!string.IsNullOrWhiteSpace(AssemblyAuthor))
                embed.AddField("Author", AssemblyAuthor);
            if (!string.IsNullOrWhiteSpace(AssemblyRepositoryUrl))
                embed.AddField("Repository", AssemblyRepositoryUrl);

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        private static string FormatName(string name)
        {
            var sb = new StringBuilder();
            var lastChar = ' ';
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && char.IsLower(lastChar)) sb.Append(' ');

                    sb.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (!char.IsWhiteSpace(lastChar)) sb.Append(' ');
                }
                else
                {
                    sb.Append(' ');
                }

                lastChar = c;
            }

            return sb.ToString();
        }
    }
}