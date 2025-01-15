using SQLite;

namespace RitsukageBot.Library.Data
{
    /// <summary>
    ///     GitHub account configuration.
    /// </summary>
    [Table("GithubAccountConfiguration")]
    public class GitHubAccountConfiguration
    {
        /// <summary>
        ///     Id.
        /// </summary>
        [PrimaryKey] [Column("Id")] public ulong Id { get; set; }

        /// <summary>
        ///     Access token.
        /// </summary>
        [Column("AccessToken")] public string AccessToken { get; set; } = string.Empty;
    }
}