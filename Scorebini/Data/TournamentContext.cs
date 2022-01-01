using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public class TournamentContext
    {
        public string TournamentId { get; set; }
        public List<ChallongeParticipant> Participants { get; set; } = null;
        public List<ChallongeMatch> Matches { get; set; } = null;
        public long MaxRoundWinners { get; set; } = 0; // will be positive
        public long MaxRoundLosers { get; set; } = 0; // will be the smallest negative number


        public List<string> RequestErrors { get; set; } = new();

        public bool IsValid => Participants != null && Matches != null;
    }


    public class TournamentParticipant 
    {
        public ChallongeParticipant Model { get; private set; }
        public string Name => Model.Name;
        public long Id => Model.Id.Value;

        public TournamentParticipant(ChallongeParticipant model)
        {
            Model = model;
        }
    }

    public enum MatchStatus
    {
        Unknown,
        Pending,
        Open,
        Complete
    }
    public class TournamentMatch
    {
        public ChallongeMatch Model { get; set; }
        public long Id => Model.Id.Value;
        public TournamentParticipant Player1 { get; set; } = null;
        public TournamentParticipant Player2 { get; set; } = null;
        public MatchStatus Status { get; set; } = MatchStatus.Unknown;
        public string RoundName { get; set; } = "INVALID";
        public long RoundNumber => Model.Round ?? 0;
        public string Identifier => Model.Identifier;


        private static MatchStatus StatusFromString(string status)
        {
            switch(status)
            {
                case "pending": return MatchStatus.Pending;
                case "open": return MatchStatus.Open;
                case "complete": return MatchStatus.Complete;
                default: return MatchStatus.Unknown;
            }
        }

        /// <summary>
        /// Expects tournament.Participants to be filled out
        /// </summary>
        public static TournamentMatch Create(TournamentView tournament, ChallongeMatch model)
        {
            TournamentMatch ret = new()
            {
                Model = model,
                Status = StatusFromString(model.State)
            };
            if(ret.Model.Player1Id.HasValue && tournament.Participants.TryGetValue(ret.Model.Player1Id.Value, out var player1))
            {
                ret.Player1 = player1;
            }
            if(ret.Model.Player2Id.HasValue && tournament.Participants.TryGetValue(ret.Model.Player2Id.Value, out var player2))
            {
                ret.Player2 = player2;
            }
            ret.RoundName = GetRoundName(tournament, model.Round ?? 0);
            return ret;
        }

        public static string GetRoundName(TournamentView tournament, long round)
        {
            string sideName = round < 0 ? "Losers" : "Winners";
            string roundName;
            if (round == tournament.Model.MaxRoundWinners)
            {
                return "Grand Finals";
            }
            else if(round == (tournament.Model.MaxRoundWinners - 1) || round == tournament.Model.MaxRoundLosers)
            {
                roundName = "Finals";
            }
            else if(round == (tournament.Model.MaxRoundWinners - 2) || round == (tournament.Model.MaxRoundLosers + 1))
            {
                roundName = "SemiFinals";
            }
            else
            {
                long absRound = Math.Abs(round);
                roundName = "Round " + absRound;
            }
            return $"{sideName} {roundName}";
        }
    }

    public class TournamentView
    {
        public TournamentContext Model { get; set; }
        public string Id => Model.TournamentId;
        public Dictionary<long, TournamentParticipant> Participants { get; } = new();
        public Dictionary<long, TournamentMatch> Matches { get; } = new();
        public List<TournamentParticipant> AlphabeticalParticipants { get; } = new();

        public TournamentView(TournamentContext model)
        {
            Model = model;
            if (Model.Participants == null)
                return;
            foreach (var partModel in Model.Participants)
            {
                if (partModel.Id.HasValue)
                {
                    Participants.Add(partModel.Id.Value, new TournamentParticipant(partModel));
                }
            }

            AlphabeticalParticipants = Participants.Values.OrderBy(p => p.Name).ToList();

            if (Model.Matches == null)
                return;
            foreach (var matchModel in Model.Matches)
            {
                if (matchModel.Id.HasValue)
                {
                    Matches.Add(matchModel.Id.Value, TournamentMatch.Create(this, matchModel));
                }
            }
        }
    }
}
