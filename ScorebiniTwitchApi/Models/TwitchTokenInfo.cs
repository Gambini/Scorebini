using Microsoft.EntityFrameworkCore;
using ScorebiniTwitchApi.Controllers;

namespace ScorebiniTwitchApi.Models
{
    [Owned]
    public class TwitchTokenInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = string.Empty;

        public TwitchTokenInfo() { }

        internal TwitchTokenInfo(TwitchAuthTokenResponse response)
        {
            AccessToken = response.AccessToken;
            RefreshToken = response.RefreshToken;
            ExpiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(response.ExpiresIn);
            Scope = string.Join(' ', response.Scope);
        }
    }
}
