using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Newtonsoft.Json;

namespace Scorebini.Data
{
    public class TournamentService
    {
        readonly ILogger Log;
        readonly IHttpClientFactory HttpFactory;
        readonly ScoreboardSettingsService SBSettingsService;

        const string TournamentsEndpoint = @"https://api.challonge.com/v1/tournaments";

        public TournamentService(ILogger<TournamentService> logger
            , IHttpClientFactory httpFactory
            , ScoreboardSettingsService sbSettings )
        {
            Log = logger;
            HttpFactory = httpFactory;
            SBSettingsService = sbSettings;
        }


        public static string ExtractTournamentIdFromUrl(string url)
        {
            // todo: add subdomain
            int idx = url.LastIndexOf('/');
            return url.Substring(idx + 1);
        }

        public async Task<TournamentContext> InitForTournamentId(string tournamentId)
        {
            SBSettingsService.LoadSettings();
            Log.LogInformation($"Init for tournament {tournamentId}");


            TournamentContext ret = new();
            ret.TournamentId = tournamentId;
            try
            {
                var parts = await GetParticipants(tournamentId);
                ret.Participants = parts.Participants;
                ret.RequestErrors.AddRange(parts.RequestErrors);
                var matches = await GetMatches(tournamentId);
                ret.Matches = matches.Matches;
                ret.RequestErrors.AddRange(matches.RequestErrors);
            }
            catch(Exception ex)
            {
                Log.LogError(ex, ex.Message);
                ret.RequestErrors.Add(ex.Message);
            }
            return ret;
        }

        public List<TournamentParticipant> GetPendingOpponents(TournamentParticipant participant, TournamentView tournament)
        {
            var ret = new List<TournamentParticipant>();
            if (participant == null || tournament == null)
                return ret;

            foreach(var pair in tournament.Matches)
            {
                if(pair.Value.Status == MatchStatus.Pending || pair.Value.Status == MatchStatus.Open)
                {
                    if(pair.Value.Player1 == participant && pair.Value.Player2 != null)
                    {
                        ret.Add(pair.Value.Player2);
                    }
                    else if(pair.Value.Player2 == participant && pair.Value.Player1 != null)
                    {
                        ret.Add(pair.Value.Player1);
                    }
                }
            }
            return ret;
        }

        public List<TournamentMatch> GetPendingMatches(TournamentView tournament)
        {
            var ret = new List<TournamentMatch>();
            if (tournament == null)
                return ret;

            foreach(var pair in tournament.Matches)
            {
                if(pair.Value.Status == MatchStatus.Open
                    && pair.Value.Player1 != null
                    && pair.Value.Player2 != null)
                {
                    ret.Add(pair.Value);
                }
            }
            return ret;
        }

        struct LevenshteinDistance
        {
            public int Distance;
            public TournamentParticipant Participant;
        }

        public List<TournamentParticipant> ParticipantAutocompleteList(string input, TournamentView tournament)
        {
            if(string.IsNullOrWhiteSpace(input))
            {
                return tournament.AlphabeticalParticipants;
            }
            LevenshteinDistance[] distances = new LevenshteinDistance[tournament.AlphabeticalParticipants.Count];
            for(int i = 0; i < tournament.AlphabeticalParticipants.Count; i++)
            {
                distances[i].Participant = tournament.AlphabeticalParticipants[i];
                distances[i].Distance = Levenshtein.Compute(input, distances[i].Participant.Name);
            }
            return distances.OrderBy(d => d.Distance).Select(d => d.Participant).ToList();
        }


        public TournamentParticipant FindParticipant(string name, TournamentView tournament)
        {
            if (name == null)
                return null;

            string lowerInput = name.ToLower();
            foreach(var pair in tournament.Participants)
            {
                if (pair.Value.Name.ToLower() == lowerInput)
                {
                    return pair.Value;
                }
            }
            return null;
        }


        public class ParticipantResponse
        {
            public class ParticipantObject
            {
                [JsonProperty("participant")]
                public ChallongeParticipant Participant { get; set; }
            }


            public List<ChallongeParticipant> Participants { get; set; }
            public List<string> RequestErrors = new();
        }

        public async Task<ParticipantResponse> GetParticipants(string tournamentId)
        {
            Log.LogDebug($"Getting participants");


            ParticipantResponse ret = new();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{TournamentsEndpoint}/{tournamentId}/participants.json?api_key={SBSettingsService.CurrentSettings?.ChallongeApiKey}");
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            if(response.IsSuccessStatusCode)
            {
                string respContent = await response.Content.ReadAsStringAsync();
                Log.LogInformation("Participant response: " + respContent);
                var participantResp = JsonConvert.DeserializeObject<IList<ParticipantResponse.ParticipantObject>>(respContent);
                if(participantResp == null)
                {
                    string err = "Could not deserialize participant response.";
                    ret.RequestErrors.Add(err);
                    Log.LogError(err);
                    return ret;
                }
                ret.Participants = participantResp.Select(p => p.Participant).Where(p => p.Id.HasValue).ToList();
                return ret;
            }
            else
            {
                string err = $"Participant response error code {response.StatusCode} ({(int)response.StatusCode})";
                ret.RequestErrors.Add(err);
                Log.LogError(err);
                await Log422Errors(response);
                return ret;
            }
        }


        public class MatchGetResponse
        {
            public class MatchObject
            {
                [JsonProperty("match")]
                public ChallongeMatch Match { get; set; }
            }

            public List<ChallongeMatch> Matches { get; set; }
            public List<string> RequestErrors = new();
        }

        public async Task<MatchGetResponse> GetMatches(string tournamentId)
        {
            Log.LogDebug("Getting matches");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{TournamentsEndpoint}/{tournamentId}/matches.json?api_key={SBSettingsService.CurrentSettings?.ChallongeApiKey}");

            MatchGetResponse ret = new();
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            if(response.IsSuccessStatusCode)
            {
                string respContent = await response.Content.ReadAsStringAsync();
                Log.LogTrace("Matches response: " + respContent);
                var matchJson = JsonConvert.DeserializeObject<IList<MatchGetResponse.MatchObject>>(respContent);
                if(matchJson == null)
                {
                    string err = "Could not deserialize matches response.";
                    Log.LogError(err);
                    ret.RequestErrors.Add(err);
                    return ret;
                }
                ret.Matches = matchJson.Select(m => m.Match).Where(m=>m.Id.HasValue).ToList();
                return ret;
            }
            else
            {
                string err = $"Matches response error code {response.StatusCode} ({(int)response.StatusCode})";
                ret.RequestErrors.Add(err);
                Log.LogError(err);
                await Log422Errors(response);
                return ret;
            }
        }


        class ChallongeErrorResponse
        {
            [JsonProperty("errors")]
            IList<string> Errors { get; set; }
        }
        private async Task Log422Errors(HttpResponseMessage response)
        {
            if((int)response.StatusCode == 422)
            {
                string content = await response.Content.ReadAsStringAsync();
                Log.LogWarning($"Error content: {content}");
                //var errorResp = JsonConvert.DeserializeObject<ChallongeErrorResponse>(content);
            }
        }

    }
}
