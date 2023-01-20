using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScorebiniTwitchApi.Controllers;
using ScorebiniTwitchApi.Models;
using System.Text.Json;

namespace ScorebiniTwitchApi.Services
{
    /// <summary>
    /// Because there are several places where this application can request
    /// a refresh token and they can happen simultaneously, this is an
    /// attempt to ensure only one refresh token is requested from twitch per token at a time.
    /// </summary>
    public class TokenRefreshService : IHostedService, IDisposable
    {
        internal record class WorkItem(
            TaskCompletionSource<ApiResult<TwitchTokenInfo?>> TaskSource,
            Guid ScorebiniUserId
        );

        private readonly List<WorkItem> InProgressRequests = new();
        private readonly object InProgressLock = new();

        private readonly CancellationTokenSource ShutdownTokenSource = new CancellationTokenSource();

        private readonly ILogger Log;
        private readonly IOptionsMonitor<TwitchOptions> TwitchConfig;
        private readonly IServiceScopeFactory ScopeFactory;

        public TokenRefreshService(ILogger<TokenRefreshService> log,
            IOptionsMonitor<TwitchOptions> twitchConfig,
            IServiceScopeFactory scopeFactory
            )
        {
            Log = log;
            TwitchConfig = twitchConfig;
            ScopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Just cancel all of the work items, don't wait for them to refresh
            ShutdownTokenSource.Cancel();
            return Task.CompletedTask;
        }

        internal async Task<ApiResult<TwitchTokenInfo?>> RefreshToken(Guid scorebiniUserId)
        {
            if (ShutdownTokenSource.IsCancellationRequested)
            {
                return new(null, 500, "Canceled.");
            }
            bool foundExistingWork = false;
            WorkItem? work = null;
            // Attempt to ensure only one refresh request per user.
            // The timing should line up correctly most of the time, but there is
            // still a chance that a token is refreshed multiple times.
            lock (InProgressLock)
            {
                foreach (var item in InProgressRequests)
                {
                    if (item.ScorebiniUserId == scorebiniUserId)
                    {
                        foundExistingWork = true;
                        work = item;
                        break;
                    }
                }
                if(work == null)
                {
                    foundExistingWork = false;
                    work = new WorkItem(
                        new TaskCompletionSource<ApiResult<TwitchTokenInfo?>>(),
                        scorebiniUserId
                        );
                    InProgressRequests.Add(work);
                }
            }

            if (foundExistingWork)
            {
                return await work.TaskSource.Task;
            }

            try
            {
                var ret = await RefreshToken(work);
                work.TaskSource.SetResult(ret);
                return ret;
            }
            catch (Exception ex)
            {
                Log.LogError("Exception attempting to refresh token for user {guid}: {exception}", scorebiniUserId, ex);
                work.TaskSource.SetException(ex);
                throw;
            }
            finally
            {
                lock (InProgressLock)
                {
                    InProgressRequests.Remove(work);
                }
            }
        }

        /// <summary>
        /// It is intended for the caller to set the result on the task source and deal with the in progress requests list.
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        private async Task<ApiResult<TwitchTokenInfo?>> RefreshToken(WorkItem work)
        {
            using var scope = ScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var user = await dbContext.Users.Where(user => user.ClientToken == work.ScorebiniUserId).FirstOrDefaultAsync();
            string userLogName = user?.TwitchInfo?.Login ?? work.ScorebiniUserId.ToString();
            if (user == null)
            {
                return new ApiResult<TwitchTokenInfo?>(null, StatusCodes.Status404NotFound, $"Could not find user guid {work.ScorebiniUserId} ({userLogName})");
            }
            if (user.TokenInfo == null)
            {
                return new ApiResult<TwitchTokenInfo?>(null, StatusCodes.Status404NotFound, $"User {userLogName} has no token to refresh");
            }
            Log.LogInformation("Refreshing auth token for user {user}", userLogName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
            Dictionary<string, string> refreshRequestValues = new()
            {
                { "client_id", TwitchConfig.CurrentValue.AppClientId },
                { "client_secret", TwitchConfig.CurrentValue.AppClientSecret },
                { "grant_type", "refresh_token" },
                { "refresh_token", user.TokenInfo.RefreshToken }
            };
            request.Content = new FormUrlEncodedContent(refreshRequestValues);
            var client = httpFactory.CreateClient("RefreshToken");
            using var response = await client.SendAsync(request, ShutdownTokenSource.Token);
            var responseStr = await response.Content.ReadAsStringAsync();
            Log.LogDebug("User {user} refresh token response: {response}", userLogName, responseStr);
            if (response.IsSuccessStatusCode)
            {
                var obj = JsonSerializer.Deserialize<TwitchAuthTokenResponse>(responseStr);
                if (obj == null)
                {
                    throw new NullReferenceException($"Unable to deserialized TwitchAuthTokenResponse from response {responseStr}");
                }
                user.TokenInfo = new TwitchTokenInfo(obj);
                dbContext.Update(user);
                dbContext.SaveChanges();
                return new ApiResult<TwitchTokenInfo?>(user.TokenInfo, StatusCodes.Status200OK, "Successful refresh.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Log.LogWarning("Invalid refresh token for user {user}. Removing auth token, user will have to re-authorize Scorebini.", userLogName);
                user.TokenInfo = null;
                dbContext.Update(user);
                dbContext.SaveChanges();
                return new ApiResult<TwitchTokenInfo?>(null, StatusCodes.Status400BadRequest, $"Invalid refresh token for user {userLogName}.");
            }
            else
            {
                Log.LogError("Unhandled status code {code} when refreshing token for user {user}. Response: {response}", (int)response.StatusCode, userLogName, responseStr);
                return new(null, StatusCodes.Status500InternalServerError, $"Unhandled status code {(int)response.StatusCode} when refreshing token");
            }

        }

        public void Dispose()
        {
            ShutdownTokenSource.Dispose();
        }
    }
}
