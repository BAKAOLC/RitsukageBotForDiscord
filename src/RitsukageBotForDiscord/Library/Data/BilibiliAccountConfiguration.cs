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
        [PrimaryKey] [Column("Id")] public int Id { get; set; }

        /// <summary>
        ///     Token.
        /// </summary>
        [Column("Token")] public string? Token { get; set; }

        /// <summary>
        ///     Cookies.
        /// </summary>
        [Column("Cookies")] public string? Cookies { get; set; }
    }
}