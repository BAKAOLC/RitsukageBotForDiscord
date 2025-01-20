using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Discord user configuration.
    /// </summary>
    [Table("DiscordUserConfiguration")]
    public class DiscordUserConfiguration
    {
        /// <summary>
        ///     User ID.
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public ulong Id { get; set; }
        
        /// <summary>
        ///     Candy count.
        /// </summary>
        [Column("candy_count")]
        public long CandyCount { get; set; }
    }
}