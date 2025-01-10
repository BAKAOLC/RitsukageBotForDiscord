using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using RitsukageBot.Library.Minecraft.Server;
using ConnectionState = RitsukageBot.Library.Minecraft.Server.Enums.ConnectionState;

namespace RitsukageBot.Modules
{
    /// <summary>
    ///     Minecraft interactions
    /// </summary>
    [Group("minecraft", "minecraft interactions")]
    public class MinecraftInteractions : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
    {
        /// <summary>
        ///     Tag custom ID
        /// </summary>
        public const string TagCustomId = "minecraft_interaction";

        /// <summary>
        ///     Logger
        /// </summary>
        public required ILogger<MinecraftInteractions> Logger { get; set; }

        /// <summary>
        /// </summary>
        [SlashCommand("server-info", "Get server info")]
        public async Task GetServerInfoAsync(string serverAddress, ushort serverPort = 25565)
        {
            await DeferAsync(true);
            var serverInfo = new ServerInfo(serverAddress, serverPort, Logger);
            await serverInfo.StartGetServerInfoAsync();

            var embed = new EmbedBuilder();
            embed.WithTitle($"{serverInfo.ServerAddress}:{serverInfo.ServerPort}");
            if (serverInfo.State == ConnectionState.Good)
            {
                embed.WithColor(Color.Green);
                if (!string.IsNullOrEmpty(serverInfo.Motd))
                    embed.WithDescription(serverInfo.Motd);
                embed.AddField("Players", $"{serverInfo.CurrentPlayerCount}/{serverInfo.MaxPlayerCount}");
                embed.AddField("Version", serverInfo.GameVersion);
                embed.AddField("Ping", serverInfo.Ping < 0 ? "Failed" : $"{serverInfo.Ping}ms");
            }
            else
            {
                embed.WithColor(Color.Red);
                embed.WithDescription("Server is offline");
            }

            if (serverInfo.IconData.Length > 0)
            {
                var ms = new MemoryStream(serverInfo.IconData);
                embed.WithThumbnailUrl("attachment://server-icon.png");
                await FollowupWithFileAsync(ms, "server-icon.png", embed: embed.Build());
            }
            else
            {
                await FollowupAsync(embed: embed.Build());
            }
        }
    }
}