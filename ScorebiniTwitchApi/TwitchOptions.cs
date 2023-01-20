using System.ComponentModel.DataAnnotations;

namespace ScorebiniTwitchApi
{
    public class TwitchOptions
    {
        public const string Section = "Twitch";
        [Required]
        public string AppClientId { get; set; } = string.Empty;
        [Required]
        public string AppClientSecret { get; set; } = string.Empty;
        public double TokenValidateIntervalMinutes { get; set; } = 50;
    }
}
