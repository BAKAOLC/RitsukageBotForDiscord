using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Chat Data Change History
    /// </summary>
    [Table("ChatDataChangeHistory")]
    public class ChatDataChangeHistory
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        [Column("id")]
        public ulong Id { get; set; }

        /// <summary>
        ///     Memory for user.
        /// </summary>
        [Indexed]
        [Column("user_id")]
        public ulong UserId { get; set; }

        /// <summary>
        ///     Key.
        /// </summary>
        [Indexed]
        [Column("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        ///     Value.
        /// </summary>
        [Column("value")]
        public int Value { get; set; } = 0;

        /// <summary>
        ///     Reason.
        /// </summary>
        [Column("reason")]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        ///     Timestamp.
        /// </summary>
        [Column("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.MinValue;
    }
}