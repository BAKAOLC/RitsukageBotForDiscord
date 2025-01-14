using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Discord channel configuration.
    /// </summary>
    [Table("DiscordChannelConfiguration")]
    public class DiscordChannelConfiguration
    {
        /// <summary>
        ///     Channel ID.
        /// </summary>
        [PrimaryKey] [Column("id")] public ulong Id { get; set; }

        /// <summary>
        ///     Automatically resolve Bilibili links.
        /// </summary>
        [Column("automatically_resolve_bilibili_links")]
        public bool AutomaticallyResolveBilibiliLinks { get; set; }
    }
}