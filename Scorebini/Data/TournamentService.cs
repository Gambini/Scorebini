using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Json;

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


        public static string ExtractTournamentIdFromChallongeUrl(string url)
        {
            // todo: add subdomain
            int idx = url.LastIndexOf('/');
            return url.Substring(idx + 1);
        }


        static string ExtractSlugFromSmashUrl(string url)
        {
            var match = Regex.Match(url, @"(tournament/[^/]*/event/[^/]*)");
            if (match.Success && match.Groups.Count > 0)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        public async Task<TournamentContext> InitForTournamentUrl(string url)
        {
            SBSettingsService.LoadSettings();
            TournamentHost host = TournamentHost.Unknown;
            string tournamentId = "";
            if (url.Contains("challonge.com", StringComparison.OrdinalIgnoreCase))
            {
                host = TournamentHost.Challonge;
                tournamentId = ExtractTournamentIdFromChallongeUrl(url);
            }
            else if(url.Contains("smash.gg", StringComparison.OrdinalIgnoreCase)
                || url.Contains("start.gg", StringComparison.OrdinalIgnoreCase))
            {
                host = TournamentHost.Smash;
                tournamentId = ExtractSlugFromSmashUrl(url);
            }
            Log.LogInformation($"Init for {host} tournament {tournamentId}");


            TournamentContext ret = new();
            try
            {
                if (host == TournamentHost.Challonge)
                {
                    var tournament = await GetChallongeTournament(tournamentId);
                    ret = new TournamentContext(tournament.Tournament);
                    ret.RequestErrors.AddRange(tournament.RequestErrors);
                }
                else if (host == TournamentHost.Smash)
                {
                    if (tournamentId == null)
                    {
                        ret.RequestErrors.Add("Invalid Start.gg url. Expects 'tournament/*/event/*' in url.");
                    }
                    else
                    {
                        var tournament = await GetSmashggTournament(tournamentId);
                        ret = new TournamentContext(tournament.Data?.Event, url, tournamentId);
                        ret.RequestErrors.AddRange(tournament.RequestErrors);
                    }
                }
                else
                {
                    ret.RequestErrors.Add("Could not parse challonge or start.gg url.");
                }
            }
            catch(Exception ex)
            {
                Log.LogError(ex, ex.Message);
                ret.RequestErrors.Add(ex.Message);
            }
            if (string.IsNullOrEmpty(ret.Url))
                ret.Url = url;
            if (ret.TournamentHost == TournamentHost.Unknown)
                ret.TournamentHost = host;
            if (string.IsNullOrEmpty(ret.TournamentId))
                ret.TournamentId = tournamentId;

            return ret;
        }

        public class PushScoreResponse
        {
            public bool Success { get; set; } = false;
            public List<string> Errors { get; } = new();
        }


        public async Task<PushScoreResponse> PushScore(MatchScoreReport report)
        {
            try
            {
                if (report.Tournament.Model?.TournamentHost == TournamentHost.Challonge)
                {
                    return await PushChallongeScore(report);
                }
                else if (report.Tournament.Model?.TournamentHost == TournamentHost.Smash)
                {
                    return await PushSmashggScore(report);
                }
            }
            catch(Exception ex)
            {
                Log.LogError(ex, ex.Message);
                var ret = new PushScoreResponse()
                {
                    Success = false
                };
                ret.Errors.Add(ex.Message);
                return ret;
            }

            var errRet = new PushScoreResponse()
            {
                Success = false
            };
            errRet.Errors.Add("Could not determine tournament host for match update.");
            Log.LogError("Could not determine tournament host for match update.");
            return errRet;
        }


        class ChallongePushScoreBody
        {
            public class MatchObject
            {
                [JsonProperty("scores_csv")]
                public string ScoresCsv { get; set; }
                [JsonProperty("winner_id")]
                public StringOrIntId WinnerId { get; set; }
            }
            [JsonProperty("match")]
            public MatchObject Match { get; set; }
        }

        public async Task<PushScoreResponse> PushChallongeScore(MatchScoreReport report)
        {

            PushScoreResponse ret = new();
            ret.Success = false;
            Log.LogDebug("Updating Challonge match {}", report.Match?.Id);
            if (report.Scores.Count != 2)
            {
                ret.Success = false;
                ret.Errors.Add($"Score report had incorrect number of scores. Expected 2, got {report.Scores.Count}.");
                return ret;
            }
            var challongeMatch = report.Match as TournamentMatch;
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{TournamentsEndpoint}/{report.Tournament.Model.TournamentId}/matches/{challongeMatch.Id}.json?api_key={SBSettingsService.CurrentSettings?.ChallongeApiKey}");

            ChallongePushScoreBody bodyObj = new()
            {
                Match = new()
            };
            bodyObj.Match.WinnerId = report.Scores.OrderByDescending(s => s.Wins).First().Player.Id;
            int p1 = 0;
            int p2 = 1;
            if (report.Scores[0].Player.Id == report.Match.Player2.Id)
            {
                p1 = 1;
                p2 = 0;
            }
            bodyObj.Match.ScoresCsv = $"{report.Scores[p1].Wins}-{report.Scores[p2].Wins}";

            string bodyJson = JsonConvert.SerializeObject(bodyObj);
            request.Content = new StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json");


            Log.LogInformation("Sending request {} body {}", request, bodyJson);
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                ret.Success = true;
                return ret;
            }
            else
            {
                string responseStr = await response.Content.ReadAsStringAsync();
                Log.LogError("Http response error when updating challonge match. Status: {} Content: '{}'", response.StatusCode, responseStr);
                ret.Success = false;
                ret.Errors.Add($"Http response error when updating challonge match. Status {response.StatusCode} ({(int)response.StatusCode}). See logs.");
                return ret;
            }
        }


        public List<ITournamentParticipant> GetPendingOpponents(ITournamentParticipant participant, TournamentView tournament)
        {
            var ret = new List<ITournamentParticipant>();
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

        public List<ITournamentMatch> GetPendingMatches(TournamentView tournament)
        {
            var ret = new List<ITournamentMatch>();
            if (tournament == null)
                return ret;

            foreach(var pair in tournament.Matches)
            {
                // Challonge reports them as Open, where Smash.gg reports them as pending.
                // As long as it isn't Complete or Unknown and there are two players, assume
                // that it is a pending match.
                if((pair.Value.Status == MatchStatus.Open || pair.Value.Status == MatchStatus.Pending)
                    && pair.Value.Player1 != null
                    && pair.Value.Player2 != null)
                {
                    ret.Add(pair.Value);
                }
            }
            return ret;
        }

        public List<string> GetAllMatchNames(TournamentView tournament)
        {
            if (tournament == null)
                return new List<string>();

            return tournament.Matches.Values.OrderBy(m => m.RoundNumber).Select(m => m.RoundName).Distinct().ToList();
        }

        struct LevenshteinDistance
        {
            public int Distance;
            public ITournamentParticipant Participant;
        }

        public List<ITournamentParticipant> ParticipantAutocompleteList(string input, TournamentView tournament)
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


        public ITournamentParticipant FindParticipant(string name, TournamentView tournament)
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

        public class TournamentGetResponse
        {
            [JsonProperty("tournament")]
            public ChallongeTournament Tournament { get; set; }
            public List<string> RequestErrors = new();
        }

        public async Task<TournamentGetResponse> GetChallongeTournament(string tournamentId)
        {
            Log.LogDebug("Getting challonge tournament");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{TournamentsEndpoint}/{tournamentId}.json?api_key={SBSettingsService.CurrentSettings?.ChallongeApiKey}&include_participants=1&include_matches=1");

            TournamentGetResponse ret = new();

            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            if(response.IsSuccessStatusCode)
            {
                string respContent = await response.Content.ReadAsStringAsync();
                Log.LogTrace("Tournament response: {}", respContent);
                ret = JsonConvert.DeserializeObject<TournamentGetResponse>(respContent);
                if(ret == null)
                {
                    string err = "Could not deserialize tournament response.";
                    Log.LogError(err);
                    ret = new();
                    ret.RequestErrors.Add(err);
                    return ret;
                }
                return ret;
            }
            else
            {
                string err = $"Tournament response error code {response.StatusCode} ({(int)response.StatusCode})";
                ret.RequestErrors.Add(err);
                Log.LogError(err);
                await Log422Errors(response);
                return ret;
            }
        }


        private async Task Log422Errors(HttpResponseMessage response)
        {
            if((int)response.StatusCode == 422)
            {
                string content = await response.Content.ReadAsStringAsync();
                Log.LogWarning($"Challonge Error content: {content}");
            }
        }


        const string SmashggEndpoint = @"https://api.start.gg/gql/alpha";
        /// <summary>
        /// Not sure if this is structured correctly
        /// </summary>
        public class SmashggErrorResponse
        {
            [JsonProperty("success")]
            public bool? Success { get; set; }
            [JsonProperty("fields")]
            public IList<string> Fields { get; set; } = null;
            [JsonProperty("message")]
            public string Message { get; set; }
            [JsonProperty("errorId")]
            public string ErrorId { get; set; }
        }

        public class SmashggPostResponse
        {
            public class DataResponse
            {
                /// <summary>
                /// Only valid for the query
                /// </summary>
                [JsonProperty("event")]
                public Smash.gg.Event Event { get; set; }
                /// <summary>
                /// Only valid for the mutation
                /// </summary>
                [JsonProperty("reportBracketSet")]
                public List<Smash.gg.Set> Sets { get; set; }
            }

            [JsonProperty("data")]
            public DataResponse Data { get; set; } = null;
            [JsonProperty("errors")]
            public List<SmashggErrorResponse> Errors { get; set; } = new();
            public List<string> RequestErrors = new();
        }

        public async Task<SmashggPostResponse> GetSmashggTournament(string tournamentId)
        {
            SmashggPostResponse firstPage = await GetSmashggTournament(tournamentId, 1);
            if(firstPage.RequestErrors.Count > 0)
            {
                return firstPage;
            }
            var firstPageSetConnection = firstPage.Data?.Event?.SetConnection;
            if(firstPageSetConnection == null)
            {
                return firstPage;
            }
            int? totalPages = firstPageSetConnection.PageInfo?.TotalPages;
            if (totalPages > 1)
            {
                for (int i = 2; i <= totalPages; i++)
                {
                    SmashggPostResponse nextPage = await GetSmashggTournament(tournamentId, i);
                    var newNodes = nextPage.Data?.Event?.SetConnection?.Nodes;
                    if (newNodes != null)
                    {
                        firstPageSetConnection.Nodes.AddRange(newNodes);
                    }
                    firstPage.RequestErrors.AddRange(nextPage.RequestErrors);
                }
            }
            return firstPage;
        }

        public async Task<SmashggPostResponse> GetSmashggTournament(string tournamentId, int page)
        {
            SmashggPostResponse ret = new();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{SmashggEndpoint}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SBSettingsService.CurrentSettings.SmashggApiKey);
            Data.Smash.gg.SmashGraphQLQuery queryObject = new();
            queryObject.Query = Smash.gg.FullEventQuery.FullQuery;
            queryObject.Variables = new()
            {
                { "slug", tournamentId },
                { "setPage", page },
                { "setsPerPage", 98 }
            };

            Log.LogInformation("Querying smash.gg with params: ${Params}", queryObject.Variables);
            request.Content = new StringContent(JsonConvert.SerializeObject(queryObject), System.Text.Encoding.UTF8, "application/json");

            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            SmashggPostResponse deserializedResponse = JsonConvert.DeserializeObject<SmashggPostResponse>(responseStr);
            if (deserializedResponse == null)
            {
                ret.RequestErrors.Add("Could not deserialize smashgg response. See logs for more details.");
                Log.LogError("Could not deserialize smashgg response with success status code {}. Content: \"{}\"", response.StatusCode, responseStr);
                deserializedResponse = new();
            }
            if (deserializedResponse.Errors?.Count > 0)
            {
                Log.LogError("Startgg response error. Status: {} , Content: \"{}\"", response.StatusCode, responseStr);
                foreach (var item in deserializedResponse.Errors)
                {
                    if (!string.IsNullOrEmpty(item?.Message))
                    {
                        ret.RequestErrors.Add($"Smashgg error: {item.Message}");
                    }
                    else
                    {
                        ret.RequestErrors.Add("Unspecified Smashgg error. See logs for more.");
                    }
                }
            }
            if (response.IsSuccessStatusCode)
            {
                ret.Data = deserializedResponse.Data;
                if (ret.Data == null)
                {
                    ret.RequestErrors.Add("Null data in response. Maybe error or incorrect url?");
                    Log.LogWarning("Null data response. Content: \"{}\"", responseStr);
                }

                return ret;
            }
            else
            {
                ret.Data = null;
                Log.LogError("Http response error. Status: {} , Content: \"{}\"", (int)response.StatusCode, responseStr);
                if(ret.RequestErrors.Count == 0)
                {
                    ret.RequestErrors.Add("Unspecified Smashgg error. See logs for more.");
                }
                return ret;
            }

        }



        /// <summary>
        /// </summary>
        /// <param name="report"></param>
        /// <returns></returns>
        public async Task<PushScoreResponse> PushSmashggScore(MatchScoreReport report)
        {
            PushScoreResponse ret = new PushScoreResponse();
            ret.Success = false;
            Log.LogDebug("Updating Smashgg match {}", report.Match?.Id);
            if (report.Scores.Count != 2)
            {
                ret.Success = false;
                ret.Errors.Add($"Score report had incorrect number of scores. Expected 2, got {report.Scores.Count}.");
                return ret;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{SmashggEndpoint}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SBSettingsService.CurrentSettings.SmashggApiKey);
            Smash.gg.SmashGraphQLQuery queryObject = new();
            queryObject.Query = Smash.gg.ReportScoreMutation.FullQuery;
            // Due to the way smashgg accepts scores, we have to do this complicated thing to
            // synthesize the match order
            List<Smash.gg.BracketSetGameDataInput> gameData = new();
            var maxScorePlayer = report.Scores[0];
            var minScorePlayer = report.Scores[1];
            if (report.Scores[1].Wins > report.Scores[0].Wins) 
            {
                maxScorePlayer = report.Scores[1];
                minScorePlayer = report.Scores[0];
            }

            int gameNum = 1;
            for (int i = 0; i < minScorePlayer.Wins; i++)
            {
                gameData.Add(new Smash.gg.BracketSetGameDataInput()
                {
                    WinnerId = minScorePlayer.Player.Id,
                    GameNum = gameNum++,
                });
            }
            for (int i = 0; i < maxScorePlayer.Wins; i++)
            {
                gameData.Add(new Smash.gg.BracketSetGameDataInput()
                {
                    WinnerId = maxScorePlayer.Player.Id,
                    GameNum = gameNum++,
                });
            }

            queryObject.Variables = new()
            {
                { "setId", report.Match.Id },
                { "winnerId", maxScorePlayer.Player.Id },
                { "gameData", gameData }
            };

            Log.LogInformation("Mutating smash.gg with params: ${Params}", queryObject.Variables);
            request.Content = new StringContent(JsonConvert.SerializeObject(queryObject), System.Text.Encoding.UTF8, "application/json");
            Log.LogTrace("Mutation request content: {}", JsonConvert.SerializeObject(queryObject));


            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            Log.LogTrace("Mutation reponse content: {}", responseStr);
            SmashggPostResponse deserializedResponse = JsonConvert.DeserializeObject<SmashggPostResponse>(responseStr);
            if (deserializedResponse == null)
            {
                ret.Errors.Add("Could not deserialize smashgg response. See logs for more details.");
                Log.LogError("Could not deserialize smashgg response with success status code {}. Content: \"{}\"", response.StatusCode, responseStr);
                deserializedResponse = new();
            }
            if (deserializedResponse.Errors?.Count > 0)
            {
                Log.LogError("Startgg response error. Status: {} , Content: \"{}\"", response.StatusCode, responseStr);
                foreach (var item in deserializedResponse.Errors)
                {
                    if (!string.IsNullOrEmpty(item?.Message))
                    {
                        ret.Errors.Add($"Smashgg error: {item.Message}");
                    }
                    else
                    {
                        ret.Errors.Add("Unspecified Smashgg error. See logs for more.");
                    }
                }
            }
            if (response.IsSuccessStatusCode)
            {
                ret.Success = (ret.Errors.Count == 0);
                return ret;
            }
            else
            {
                Log.LogError("Http response error. Status: {} , Content: \"{}\"", (int)response.StatusCode, responseStr);
                ret.Success = false;
                if (ret.Errors.Count == 0)
                {
                    ret.Errors.Add("Unspecified Smashgg error. See logs for details.");
                }
                return ret;
            }
        }

    }
}
