using Microsoft.EntityFrameworkCore;

namespace ScorebiniTwitchApi.Models
{
    [Owned]
    public class TwitchUserInfo
    {
        public string Login { get; set; }
        public string TwitchId { get; set; }
        public string? BroadcasterType { get; set; }

        public TwitchUserInfo(string login, string twitchId, string? broadcasterType)
        {
            Login = login;
            TwitchId = twitchId;
            BroadcasterType = broadcasterType;
        }
    }
}
