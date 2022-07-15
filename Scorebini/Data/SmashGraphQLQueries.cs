using Newtonsoft.Json;
using System.Collections.Generic;

namespace Scorebini.Data.Smash.gg
{
    /**
     * 
     * 
     * To Start:
     * 
     query EventQuery($slug: String) {
		event(slug: $slug){
			id
			name
    	sets {
        nodes {
          id
          round
          fullRoundText
          state
          slots {
            entrant {
              id
              name
            }
            
          }
          
        }
      }
		}
	}
     */

    public enum SetState
    {
        Unknown = 0,
        Pending = 1,
        Open = 2,
        Complete = 3,
    }


    public static class SetStateExtensions
    {
        public static MatchStatus ToMatchStatus(this SetState state)
        {
            return state switch
            {
                SetState.Unknown => MatchStatus.Unknown,
                SetState.Pending => MatchStatus.Pending,
                SetState.Open => MatchStatus.Open,
                SetState.Complete => MatchStatus.Complete,
                _ => MatchStatus.Unknown,
            };
        }
    }


    public class Event
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("sets")]
        public SetConnection SetConnection { get; set; }
    }

    public class PageInfo
    {
        [JsonProperty("total")]
        public int Total { get; set; }
        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }
        [JsonProperty("page")]
        public int Page { get; set; }
        [JsonProperty("perPage")]
        public int PerPage { get; set; }
        [JsonProperty("sortBy")]
        public string SortBy { get; set; }
        //[JsonProperty("filter")]
        //public string Filter { get; set; }
    }


    public class Entrant
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }


    public class SetSlot
    {
        [JsonProperty("entrant")]
        public Entrant Entrant { get; set; }
    }

    public class Set
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("fullRoundText")]
        public string FullRoundText { get; set; }
        [JsonProperty("round")]
        public long Round { get; set; }
        [JsonProperty("slots")]
        public IList<SetSlot> Slots { get; set; }
        [JsonProperty("state")]
        public int State { get; set; } // Maps to SetState (reversed engineered, might be wrong)
    }

    public class SetConnection
    {
        [JsonProperty("pageInfo")]
        public PageInfo PageInfo { get; set; }

        [JsonProperty("nodes")]
        public IList<Set> Nodes { get; set; }
    }


    
    public class SmashGraphQLQuery
    {
        [JsonProperty("query")]
        public string Query { get; set; }
        /// <summary>
        /// Not required
        /// </summary>
        [JsonProperty("operationName")]
        public string OperationName { get; set; }
        /// <summary>
        /// Not required unless you have variables in the query string
        /// </summary>
        [JsonProperty("variables")]
        public Dictionary<string, string> Variables { get; set; } = new();
    }


    public static class FullEventQuery
    {
        public static string FullQuery = @"
query EventQuery($slug: String) {
  event(slug: $slug){
    id
    name
      sets {
      nodes {
        id
        round
        fullRoundText
        state
        slots {
          entrant {
            id
            name
          }
        }
      }
    }
  }
}";
    }
}
