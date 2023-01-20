using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScorebiniTwitchApi.Models;

namespace ScorebiniTwitchApi.Services
{
    /// <summary>
    /// Acoording to https://dev.twitch.tv/docs/authentication/validate-tokens/ we must
    /// validate tokens every hour or we get messaged by twitch support.
    /// </summary>
    public class TokenValidationBackgroundService : BackgroundService
    {
        private readonly ILogger<TokenValidationBackgroundService> Log;
        private readonly IOptionsMonitor<TwitchOptions> TwitchConfig;
        private readonly IServiceScopeFactory ScopeFactory;
        private readonly TokenRefreshService TokenRefreshService;

        public TokenValidationBackgroundService(
            ILogger<TokenValidationBackgroundService> log,
            IOptionsMonitor<TwitchOptions> twitchConfig,
            IServiceScopeFactory scopeFactory,
            TokenRefreshService tokenRefresh
            )
        {
            Log = log;
            TwitchConfig = twitchConfig;
            ScopeFactory = scopeFactory;
            TokenRefreshService = tokenRefresh;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () => await BackgroundPollLoop(stoppingToken), CancellationToken.None);
        }

        private async Task BackgroundPollLoop(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Start refreshing immediately in case our app server was restarted.
                // Or should we not do this?
                using var scope = ScopeFactory.CreateScope();
                var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpFactory.CreateClient("ValidateClient");
                ScorebiniUserInfo[] usersWithTokens;
                using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                {
                    usersWithTokens = dbContext.Users.AsNoTracking().Where(user => user.TokenInfo != null).ToArray();
                }
                foreach (var user in usersWithTokens)
                {
                    await ValidateToken(user, httpClient, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(TwitchConfig.CurrentValue.TokenValidateIntervalMinutes), stoppingToken);
            }
        }

        private async Task ValidateToken(ScorebiniUserInfo user, HttpClient client, CancellationToken cancelToken)
        {
            string userLogName = user.TwitchInfo?.Login ?? user.ClientToken.ToString();
            Log.LogInformation("Validating token for user {user}", userLogName);
            using var validateRequest = new HttpRequestMessage(HttpMethod.Get, @"https://id.twitch.tv/oauth2/validate");
            validateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.TokenInfo!.AccessToken);
            using var response = await client.SendAsync(validateRequest, cancelToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await TokenRefreshService.RefreshToken(user.ClientToken);
            }
        }
    }
}
