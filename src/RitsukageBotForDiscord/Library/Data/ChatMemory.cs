using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Chat Memory
    /// </summary>
    [Table("ChatMemory")]
    public class ChatMemory
    {
        /// <summary>
        ///     Key.
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        ///     Memory for user.
        /// </summary>
        [Column("user_id")]
        public ulong UserId { get; set; }

        /// <summary>
        ///     Type.
        /// </summary>
        [Column("type")]
        public ChatMemoryType Type { get; set; } = ChatMemoryType.ShortTerm;

        /// <summary>
        ///     Value.
        /// </summary>
        [Column("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        ///     Timestamp.
        /// </summary>
        [Column("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.MinValue;
    }

    /// <summary>
    ///     Chat Memory Type
    /// </summary>
    public enum ChatMemoryType
    {
        /// <summary>
        ///     Short Term
        /// </summary>
        ShortTerm,

        /// <summary>
        ///     Long Term
        /// </summary>
        LongTerm,
    }
}