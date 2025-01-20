using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Bilibili account configuration.
    /// </summary>
    [Table("BilibiliAccountConfiguration")]
    public class BilibiliAccountConfiguration
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        ///     Token.
        /// </summary>
        [Column("token")]
        public string? Token { get; set; }

        /// <summary>
        ///     Cookies.
        /// </summary>
        [Column("cookies")]
        public string? Cookies { get; set; }
    }
}