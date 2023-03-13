using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


namespace ScorebiniTwitchApi.Shared.Responses
{
    /// <summary>
    /// Represents an outcome
    /// </summary>
    /// <param name="Id"> An ID that identifies this outcome.</param>
    /// <param name="Title"> The outcome’s text.</param>
    /// <param name="Users"> The number of unique viewers that chose this outcome.</param>
    /// <param name="ChannelPoints"> The number of Channel Points spent by viewers on this outcome.</param>
    /// <param name="Color">
    /// The color that visually identifies this outcome in the UX. Possible values are:
    /// <list type="bullet">
    ///     <item>BLUE</item>
    ///     <item>PINK</item>
    /// </list>
    /// If the number of outcomes is two, the color is BLUE for the first outcome and PINK for the second outcome.
    /// If there are more than two outcomes, the color is BLUE for all outcomes.
    /// </param>
    public record class PredictionOutcome(
        string Id,
        string Title,
        int Users,
        int ChannelPoints,
        string Color
    );

    public enum PredictionStatus
    {
        /// <summary>
        /// The Prediction is running and viewers can make predictions.
        /// </summary>
        Active,
        /// <summary>
        /// The broadcaster canceled the Prediction and refunded the Channel Points to the participants.
        /// </summary>
        Canceled,
        /// <summary>
        /// The broadcaster locked the Prediction, which means viewers can no longer make predictions.
        /// </summary>
        Locked,
        /// <summary>
        /// The winning outcome was determined and the Channel Points were distributed to the viewers who predicted the correct outcome.
        /// </summary>
        Resolved
    }


    /// <summary>
    /// Represents a prediction.
    /// </summary>
    /// <param name="Id"> An ID that identifies this prediction.</param>
    /// <param name="BroadcasterId"> An ID that identifies the broadcaster that created the prediction.</param>
    /// <param name="BroadcasterName"> The broadcaster’s display name.</param>
    /// <param name="BroadcasterLogin"> The broadcaster’s login name.</param>
    /// <param name="Title"> The question that the prediction asks.</param>
    /// <param name="WinningOutcomeId"> The ID of the winning outcome. Is null unless status is RESOLVED.</param>
    /// <param name="Outcomes"> The list of possible outcomes for the prediction.</param>
    /// <param name="PredictionWindow"> The length of time (in seconds) that the prediction will run for.</param>
    /// <param name="Status">The prediction’s status. </param>
    /// <param name="CreatedAt"> The RFC-3339 UTC date and time of when the Prediction began.</param>
    /// <param name="EndedAt"> The RFC-3339 UTC date and time of when the Prediction ended. If status is ACTIVE, this is set to null.</param>
    /// <param name="LockedAt"> The RFC-3339 UTC date and time of when the Prediction was locked. If status is not LOCKED, this is set to null.</param>
    public record class Prediction(
        string Id,
        string BroadcasterId,
        string BroadcasterName,
        string BroadcasterLogin,
        string Title,
        string? WinningOutcomeId,
        List<PredictionOutcome> Outcomes,
        int PredictionWindow,
        PredictionStatus Status,
        string CreatedAt,
        string? EndedAt,
        string? LockedAt
    );

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Meta"></param>
    /// <param name="Prediction">May be null if <see cref="ResponseCommonMetadata.Code"/> was not 200.</param>
    public record class CreatePredictionResponse(
        ResponseCommonMetadata Meta,
        Prediction? Prediction
    );

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Meta"></param>
    /// <param name="Prediction">May be null if no predictions have ever been made, or if <see cref="ResponseCommonMetadata.Code"/> was not 200.</param>
    public record class GetCurrentPredictionResponse(
        ResponseCommonMetadata Meta,
        Prediction? Prediction
    );

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Meta"></param>
    /// <param name="Prediction">May be null if <see cref="ResponseCommonMetadata.Code"/> was not 200.</param>
    public record class EndPredictionResponse(
        ResponseCommonMetadata Meta,
        Prediction? Prediction
    );
}
