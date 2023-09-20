using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Scorebini.Data
{
    public class ChallongeMatch
    {
        [JsonProperty("id"), JsonConverter(typeof(StringOrIntIdConverter))]
        public StringOrIntId Id { get; set; }
        [JsonProperty("player1_id"), JsonConverter(typeof(StringOrIntIdConverter))]
        public StringOrIntId Player1Id { get; set; }
        [JsonProperty("player2_id"), JsonConverter(typeof(StringOrIntIdConverter))]
        public StringOrIntId Player2Id { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
        /// <summary>
        /// Losers is negative, winners is positive. Round 1 is the first, up to n rounds
        /// </summary>
        [JsonProperty("round")]
        public long? Round { get; set; }
        /// <summary>
        /// Will be values like "A", "B", etc.
        /// </summary>
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    }
}
