using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     Chat User Information
    /// </summary>
    [Table("ChatUserInformation")]
    public class ChatUserInformation
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey]
        [Column("id")]
        public ulong Id { get; set; }
        
        /// <summary>
        ///     Good.
        /// </summary>
        [Column("good")]
        public int Good { get; set; }
    }
}