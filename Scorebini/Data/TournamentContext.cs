using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public enum TournamentHost
    {
        Unknown = 0,
        Challonge,
        Smash
    }

    public enum TournamentElimType
    {
        SingleElim = 0,
        DoubleElim,
        RoundRobin
    }


    public class MatchScoreReport
    {
        public class Score
        {
            public ITournamentParticipant Player { get; set; }
            public int Wins { get; set; }
        }
        public TournamentView Tournament { get; set; }
        public ITournamentMatch Match { get; set; }
        public List<Score> Scores { get; } = new();
    }

    public class TournamentContext
    {

        public class ChallongeData
        {
            public ChallongeTournament Tournament;
            public List<ChallongeParticipant> Participants { get; set; } = null;
            public List<ChallongeMatch> Matches { get; set; } = null;

            public bool IsValid => Participants != null && Matches != null;
        }

        public class SmashggData
        {
            public Smash.gg.Event Tournament;
            public List<Smash.gg.Entrant> Participants = null;
            public List<Smash.gg.Set> Matches = null;
            public bool IsValid => Participants != null && Matches != null;
        }

        public string Url { get; set; }
        public TournamentHost TournamentHost { get; set; } = TournamentHost.Unknown;
        public string TournamentId { get; set; }
        public ChallongeData Challonge { get; set; } = null;
        public SmashggData Smashgg { get; set; } = null;

        public long MaxRoundWinners { get; set; } = 0; // will be positive.
        public long MaxRoundLosers { get; set; } = 0; // will be the smallest negative number
        public TournamentElimType ElimType { get; set; } = TournamentElimType.DoubleElim;


        public List<string> RequestErrors { get; set; } = new();

        public bool IsValid => TournamentHost != TournamentHost.Unknown && (Challonge?.IsValid == true || Smashgg?.IsValid == true);

        public TournamentContext()
        {
        }

        public TournamentContext(ChallongeTournament tournament)
        {
            TournamentHost = TournamentHost.Challonge;
            Challonge = new ChallongeData()
            {
                Tournament = tournament
            };
            Url = Challonge.Tournament?.FullUrl;
            TournamentId = Challonge.Tournament?.UrlId;
            Challonge.Participants = Challonge.Tournament?.Participants?.Select(p => p.Participant).Where(p => p != null).ToList();
            Challonge.Matches = Challonge.Tournament?.Matches?.Select(m => m.Match).Where(m => m != null).ToList();
            if(Challonge.Matches != null && Challonge.Matches.Count > 0)
            {
                MaxRoundWinners = Challonge.Matches.Select(m => m.Round ?? 0).Max();
                MaxRoundLosers = Challonge.Matches.Select(m => m.Round ?? 0).Min();
            }
            ElimType = (Challonge.Tournament?.TournamentType) switch
            {
                "single elimination" => TournamentElimType.SingleElim,
                "double elimination" => TournamentElimType.DoubleElim,
                "round robin" => TournamentElimType.RoundRobin,
                _ => TournamentElimType.DoubleElim,// sure, why not default to double elim
            };
        }


        public TournamentContext(Smash.gg.Event tournament, string url, string tournamentId)
        {
            TournamentHost = TournamentHost.Smash;
            Smashgg = new SmashggData()
            {
                Tournament = tournament
            };
            Url = url;
            TournamentId = tournamentId;
            Smashgg.Matches = Smashgg.Tournament?.SetConnection?.Nodes?.Where(n => n != null).ToList();
            // this is an ugly linq query, but should only be run for < 100 item generally so performance is less of an issue
            Smashgg.Participants = Smashgg.Matches?.SelectMany(m => m.Slots)
                .Select(slot => slot?.Entrant).Where(e => e != null)
                .DistinctBy(e => e.Id).ToList();
            if (Smashgg.Matches != null && Smashgg.Matches.Count > 0)
            {
                MaxRoundWinners = Smashgg.Matches.Select(m => m.Round).Max();
                MaxRoundLosers = Smashgg.Matches.Select(m => m.Round).Min();
            }
            // TODO: Find this information somehow
            ElimType = TournamentElimType.DoubleElim;
        }
    }


    public class ChallongeTournamentParticipant : ITournamentParticipant
    {
        public ChallongeParticipant Model { get; private set; }
        public string Name => Model.Name;
        public long Id => Model.Id.Value;

        public ChallongeTournamentParticipant(ChallongeParticipant model)
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
    public class TournamentMatch : ITournamentMatch
    {
        public ChallongeMatch Model { get; set; }
        public long Id => Model.Id.Value;
        public ChallongeTournamentParticipant Player1 { get; set; } = null;
        public ChallongeTournamentParticipant Player2 { get; set; } = null;
        public MatchStatus Status { get; set; } = MatchStatus.Unknown;
        public string RoundName { get; set; } = "INVALID";
        public long RoundNumber => Model.Round ?? 0;
        public string Identifier => Model.Identifier;

        // Explicit interface implementation to calm compile errors
        ITournamentParticipant ITournamentMatch.Player1 => Player1;
        ITournamentParticipant ITournamentMatch.Player2 => Player2;

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
                ret.Player1 = player1 as ChallongeTournamentParticipant;
            }
            if(ret.Model.Player2Id.HasValue && tournament.Participants.TryGetValue(ret.Model.Player2Id.Value, out var player2))
            {
                ret.Player2 = player2 as ChallongeTournamentParticipant;
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
        public Dictionary<long, ITournamentParticipant> Participants { get; } = new();
        public Dictionary<long, ITournamentMatch> Matches { get; } = new();
        public List<ITournamentParticipant> AlphabeticalParticipants { get; } = new();

        public TournamentView(TournamentContext model)
        {
            Model = model;

            if (Model.TournamentHost == TournamentHost.Challonge && Model.Challonge != null)
            {
                if (Model.Challonge.Participants == null)
                    return;
                foreach (var partModel in Model.Challonge.Participants)
                {
                    if (partModel.Id.HasValue)
                    {
                        Participants.Add(partModel.Id.Value, new ChallongeTournamentParticipant(partModel));
                    }
                }


                if (Model.Challonge.Matches == null)
                    return;
                foreach (var matchModel in Model.Challonge.Matches)
                {
                    if (matchModel.Id.HasValue)
                    {
                        Matches.Add(matchModel.Id.Value, TournamentMatch.Create(this, matchModel));
                    }
                }
            }
            else if (Model.TournamentHost == TournamentHost.Smash && Model.Smashgg != null)
            {
                if (Model.Smashgg.Participants == null)
                    return;
                foreach (var partModel in Model.Smashgg.Participants)
                {
                    Participants.Add(partModel.Id, new Smash.gg.TournamentParticipant(partModel));
                }


                if (Model.Smashgg.Matches == null)
                    return;
                foreach (var matchModel in Model.Smashgg.Matches)
                {
                    Matches.Add(matchModel.Id, Smash.gg.TournamentMatch.Create(this, matchModel));
                }
            }


            AlphabeticalParticipants = Participants.Values.OrderBy(p => p.Name).ToList();
        }


        public bool IsValidMatchId(long id)
        {
            return Matches?.ContainsKey(id) == true;
        }
    }
}
