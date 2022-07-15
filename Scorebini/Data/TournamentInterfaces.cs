namespace Scorebini.Data
{
    public interface ITournamentParticipant 
    {
        string Name { get; }
        long Id { get; }
    }


    public interface ITournamentMatch
    {
        long Id { get; }
        ITournamentParticipant Player1 { get; }
        ITournamentParticipant Player2 { get; }
        MatchStatus Status { get; }
        string RoundName { get; }
        long RoundNumber { get; }
    }
}
