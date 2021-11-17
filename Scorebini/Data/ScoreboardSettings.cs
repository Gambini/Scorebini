using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public class ScoreboardSettings
    {
        public string ChallongeApiKey { get; set; }
        public string OutputDirectory { get; set; } = "output";
        public int UpdateIntervalSeconds { get; set; } = 30;
    }
}
