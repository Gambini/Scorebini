using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scorebini.Data
{
    public class SettingsProfile
    {
        public string ProfileName { get; set; }
        public string ChallongeApiKey { get; set; }
        public string SmashggApiKey { get; set; }
        public List<string> EnabledTwitchUsers { get; set; } = new();
    }

    public class ScoreboardSettings
    {
        /// <summary>
        /// Only used for upgrading from version 0, do not use
        /// </summary>
        public string ChallongeApiKey { get; set; }
        public string OutputDirectory { get; set; } = "output";
        public int UpdateIntervalSeconds { get; set; } = 30;
        /// <summary>
        /// Only used for upgrading from version 0, do not use
        /// </summary>
        public string SmashggApiKey { get; set; }
        public int Version { get; set; } = 0;
        public int SelectedProfileIndex { get; set; } = 0;
        public List<string> AllTwitchUsers { get; set; } = new();
        public List<SettingsProfile> Profiles { get; set; } = new();


        public SettingsProfile GetSelectedProfile()
        {
            if (SelectedProfileIndex >= 0 && SelectedProfileIndex < Profiles.Count)
            {
                return Profiles[SelectedProfileIndex];
            }
            else
            {
                return null;
            }
        }
    }
}
