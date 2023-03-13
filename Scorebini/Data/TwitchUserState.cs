namespace Scorebini.Data
{

    using ScorebiniTwitchApi.Shared.Responses;

    public enum ScorebiniTwitchUserState
    {
        Unknown = 0,
        Error,
        Authed,
        /// <summary>
        /// Waiting for the user to authorize the app through the redirect url
        /// </summary>
        AwaitingAuth,
        /// <summary>
        /// This is a general "in progress" indicator. Might be authed or not.
        /// </summary>
        RequestInProgres,
    }

    public class TwitchUserState
    {
        public string Login { get; set; }
        public ScorebiniTwitchUserState State { get; set; } = ScorebiniTwitchUserState.Unknown;
        public string RedirectUrl { get; set; }
        public string MostRecentError { get; set; }
        public ResponseCommonMetadata MostRecentResponseMeta { get; set; }
        /// <summary>
        /// Might be null if no active predictions.
        /// </summary>
        public GetCurrentPredictionResponse CurrentPrediction { get; set; }
    }
}
