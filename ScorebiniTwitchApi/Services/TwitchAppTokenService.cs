
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScorebiniTwitchApi.Services
{

    public class TwitchAppTokenService
    {
        const string TWITCH_TOKEN_URL = @"https://id.twitch.tv/oauth2/token";
        private string? CurrentToken { get; set; }
        private readonly SemaphoreSlim TokenLock; 

        private readonly ILogger<TwitchAppTokenService> Log;
        private readonly IOptionsMonitor<TwitchOptions> TwitchConfig;
        private readonly IHttpClientFactory HttpFactory;

        public TwitchAppTokenService(
            ILogger<TwitchAppTokenService> logger,
            IOptionsMonitor<TwitchOptions> twitchConfig,
            IHttpClientFactory httpFactory)
        {
            Log = logger;
            TwitchConfig = twitchConfig;
            HttpFactory = httpFactory;
            TokenLock = new SemaphoreSlim(1, 1);
        }

        public async Task<string> GetOrRequestCurrentToken(CancellationToken cancelToken)
        {
            await TokenLock.WaitAsync(cancelToken);
            try
            {
                if (CurrentToken == null)
                {
                    CurrentToken = await RequestNewToken(cancelToken);
                }
                return CurrentToken;
            }
            finally
            {
                TokenLock.Release();
            }
        }

        public async Task<string> ForceUpdateCurrentToken(CancellationToken cancelToken)
        {
            await TokenLock.WaitAsync(cancelToken);
            try
            {
                CurrentToken = await RequestNewToken(cancelToken);
                return CurrentToken;
            }
            finally
            {
                TokenLock.Release();
            }
        }


        /// <summary>
        /// Passthrough client.SendAsync(request) on the happy path.
        /// If the response is a 401, then we get a new token and retry the response with the new token.
        /// If the response continues to 401 after 1 retry, we don't continue retrying.
        /// This function sets the Authorization header.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="makeRequestFn"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendAsyncWithAuthRetry(HttpClient client, Func<HttpRequestMessage> makeRequestFn, CancellationToken cancelToken)
        {
            string appToken = await GetOrRequestCurrentToken(cancelToken);
            var request = makeRequestFn();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", appToken);
            var response = await client.SendAsync(request, cancelToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Log.LogInformation("App token became invalid.");
                response.Dispose(); // dispose here because we don't have a `using` statement
                string newToken = await ForceUpdateCurrentToken(cancelToken);
                var request2 = makeRequestFn(); // you are not supposed to re-use HttpRequestMessage objects, so create a new one
                request2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
                return await client.SendAsync(request2);
            }
            return response;
        }

        public class AppTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
            [JsonPropertyName("expires_in")]
            public long? ExpiresIn { get; set; }
            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }
        }

        private async Task<string> RequestNewToken(CancellationToken cancelToken)
        {
            Log.LogDebug("Requesting new app token");
            using var req = new HttpRequestMessage(HttpMethod.Post, TWITCH_TOKEN_URL);
            Dictionary<string, string> postData = new()
            {
                { "client_id", TwitchConfig.CurrentValue.AppClientId },
                { "client_secret", TwitchConfig.CurrentValue.AppClientSecret },
                { "grant_type", "client_credentials" },
            };
            req.Content = new FormUrlEncodedContent(postData);
            using var client = HttpFactory.CreateClient("AppTokenRequest");
            using var resp = await client.SendAsync(req, cancelToken);
            var respStr = await resp.Content.ReadAsStringAsync(cancelToken);
            if (resp.IsSuccessStatusCode)
            {
                var respValue = JsonSerializer.Deserialize<AppTokenResponse>(respStr);
                if (respValue == null)
                {
                    Log.LogError("Could not parse AppTokenResponse from response {respStr}", respStr);
                    throw new InvalidDataException("Could not deserialize AppTokenResponse");
                }
                if (string.IsNullOrEmpty(respValue.AccessToken))
                {
                    Log.LogError("Received empty access token in response from twich");
                    throw new InvalidDataException("Received empty access token in response from twich");
                }
                Log.LogInformation("Received new app access token");
                return respValue.AccessToken;
            }
            else
            {
                throw new NotImplementedException($"Received failure code {resp.StatusCode} ({(int)resp.StatusCode}): {respStr}");
            }
        }
    }
}
