using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScorebiniTwitchApi.Shared.Requests
{
    /// <summary>
    /// Specify one of the possible voting options
    /// </summary>
    /// <param name="Title">Text that viewer will see and may select. Maximum of 25 characters</param>
    public record class CreatePredictionOutcome(string Title);

    /// <summary>
    /// Creates a Channel Points Prediction.
    /// With a Channel Points Prediction, the broadcaster poses a question and viewers try to predict the outcome.
    /// The prediction runs as soon as it’s created. The broadcaster may run only one prediction at a time.
    /// </summary>
    /// <param name="Login">Twitch username</param>
    /// <param name="Title">Question broadcaster is asking. Limited to 45 characters.</param>
    /// <param name="Outcomes">List of possible outcomes a user may choose. Requires between 2 and 10 options.</param>
    /// <param name="PredictionWindow">
    /// The length of time (in seconds) that the prediction will run for. 
    /// The minimum is 30 seconds and the maximum is 1800 seconds (30 minutes).
    /// </param>
    public record class CreatePrediction(
        string Login,
        string Title,
        List<CreatePredictionOutcome> Outcomes,
        int PredictionWindow
    );

    public enum EndPredictionStatus
    {
        /// <summary>
        /// The winning outcome is determined and the Channel Points 
        /// are distributed to the viewers who predicted the correct outcome.
        /// </summary>
        Resolved,
        /// <summary>
        /// The broadcaster is canceling the prediction and sending refunds to the participants.
        /// </summary>
        Canceled,
        /// <summary>
        /// The broadcaster is locking the prediction, which means viewers may no longer make predictions.
        /// </summary>
        Locked
    };

    /// <summary>
    /// Locks, resolves, or cancels a Channel Points Prediction.
    /// </summary>
    /// <param name="BroadcasterId">
    /// NOT The login. You are expected to have requested the current prediction
    /// before you call this endpoint. See <see cref="Responses.Prediction.BroadcasterId"/>
    /// </param>
    /// <param name="PredictionId">Prediction id from <see cref="Responses.Prediction.Id"/></param>
    /// <param name="Status">New status for the prediction.</param>
    /// <param name="WinningOutcomeId">
    /// If <paramref name="Status"/> is Resolved, then this 
    /// must be an outcome id from <see cref="Responses.PredictionOutcome.Id"/>
    /// </param>
    public record class EndPrediction(
        string BroadcasterId,
        string PredictionId,
        EndPredictionStatus Status,
        string? WinningOutcomeId
    );
}
