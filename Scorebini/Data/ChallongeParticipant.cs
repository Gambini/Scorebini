using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Scorebini.Data
{
    public class ChallongeParticipant
    {
        [JsonProperty("id"), JsonConverter(typeof(StringOrIntIdConverter))]
        public StringOrIntId Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
