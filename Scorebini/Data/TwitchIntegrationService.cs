using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ScorebiniTwitchApi.Shared.Requests;
using ScorebiniTwitchApi.Shared.Responses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public class TwitchIntegrationService
    {
        private readonly ILogger<TwitchIntegrationService> Log;
        private readonly IHttpClientFactory HttpFactory;
        private readonly List<TwitchUserState> UserCache = new();
        private readonly object UserListLock = new object();

        public TwitchIntegrationService(
            ILogger<TwitchIntegrationService> log,
            IHttpClientFactory httpFactory
            ) 
        {
            Log = log;
            HttpFactory = httpFactory;
        }

        public TwitchUserState GetUser(string twitchLogin)
        {
            lock(UserListLock)
            {
                foreach (var user in UserCache)
                {
                    if (user.Login == twitchLogin)
                    {
                        return user;
                    }
                }

                TwitchUserState newUser = new TwitchUserState();
                newUser.Login = twitchLogin;
                UserCache.Add(newUser);
                return newUser;
            }
        }

        private static string MakeScorebiniUrl(ScoreboardSettings settings, string rest)
        {
            string baseUrl = settings.ScorebiniTwitchApi.TrimEnd('/');
            string next = rest.TrimStart('/');
            return baseUrl + "/" + next;
        }


        public async Task AuthUser(TwitchUserState user, ScoreboardSettings settings)
        {
            user.State = ScorebiniTwitchUserState.RequestInProgres;
            string url = MakeScorebiniUrl(settings, "TwitchAuth/AuthUser");
            url = QueryHelpers.AddQueryString(url, "login", user.Login);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var basicResponse = JsonConvert.DeserializeObject<BasicResponse>(responseStr);
            if (basicResponse == null)
            {
                Log.LogError("Unable to deserialize response '{response}'", responseStr);
                user.MostRecentError = "Unable to deserialize auth response";
                user.MostRecentResponseMeta = null;
                user.State = ScorebiniTwitchUserState.Error;
            }
            else
            {
                user.MostRecentError = null;
                user.MostRecentResponseMeta = basicResponse.Meta;
            }


            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                user.State = ScorebiniTwitchUserState.Authed;
                return;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.SeeOther)
            {
                var redirectResponse = JsonConvert.DeserializeObject<AuthorizeRedirectResponse>(responseStr);
                if (redirectResponse == null)
                {
                    Log.LogError("Unable to deserialize redirect response '{response}'", responseStr);
                    user.MostRecentError = "Unable to deserialize auth redirect response";
                    user.State = ScorebiniTwitchUserState.Error;
                    return;
                }
                user.RedirectUrl = redirectResponse.RedirectUri;
                user.State = ScorebiniTwitchUserState.AwaitingAuth;
                return;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                user.State = ScorebiniTwitchUserState.Error;
                user.MostRecentError = basicResponse?.Meta?.Message ?? "Unknown 404";
                return;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                user.State = ScorebiniTwitchUserState.Error;
                user.MostRecentError = basicResponse?.Meta?.Message ?? "Too many requests";
                return;
            }
        }

        public async Task<bool> ScorebiniApiHealthCheck(ScoreboardSettings settings)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, MakeScorebiniUrl(settings, "Health"));
            using var client = HttpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<GetCurrentPredictionResponse> GetCurrentPrediction(ScoreboardSettings settings, string login)
        {
            string url = MakeScorebiniUrl(settings, "/TwitchPrediction/CurrentPrediction");
            url = QueryHelpers.AddQueryString(url, "login", login);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<GetCurrentPredictionResponse>(responseStr);
            if(responseObj == null)
            {
                int statusCode = (int)response.StatusCode;
                if (statusCode == 200)
                    statusCode = -1;
                Log.LogError("Unable to deserialize GetCurrentPredictionResponse from '{response}'", responseStr);
                return new GetCurrentPredictionResponse(new(statusCode, "Could not deserialize response."), null);
            }
            return responseObj;
        }

        public async Task<CreatePredictionResponse> CreatePrediction(ScoreboardSettings settings, CreatePrediction requestObj)
        {
            string url = MakeScorebiniUrl(settings, "/TwitchPrediction/CreatePrediction");
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = JsonContent.Create(requestObj);
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<CreatePredictionResponse>(responseStr);
            if(responseObj == null)
            {
                int statusCode = (int)response.StatusCode;
                if (statusCode == 200)
                    statusCode = -1;
                Log.LogError("Unable to deserialize CreatePredictionResponse from '{response}'", responseStr);
                return new CreatePredictionResponse(new(statusCode, "Could not deserialize response."), null);
            }
            return responseObj;
        }


        public async Task<EndPredictionResponse> EndPrediction(ScoreboardSettings settings, EndPrediction requestObj)
        {
            string url = MakeScorebiniUrl(settings, "/TwitchPrediction/EndPrediction");
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = JsonContent.Create(requestObj);
            using var client = HttpFactory.CreateClient();
            using var response = await client.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<EndPredictionResponse>(responseStr);
            if(responseObj == null)
            {
                int statusCode = (int)response.StatusCode;
                if (statusCode == 200)
                    statusCode = -1;
                Log.LogError("Unable to deserialize EndPredictionResponse from '{response}'", responseStr);
                return new EndPredictionResponse(new(statusCode, "Could not deserialize response."), null);
            }
            return responseObj;
        }

    }
}
