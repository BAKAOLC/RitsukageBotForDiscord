using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Bot Record Information
    /// </summary>
    [Table("BotRecordInformation")]
    public class BotRecordInformation
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        ///     Total Candy Count.
        /// </summary>
        [Column("total_candy_count")]
        public long TotalCandyCount { get; set; }
    }
}