using RitsukageBot.Library.Enums.Bilibili;
using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Bilibili watcher configuration.
    /// </summary>
    [Table("BilibiliWatcherConfiguration")]
    public class BilibiliWatcherConfiguration
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey] [AutoIncrement] [Column("id")]
        public ulong Id { get; set; }

        /// <summary>
        ///     Channel id.
        /// </summary>
        [Indexed] [Column("channel_id")] public ulong ChannelId { get; set; }

        /// <summary>
        ///     Type.
        /// </summary>
        [Column("type")] public WatcherType Type { get; set; }

        /// <summary>
        ///     Target.
        /// </summary>
        [Indexed] [Column("target")] public string Target { get; set; } = string.Empty;

        /// <summary>
        ///     Last information.
        /// </summary>
        [Column("last_information")] public string LastInformation { get; set; } = string.Empty;
    }
}