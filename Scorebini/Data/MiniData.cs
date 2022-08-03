using Newtonsoft.Json;

namespace Scorebini.Data
{

    public record class PlayerInputMatchSelectedEventArgs(
        Shared.PlayerInputComponent Sender,
        ITournamentMatch Match
    );
    public record class PlayerInputPlayerSelectedEventArgs(
        Shared.PlayerInputComponent Sender,
        ITournamentParticipant Player
    );


    public enum MatchOrParticipant
    {
        Match = 0,
        Participant
    }

    /// <summary>
    /// Used for displaying in the player select dropdown
    /// </summary>
    public struct MatchOrParticipantId : System.IEquatable<MatchOrParticipantId>
    {
        public MatchOrParticipant Type { get; set; } = MatchOrParticipant.Match;
        public long Id { get; set; }

        public MatchOrParticipantId()
        {
            Type = MatchOrParticipant.Match;
            Id = 0;
        }

        public MatchOrParticipantId(string jsonStr)
        {
            MatchOrParticipantId? ret = JsonConvert.DeserializeObject<MatchOrParticipantId>(jsonStr);
            Type = ret?.Type ?? MatchOrParticipant.Match;
            Id = ret?.Id ?? 0;
        }

        public MatchOrParticipantId(MatchOrParticipant type, long id)
        {
            Type = type;
            Id = id;
        }

        public MatchOrParticipantId(ITournamentMatch match) :
            this(MatchOrParticipant.Match, match.Id)
        {
        }

        public MatchOrParticipantId(ITournamentParticipant participant) :
            this(MatchOrParticipant.Participant, participant.Id)
        {
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public bool Equals(MatchOrParticipantId other)
        {
            return Id == other.Id && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if(obj is MatchOrParticipantId id)
            {
                return Equals(id);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(this.Type, this.Id);
        }

        public static bool operator ==(MatchOrParticipantId left, MatchOrParticipantId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchOrParticipantId left, MatchOrParticipantId right)
        {
            return !(left == right);
        }
    }
}
