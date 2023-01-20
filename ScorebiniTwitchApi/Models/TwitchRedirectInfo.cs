using Microsoft.EntityFrameworkCore;

namespace ScorebiniTwitchApi.Models
{
    [Owned]
    public class TwitchRedirectInfo
    {
        public string CSRFStateString { get; set; } = string.Empty;
        public DateTime AuthTime { get; set; } = DateTime.UtcNow;
    }
}
