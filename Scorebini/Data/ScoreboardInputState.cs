using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public enum GrandFinalsPosition
    {
        None = 0,
        Winners = 1,
        Losers = 2
    }

    public class ScoreboardPlayerState
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public GrandFinalsPosition GFPosition { get; set; } = GrandFinalsPosition.None;

        public string FormatName()
        {
            if(GFPosition == GrandFinalsPosition.None)
            {
                return Name;
            }
            else
            {
                string pos = "";
                switch (GFPosition)
                {
                    case GrandFinalsPosition.Winners:
                        pos = " (W)";
                        break;
                    case GrandFinalsPosition.Losers:
                        pos = " (L)";
                        break;
                    default:
                        break;
                }
                return Name + pos;
            }
        }
    }

    public class ScoreboardInputState
    {
        public string ChallongeUrl { get; set; } = "";
        public ScoreboardPlayerState Player1 { get; set; } = new();
        public ScoreboardPlayerState Player2 { get; set; } = new();
        public string RoundName { get; set; } = "";

        public List<Commentator> Commentators { get; set; } = new();

        public void SwapPlayers()
        {
            ScoreboardPlayerState tmp = Player1;
            Player1 = Player2;
            Player2 = tmp;
        }

        public void ResetScore()
        {
            Player1.Score = 0;
            Player2.Score = 0;
        }
    }
}
