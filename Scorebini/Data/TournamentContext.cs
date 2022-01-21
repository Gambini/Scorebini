using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public enum TournamentElimType
    {
        SingleElim = 0,
        DoubleElim,
        RoundRobin
    }

    public class TournamentContext
    {
        public string Url { get; set; }
        public string TournamentId { get; set; }
        public ChallongeTournament Tournament { get; set; }
        public List<ChallongeParticipant> Participants { get; set; } = null;
        public List<ChallongeMatch> Matches { get; set; } = null;
        public long MaxRoundWinners { get; set; } = 0; // will be positive
        public long MaxRoundLosers { get; set; } = 0; // will be the smallest negative number
        public TournamentElimType ElimType { get; set; } = TournamentElimType.DoubleElim;


        public List<string> RequestErrors { get; set; } = new();

        public bool IsValid => Participants != null && Matches != null;

        public TournamentContext()
        {
        }

        public TournamentContext(ChallongeTournament tournament)
        {
            Tournament = tournament;
            Url = Tournament?.FullUrl;
            TournamentId = Tournament?.UrlId;
            Participants = Tournament?.Participants?.Select(p => p.Participant).Where(p => p != null).ToList();
            Matches = Tournament?.Matches?.Select(m => m.Match).Where(m => m != null).ToList();
            if(Matches != null)
            {
                MaxRoundWinners = Matches.Select(m => m.Round ?? 0).Max();
                MaxRoundLosers = Matches.Select(m => m.Round ?? 0).Min();
            }
            ElimType = (Tournament?.TournamentType) switch
            {
                "single elimination" => TournamentElimType.SingleElim,
                "double elimination" => TournamentElimType.DoubleElim,
                "round robin" => TournamentElimType.RoundRobin,
                _ => TournamentElimType.DoubleElim,// sure, why not default to double elim
            };
        }
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
            if (tournament.Model.ElimType == TournamentElimType.DoubleElim)
            {
                string sideName = round < 0 ? "Losers" : "Winners";
                string roundName;
                if (round == tournament.Model.MaxRoundWinners)
                {
                    return "Grand Finals";
                }
                else if (round == (tournament.Model.MaxRoundWinners - 1) || round == tournament.Model.MaxRoundLosers)
                {
                    roundName = "Finals";
                }
                else if (round == (tournament.Model.MaxRoundWinners - 2) || round == (tournament.Model.MaxRoundLosers + 1))
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
            else if(tournament.Model.ElimType == TournamentElimType.SingleElim)
            {
                string roundName;
                if (round == tournament.Model.MaxRoundWinners)
                {
                    return "Grand Finals";
                }
                else if (round == (tournament.Model.MaxRoundWinners - 1))
                {
                    roundName = "Finals";
                }
                else if (round == (tournament.Model.MaxRoundWinners - 2))
                {
                    roundName = "SemiFinals";
                }
                else
                {
                    roundName = "Round " + round;
                }
                return roundName;
            }
            else // round robin or unknown
            {
                return $"Round {round}";
            }
        }
    }

    public class TournamentView
    {
        public string Url => Model.Url;
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
