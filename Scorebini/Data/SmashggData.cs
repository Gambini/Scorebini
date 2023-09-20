using System;
using System.Collections.Generic;
using System.Linq;

namespace Scorebini.Data.Smash.gg
{
    public class TournamentParticipant : ITournamentParticipant
    {
        public Entrant Model { get; private set; }

        public string Name => Model.Name;
        public StringOrIntId Id => Model.Id;

        public TournamentParticipant(Entrant model)
        {
            Model = model;
        }
    }


    public class TournamentMatch : ITournamentMatch
    {
        public Set Model { get; private set; }
        public StringOrIntId Id => Model.Id;

        public TournamentParticipant Player1 { get; set; } = null;
        public TournamentParticipant Player2 { get; set; } = null;

        public MatchStatus Status => ((SetState)Model.State).ToMatchStatus();
        public string RoundName => Model.FullRoundText ?? "INVALID";
        public long RoundNumber => Model.Round;

        ITournamentParticipant ITournamentMatch.Player1 => Player1;

        ITournamentParticipant ITournamentMatch.Player2 => Player2;

        /// <summary>
        /// Expects tournament.Participants to be filled out
        /// </summary>
        public static TournamentMatch Create(TournamentView tournament, Set model)
        {
            TournamentMatch ret = new TournamentMatch()
            {
                Model = model
            };

            static TournamentParticipant GetValidParticipant(TournamentView tournament, Set model, int slot)
            {
                if (model?.Slots == null)
                    return null;
                if (model.Slots.Count > slot)
                {
                    var entrant = model.Slots[slot]?.Entrant;
                    if (entrant != null && tournament.Participants.TryGetValue(entrant.Id, out var player))
                    {
                        return player as TournamentParticipant;
                    }
                }
                return null;
            }

            ret.Player1 = GetValidParticipant(tournament, ret.Model, 0);
            ret.Player2 = GetValidParticipant(tournament, ret.Model, 1);

            return ret;
        }

    }
}
