using System.ComponentModel.DataAnnotations;

namespace ScorebiniTwitchApi.Models
{
    /// <summary>
    /// This is the main class that owns a bunch of related data.
    /// The design of this is due to inexperience.
    /// This is intended to be used with fewer than 10 users
    /// </summary>
    public class ScorebiniUserInfo
    {
        [Key]
        public int Id { get; set; }
        /// <summary>
        /// Sent to the client instead of exposing the database row.
        /// </summary>
        public Guid ClientToken { get; set; }
        /// <summary>
        /// UTC
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// UTC
        /// </summary>
        public DateTime LastAuthed { get; set; }
        public TwitchUserInfo? TwitchInfo { get; set; }
        public TwitchTokenInfo? TokenInfo { get; set; }
        public TwitchRedirectInfo? RedirectInfo { get; set; }
    }
}
