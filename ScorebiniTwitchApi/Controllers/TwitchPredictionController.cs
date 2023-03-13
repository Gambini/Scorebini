using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ScorebiniTwitchApi.Models;
using ScorebiniTwitchApi.Services;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ScorebiniTwitchApi.Controllers
{

    internal static class PredictionStatusExtensions
    {
        internal static Shared.Responses.PredictionStatus ResponseFromString(string str)
        {
            return str switch
            {
                "ACTIVE" => Shared.Responses.PredictionStatus.Active,
                "CANCELED" => Shared.Responses.PredictionStatus.Canceled,
                "LOCKED" => Shared.Responses.PredictionStatus.Locked,
                "RESOLVED" => Shared.Responses.PredictionStatus.Resolved,
                _ => throw new Exception($"Unexpected status string '{str}'")
            };
        }

        internal static string StatusToString(this Shared.Requests.EndPredictionStatus status)
        {
            return status switch
            {
                Shared.Requests.EndPredictionStatus.Resolved => "RESOLVED",
                Shared.Requests.EndPredictionStatus.Canceled => "CANCELED",
                Shared.Requests.EndPredictionStatus.Locked => "LOCKED",
                _ => throw new Exception($"Unexpected status enum '{status}'")
            };
        }
    }

    internal record class TwitchResponsePredictionOutcome(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("title")]
        string Title,
        [property: JsonPropertyName("users")]
        int Users,
        [property: JsonPropertyName("channel_points")]
        int ChannelPoints,
        [property: JsonPropertyName("color")]
        string Color)
    {
        internal Shared.Responses.PredictionOutcome ToResponseOutcome()
        {
            return new Shared.Responses.PredictionOutcome(
                Id,
                Title,
                Users,
                ChannelPoints,
                Color
            );
        }
    }

    internal record class TwitchResponsePrediction(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("broadcaster_id")]
        string BroadcasterId,
        [property: JsonPropertyName("broadcaster_name")]
        string BroadcasterName,
        [property: JsonPropertyName("broadcaster_login")]
        string BroadcasterLogin,
        [property: JsonPropertyName("title")]
        string Title,
        [property: JsonPropertyName("winning_outcome_id")]
        string? WinningOutcomeId,
        [property: JsonPropertyName("outcomes")]
        List<TwitchResponsePredictionOutcome> Outcomes,
        [property: JsonPropertyName("prediction_window")]
        int PredictionWindow,
        [property: JsonPropertyName("status")]
        string Status,
        [property: JsonPropertyName("created_at")]
        string CreatedAt,
        [property: JsonPropertyName("ended_at")]
        string? EndedAt,
        [property: JsonPropertyName("locked_at")]
        string? LockedAt
        )
    {
        internal Shared.Responses.Prediction ToResponsePrediction()
        {
            return new Shared.Responses.Prediction(
                Id,
                BroadcasterId,
                BroadcasterName,
                BroadcasterLogin,
                Title,
                WinningOutcomeId,
                Outcomes.Select(o => o.ToResponseOutcome()).ToList(),
                PredictionWindow,
                PredictionStatusExtensions.ResponseFromString(Status),
                CreatedAt,
                EndedAt,
                LockedAt
            );
        }
    }


    internal record class TwitchPredictionResponseMessage(
        [property: JsonPropertyName("data")]
        List<TwitchResponsePrediction>? Data,
        [property: JsonPropertyName("message")]
        string? Message
    );


    internal record class TwitchPredictionOutcomeRequest(
        [property: JsonPropertyName("title")]
        string Title
    );

    internal record class TwitchCreatePredictionRequest(
        [property: JsonPropertyName("broadcaster_id")]
        string BroadcasterId,
        [property: JsonPropertyName("title")]
        string Title,
        [property: JsonPropertyName("outcomes")]
        List<TwitchPredictionOutcomeRequest> Outcomes,
        [property: JsonPropertyName("prediction_window")]
        int PredictionWindow
    );


    internal record class TwitchEndPredicitonRequest(
        [property: JsonPropertyName("broadcaster_id")]
        string BroadcasterId,
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("status")]
        string Status,
        [property: JsonPropertyName("winning_outcome_id")]
        string? WinningOutcomeId
    );



    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class TwitchPredictionController : ControllerBase
    {
        private readonly IOptionsMonitor<TwitchOptions> TwitchConfig;
        private readonly ILogger<TwitchPredictionController> Log;
        private readonly TwitchAppTokenService AppTokenService;
        private readonly IHttpClientFactory HttpFactory;
        private readonly AppDbContext DbContext;
        private readonly TokenRefreshService UserTokenRefreshService;

        public TwitchPredictionController(
            IOptionsMonitor<TwitchOptions> twitchConfig, 
            ILogger<TwitchPredictionController> log, 
            TwitchAppTokenService appTokenService, 
            IHttpClientFactory httpFactory, 
            AppDbContext dbContext,
            TokenRefreshService tokenRefresh
            )
        {
            TwitchConfig = twitchConfig;
            Log = log;
            AppTokenService = appTokenService;
            HttpFactory = httpFactory;
            DbContext = dbContext;
            UserTokenRefreshService = tokenRefresh;
        }


        [Route("CurrentPrediction")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Shared.Responses.GetCurrentPredictionResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Shared.Responses.BasicResponse))] // If no user matches the login
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(Shared.Responses.BasicResponse))] // pass through from twitch 
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(Shared.Responses.BasicResponse))] // pass through from twitch
        public async Task<IActionResult> GetCurrentPrediction(string login)
        {
            ScorebiniUserInfo? existingUser = await DbContext.Users
                .Where(user => user.TwitchInfo != null && user.TwitchInfo.Login == login)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if(existingUser == null)
            {
                return NotFound(new Shared.Responses.BasicResponse(new(404, "No user matches login.")));
            }

            string uri = QueryHelpers.AddQueryString(@"https://api.twitch.tv/helix/predictions",
                new Dictionary<string, string?>()
                {
                    { "broadcaster_id", existingUser.TwitchInfo?.TwitchId },
                    { "first", "1" }
                });

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", existingUser.TokenInfo?.AccessToken);
            request.Headers.Add("Client-Id", TwitchConfig.CurrentValue.AppClientId);
            using var httpClient = HttpFactory.CreateClient("CurrentPrediction");
            using var response = await httpClient.SendAsync(request, HttpContext.RequestAborted);
            string responseStr = await response.Content.ReadAsStringAsync();
            Log.LogDebug("Response from {uri} for user {user}: {response}", uri, login, responseStr);
            var responseObj = JsonSerializer.Deserialize<TwitchPredictionResponseMessage>(responseStr);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (responseObj == null)
                {
                    Log.LogError("Unable to parse TwitchPredictionResponseMessage from '{ResponseStr}'", responseStr);
                    return StatusCode(StatusCodes.Status500InternalServerError, new Shared.Responses.BasicResponse(new(500, "Unable to parse response.")));
                }
                if (responseObj.Data?.Count > 0)
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "Success"), responseObj.Data[0].ToResponsePrediction()));
                }
                else
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "No predictions in response."), null));
                }
            }
            else
            {

                return StatusCode((int)response.StatusCode, new Shared.Responses.BasicResponse(new((int)response.StatusCode, responseObj?.Message ?? "Unhandled error from twitch.")));
            }
        }

        [Route("CreatePrediction")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Shared.Responses.CreatePredictionResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(Shared.Responses.BasicResponse))]
        public async Task<IActionResult> CreatePrediction([FromBody] Shared.Requests.CreatePrediction createRequest)
        {
            string login = createRequest.Login;
            ScorebiniUserInfo? existingUser = await DbContext.Users
                .Where(user => user.TwitchInfo != null && user.TwitchInfo.Login == login)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if(existingUser == null)
            {
                return NotFound(new Shared.Responses.BasicResponse(new(404, "No user matches login.")));
            }

            if(createRequest.Title.Length > 45)
            {
                createRequest = createRequest with { Title = createRequest.Title.Substring(0, 45) };
            }

            for (int i = 0; i < createRequest.Outcomes.Count; i++)
            {
                var outcome = createRequest.Outcomes[i];
                if(outcome.Title.Length > 25)
                {
                    createRequest.Outcomes[i] = outcome with { Title = outcome.Title.Substring(0, 25) };
                }
            }

            if(createRequest.PredictionWindow < 30 || createRequest.PredictionWindow > 1800)
            {
                createRequest = createRequest with { PredictionWindow = Math.Clamp(createRequest.PredictionWindow, 30, 1800) };
            }

            var twitchRequestObj = new TwitchCreatePredictionRequest(
                existingUser.TwitchInfo!.TwitchId,
                createRequest.Title,
                createRequest.Outcomes.Select(o => new TwitchPredictionOutcomeRequest(o.Title)).ToList(),
                createRequest.PredictionWindow
            );

            using var msg = new HttpRequestMessage(HttpMethod.Post, @"https://api.twitch.tv/helix/predictions");
            msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", existingUser.TokenInfo?.AccessToken);
            msg.Headers.Add("Client-Id", TwitchConfig.CurrentValue.AppClientId);
            msg.Content = JsonContent.Create(twitchRequestObj);
            using var httpClient = HttpFactory.CreateClient();
            using var resp = await httpClient.SendAsync(msg, HttpContext.RequestAborted);
            var responseStr = await resp.Content.ReadAsStringAsync();
            Log.LogDebug("Response to create prediction {prediction} for user {user}: {response}", createRequest.Title, createRequest.Login, responseStr);
            var responseObj = JsonSerializer.Deserialize<TwitchPredictionResponseMessage>(responseStr);

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                if (responseObj == null)
                {
                    Log.LogError("Unable to parse TwitchPredictionResponseMessage from '{ResponseStr}'", responseStr);
                    return StatusCode(StatusCodes.Status500InternalServerError, new Shared.Responses.BasicResponse(new(500, "Unable to parse response.")));
                }
                if (responseObj.Data?.Count > 0)
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "Success"), responseObj.Data[0].ToResponsePrediction()));
                }
                else
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "No predictions in response."), null));
                }
            }
            else
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    UserTokenRefreshService.RefreshTokenFireAndForget(existingUser.ClientToken);
                }
                return StatusCode((int)resp.StatusCode, new Shared.Responses.BasicResponse(new((int)resp.StatusCode, responseObj?.Message ?? "Unhandled error from twitch.")));
            }
        }

        [Route("EndPrediction")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Shared.Responses.EndPredictionResponse))]
        public async Task<IActionResult> EndPrediction([FromBody] Shared.Requests.EndPrediction endPred)
        {
            string twitchId = endPred.BroadcasterId;
            ScorebiniUserInfo? existingUser = await DbContext.Users
                .Where(user => user.TwitchInfo != null && user.TwitchInfo.TwitchId == twitchId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if(existingUser == null)
            {
                return NotFound(new Shared.Responses.BasicResponse(new(404, "No user matches login.")));
            }
            var twitchRequestObj = new TwitchEndPredicitonRequest(
                existingUser.TwitchInfo!.TwitchId,
                endPred.PredictionId,
                endPred.Status.StatusToString(),
                endPred.WinningOutcomeId
            );
            using var msg = new HttpRequestMessage(HttpMethod.Patch, @"https://api.twitch.tv/helix/predictions");
            msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", existingUser.TokenInfo?.AccessToken);
            msg.Headers.Add("Client-Id", TwitchConfig.CurrentValue.AppClientId);
            msg.Content = JsonContent.Create(twitchRequestObj);
            using var httpClient = HttpFactory.CreateClient();
            using var resp = await httpClient.SendAsync(msg, HttpContext.RequestAborted);
            var responseStr = await resp.Content.ReadAsStringAsync();
            Log.LogDebug("Response to end prediction {prediction} for user {user}: {response}", endPred.PredictionId, existingUser.TwitchInfo?.Login, responseStr);
            var responseObj = JsonSerializer.Deserialize<TwitchPredictionResponseMessage>(responseStr);

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                if (responseObj == null)
                {
                    Log.LogError("Unable to parse TwitchPredictionResponseMessage from '{ResponseStr}'", responseStr);
                    return StatusCode(StatusCodes.Status500InternalServerError, new Shared.Responses.BasicResponse(new(500, "Unable to parse response.")));
                }
                if (responseObj.Data?.Count > 0)
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "Success"), responseObj.Data[0].ToResponsePrediction()));
                }
                else
                {
                    return Ok(new Shared.Responses.GetCurrentPredictionResponse(new(200, "No predictions in response."), null));
                }
            }
            else
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    UserTokenRefreshService.RefreshTokenFireAndForget(existingUser.ClientToken);
                }
                return StatusCode((int)resp.StatusCode, new Shared.Responses.BasicResponse(new((int)resp.StatusCode, responseObj?.Message ?? "Unhandled error from twitch.")));
            }
        }
    }
}
