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
        [PrimaryKey]
        [Column("id")]
        public ulong Id { get; set; }

        /// <summary>
        ///     Access token.
        /// </summary>
        [Column("access_token")]
        // ReSharper disable once PropertyCanBeMadeInitOnly.Global
        public string AccessToken { get; set; } = string.Empty;
    }
}