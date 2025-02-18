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
        ///     Id.
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        [Column("id")]
        public ulong Id { get; set; }

        /// <summary>
        ///     Key.
        /// </summary>
        [Indexed]
        [Column("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        ///     Memory for user.
        /// </summary>
        [Indexed]
        [Column("user_id")]
        public ulong UserId { get; set; }

        /// <summary>
        ///     Type.
        /// </summary>
        [Indexed]
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
        ///     Any
        /// </summary>
        Any,

        /// <summary>
        ///     Short Term
        /// </summary>
        ShortTerm,

        /// <summary>
        ///     Long Term
        /// </summary>
        LongTerm,

        /// <summary>
        ///    Self State
        /// </summary>
        SelfState,
    }
}