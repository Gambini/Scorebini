using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Scorebini.Data
{
    public class ChallongeMatch
    {
        [JsonProperty("id")]
        public long? Id { get; set; }
        [JsonProperty("player1_id")]
        public long? Player1Id { get; set; }
        [JsonProperty("player2_id")]
        public long? Player2Id { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
    }
}
