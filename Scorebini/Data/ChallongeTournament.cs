using Newtonsoft.Json;
using System.Collections.Generic;

namespace Scorebini.Data
{
    public class ChallongeTournament
    {
        // Challonge API uses wrapper objects, so must we
        public class ParticipantEntry 
        {
            [JsonProperty("participant")]
            public ChallongeParticipant Participant { get; set; }
        }

        public class MatchEntry
        {
            [JsonProperty("match")]
            public ChallongeMatch Match { get; set; }
        }

        // Integer representation of tournament id
        [JsonProperty("id")]
        public long? Id { get; set; }
        [JsonProperty("participants_count")]
        public long? ParticipantCount { get; set; }
        [JsonProperty("tournament_type")]
        public string TournamentType { get; set; }
        // Includes the https://
        [JsonProperty("full_challonge_url")]
        public string FullUrl { get; set; } 
        // Does not include https://, pretty much just the tournamentId part
        [JsonProperty("url")]
        public string UrlId { get; set; } 

        [JsonProperty("participants")]
        public IList<ParticipantEntry> Participants { get; set; }
        [JsonProperty("matches")]
        public IList<MatchEntry> Matches { get; set; }
    }
}
