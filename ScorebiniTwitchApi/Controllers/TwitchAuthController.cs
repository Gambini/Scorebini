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
    internal class TwitchUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("login")]
        public string? Login { get; set; }
        [JsonPropertyName("broadcaster_type")]
        public string? BroadcasterType { get; set; }
    }

    internal record class TwitchAuthTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("refresh_token")]
        string RefreshToken,
        [property: JsonPropertyName("scope")]
        IList<string> Scope
    );

    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class TwitchAuthController : ControllerBase
    {
        private readonly IOptionsMonitor<TwitchOptions> TwitchConfig;
        private readonly ILogger<TwitchAuthController> Log;
        private readonly TwitchAppTokenService AppTokenService;
        private readonly IHttpClientFactory HttpFactory;
        private readonly AppDbContext DbContext;

        public TwitchAuthController(
            IOptionsMonitor<TwitchOptions> twitchConfig, 
            ILogger<TwitchAuthController> log,
            TwitchAppTokenService appTokenService,
            IHttpClientFactory httpFactory,
            AppDbContext dbContext
            )
        {
            TwitchConfig = twitchConfig;
            Log = log;
            AppTokenService = appTokenService;
            HttpFactory = httpFactory;
            DbContext = dbContext;
        }


        internal class TwitchUserResponse
        {
            [JsonPropertyName("data")]
            public TwitchUser[] Data { get; set; } = Array.Empty<TwitchUser>();
        }


        internal async Task<ApiResult<TwitchUserResponse?>> GetTwitchUserInfo(string login)
        {
            string uri = QueryHelpers.AddQueryString(@"https://api.twitch.tv/helix/users", "login", login);
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Add("Client-Id", TwitchConfig.CurrentValue.AppClientId);
            var client = HttpFactory.CreateClient("TwitchUserIdRequest");
            var response = await AppTokenService.SendAsyncWithAuthRetry(client, req, HttpContext.RequestAborted);
            var responseStr = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            Log.LogDebug("User response for login {login}: ({httpCode}) {response}", login, (int)response.StatusCode, responseStr);
            if (response.IsSuccessStatusCode)
            {
                var userResponse = JsonSerializer.Deserialize<TwitchUserResponse>(responseStr);
                if (userResponse == null)
                {
                    Log.LogError("Unable to parse TwitchUserResponse from response '{response}'", responseStr);
                    return new ApiResult<TwitchUserResponse?>(null, response.StatusCode, "Unable to parse response");
                }
                return new ApiResult<TwitchUserResponse?>(userResponse, response.StatusCode, "Success");
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ApiResult<TwitchUserResponse?>(null, response.StatusCode, "Too Many Requests");
            }
            else
            {
                Log.LogError("Unhandled error accesssing {uri} ({httpCode}): {response}", uri, (int)response.StatusCode, responseStr);
                return new ApiResult<TwitchUserResponse?>(null, response.StatusCode, "Unhandled Error.");
            }
        }

        [Route("AuthUser")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TwitchUser))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(BasicResponse))]
        [ProducesResponseType(StatusCodes.Status303SeeOther, Type = typeof(AuthorizeRedirectResponse))]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests, Type = typeof(BasicResponse))]
        public async Task<IActionResult> AuthUser(string login)
        {
            ScorebiniUserInfo? existingUser = await DbContext.Users
                .Where(user => user.TwitchInfo != null && user.TwitchInfo.Login == login)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (existingUser == null)
            {
                var getUserResponse = await GetTwitchUserInfo(login);
                if (getUserResponse.Obj == null)
                {
                    return StatusCode(getUserResponse.Code, new BasicResponse(
                        new(getUserResponse.Code, getUserResponse.Message ?? "Unknown Error"))
                        );
                }
                else
                {
                    if (getUserResponse.Obj.Data.Length == 0)
                    {
                        return NotFound(new BasicResponse(new(StatusCodes.Status404NotFound, "No user matches login.")));
                    }
                    var twitchUser = getUserResponse.Obj.Data[0];
                    if (twitchUser.Login == null || twitchUser.Id == null)
                    {
                        throw new Exception("Invalid twitch user. This should not happen.");
                    }
                    existingUser = new ScorebiniUserInfo()
                    {
                        ClientToken = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        TwitchInfo = new TwitchUserInfo(twitchUser.Login, twitchUser.Id, twitchUser.BroadcasterType),
                    };
                }
            }


            if (existingUser.TokenInfo == null)
            {
                string randomStateString = Guid.NewGuid().ToString();
                existingUser.RedirectInfo = new TwitchRedirectInfo()
                {
                    CSRFStateString = randomStateString,
                    AuthTime = DateTime.UtcNow,
                };
                Dictionary<string, string?> redirectParams = new()
                {
                    { "response_type", "code" },
                    { "client_id", TwitchConfig.CurrentValue.AppClientId },
                    { "redirect_uri", "http://localhost:8288/TwitchAuth/TwitchRedirect"},
                    { "scope", "channel:manage:predictions" },
                    { "state", existingUser.RedirectInfo.CSRFStateString }
                };

                string redirectUrl = QueryHelpers.AddQueryString(@"https://id.twitch.tv/oauth2/authorize", redirectParams);
                DbContext.Update(existingUser);
                DbContext.SaveChanges();
                return StatusCode(StatusCodes.Status303SeeOther,
                    new AuthorizeRedirectResponse(new(StatusCodes.Status303SeeOther, "Follow this url to authorize Scorebini."),
                    redirectUrl
                    ));
            }
            else
            {
                return Ok(new BasicResponse(new(StatusCodes.Status200OK, $"User {login} is authorized.")));
            }
        }


        /// <summary>
        /// Only used in twitch->scorebini communication
        /// </summary>
        /// <param name="code"></param>
        /// <param name="scope"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [Route("TwitchRedirect")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> TwitchRedirect(string code, string scope, string state)
        {
            Log.LogInformation("Received new twitch redirect.");
            ScorebiniUserInfo? user = await DbContext.Users
                .Where(user => user.RedirectInfo != null && user.RedirectInfo.CSRFStateString == state)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (user == null)
            {
                Log.LogWarning("Could not find user waiting on state string {state}", state);
                return Ok();
            }

            user.RedirectInfo = null;
            DbContext.Update(user);
            DbContext.SaveChanges();

            Dictionary<string, string> authValues = new()
            {
                { "client_id", TwitchConfig.CurrentValue.AppClientId },
                { "client_secret", TwitchConfig.CurrentValue.AppClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", "http://localhost:8288/TwitchAuth/TwitchRedirect" }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, @"https://id.twitch.tv/oauth2/token");
            request.Content = new FormUrlEncodedContent(authValues);
            var client = HttpFactory.CreateClient("TwitchTokenRequest");
            var response = await client.SendAsync(request);
            var responseStr = await response.Content.ReadAsStringAsync();
            Log.LogDebug("Response from {uri} for user {user}: {response}", request.RequestUri?.ToString(), user.TwitchInfo?.Login, responseStr);
            if (response.IsSuccessStatusCode)
            {
                var obj = JsonSerializer.Deserialize<TwitchAuthTokenResponse>(responseStr);
                if (obj == null)
                {
                    Log.LogError("Unable to parse TwitchAuthTokenResponse from response string {response}", responseStr);
                    return Ok();
                }
                user.TokenInfo = new TwitchTokenInfo(obj);
                DbContext.Update(user);
                DbContext.SaveChanges();
                return Ok();
            }
            else
            {
                return Ok();
            }

        }

    }
}
