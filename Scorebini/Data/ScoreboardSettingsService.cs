using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Scorebini.Data
{
    public class ScoreboardSettingsService
    {
        public ScoreboardSettings CurrentSettings { get; set; } = new ScoreboardSettings();
        const string SettingsFilePath = @"Config/ScoreboardSettings.json";
        const string InputStateFilePath = @"Config/ScoreboardInputState.json";
        const string CommentatorFilePath = @"ScoreboardCommentators.json"; // relative to output directory
        private readonly string FullSettingsFilePath;
        private readonly string FullInputStateFilePath;

        private readonly ILogger Log;

        public ScoreboardSettingsService(ILogger<ScoreboardSettingsService> logger)
        {
            Log = logger;
            FullSettingsFilePath = Path.GetFullPath(SettingsFilePath);
            FullInputStateFilePath = Path.GetFullPath(InputStateFilePath);
        }

        
        // returns true if settings were loaded successfully
        public bool LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    Log?.LogInformation($"No file at {FullSettingsFilePath}, default initializing settings.");
                    CurrentSettings = new();
                    CurrentSettings.Profiles.Add(new SettingsProfile()
                    {
                        ProfileName = "Default"
                    });
                    return true;
                }
                else
                {
                    Log?.LogInformation($"Reading settings from {FullSettingsFilePath}");
                    string fileContents = File.ReadAllText(SettingsFilePath, System.Text.Encoding.UTF8);
                    ScoreboardSettings newSettings = JsonConvert.DeserializeObject<ScoreboardSettings>(fileContents);
                    if (newSettings == null)
                    {
                        Log?.LogError($"Could not deserialize json to settings.");
                        return false;
                    }
                    else
                    {
                        if (newSettings.Version == 0)
                        {
                            newSettings = UpgradeVersion0To1(newSettings);
                        }
                        CurrentSettings = newSettings;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error loading settings: {ex.Message}");
                return false;
            }
        }

        private ScoreboardSettings UpgradeVersion0To1(ScoreboardSettings v0)
        {
            Log?.LogInformation("Upgrading settings from version 0 to version 1.");
            ScoreboardSettings ret = new();
            ret.Version = 1;
            ret.OutputDirectory = v0.OutputDirectory;
            ret.UpdateIntervalSeconds = v0.UpdateIntervalSeconds;
            SettingsProfile defaultProfile = new();
            defaultProfile.ProfileName = "Default";
#pragma warning disable CS0618 // Type or member is obsolete
            defaultProfile.ChallongeApiKey = v0.ChallongeApiKey;
            defaultProfile.SmashggApiKey = v0.SmashggApiKey;
#pragma warning restore CS0618 // Type or member is obsolete
            ret.Profiles.Add(defaultProfile);
            return ret;
        }

        public bool SaveSettings(ScoreboardSettings newSettings)
        {
            try
            {
                Log?.LogInformation($"Saving settings to {FullSettingsFilePath}");
                (new FileInfo(FullSettingsFilePath)).Directory?.Create();
                string serialized = JsonConvert.SerializeObject(newSettings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, serialized, System.Text.Encoding.UTF8);
                Log?.LogInformation($"Saved");
                CurrentSettings = newSettings;
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error saving settings: ${ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Will never return null
        /// </summary>
        public ScoreboardInputState LoadInputState()
        {
            try
            {
                if (!File.Exists(InputStateFilePath))
                {
                    Log?.LogInformation($"No file at {FullInputStateFilePath}, default initializing settings.");
                    return new ScoreboardInputState();
                }
                else
                {
                    Log?.LogInformation($"Reading input state from {FullInputStateFilePath}");
                    string fileContents = File.ReadAllText(InputStateFilePath, System.Text.Encoding.UTF8);
                    ScoreboardInputState ret = JsonConvert.DeserializeObject<ScoreboardInputState>(fileContents);
                    if (ret == null)
                    {
                        Log?.LogError($"Could not deserialize json to input state.");
                        return new ScoreboardInputState();
                    }
                    else
                    {
                        return ret;
                    }
                }

            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error loading input state: ${ex.Message}");
                return new ScoreboardInputState();
            }
        }

        public void SaveInputState(ScoreboardInputState input)
        {
            try
            {
                Log?.LogInformation($"Saving input state to {FullInputStateFilePath}");
                (new FileInfo(FullSettingsFilePath)).Directory?.Create();
                string serialized = JsonConvert.SerializeObject(input, Formatting.None);
                File.WriteAllText(InputStateFilePath, serialized, System.Text.Encoding.UTF8);
                Log?.LogInformation($"Saved");
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error saving input state: ${ex.Message}");
                return;
            }
        }

        public CommentatorList LoadCommentators()
        {
            try
            {
                if(CurrentSettings == null)
                {
                    LoadSettings();
                }
                string fullPath = Path.Combine(CurrentSettings.OutputDirectory, CommentatorFilePath);
                if (!File.Exists(fullPath))
                {
                    Log?.LogInformation($"No file at {fullPath}, default initializing settings.");
                    return new CommentatorList();
                }
                else
                {
                    Log?.LogInformation($"Reading commentators from {fullPath}");
                    string fileContents = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    CommentatorList ret = JsonConvert.DeserializeObject<CommentatorList>(fileContents);
                    if (ret == null)
                    {
                        Log?.LogError($"Could not deserialize json to commentator list.");
                        return new CommentatorList();
                    }
                    else
                    {
                        return ret;
                    }
                }

            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error loading commentator list: ${ex.Message}");
                return new CommentatorList();
            }
        }


        public void SaveCommentatorList(CommentatorList input)
        {
            try
            {
                if(CurrentSettings == null)
                {
                    LoadSettings();
                }
                string fullPath = Path.Combine(CurrentSettings.OutputDirectory, CommentatorFilePath);
                Log?.LogInformation($"Saving commentator list to {fullPath}");
                string serialized = JsonConvert.SerializeObject(input, Formatting.None);
                File.WriteAllText(fullPath, serialized, System.Text.Encoding.UTF8);
                Log?.LogInformation($"Saved");
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, $"Error saving commentator list: ${ex.Message}");
                return;
            }
        }


        public void WriteOutputFiles(ScoreboardSettings settings, ScoreboardInputState input)
        {
            try
            {
                string outputDir = Path.GetFullPath(settings.OutputDirectory);
                Directory.CreateDirectory(outputDir);
                WriteToFile(outputDir, "Player1.txt", input.Player1?.FormatName() ?? "");
                WriteToFile(outputDir, "Player1Score.txt", input.Player1?.Score.ToString() ?? "");
                WriteToFile(outputDir, "Player2.txt", input.Player2?.FormatName() ?? "");
                WriteToFile(outputDir, "Player2Score.txt", input.Player2?.Score.ToString() ?? "");
                WriteToFile(outputDir, "RoundName.txt", input.RoundName);
                for(int i = 0; i < input.Commentators.Count; i++)
                {
                    var comm = input.Commentators[i];
                    WriteToFile(outputDir, $"Commentator{i+1}_Name.txt", comm.Name ?? "");
                    WriteToFile(outputDir, $"Commentator{i+1}_Handle.txt", comm.Handle ?? "");
                }
            }
            catch(Exception ex)
            {
                Log.LogError(ex, $"Error saving output files: {ex.Message}");
            }
        }


        private void WriteToFile(string outputDir, string fileName, string contents)
        {
            string fullPath = Path.Combine(outputDir, fileName);
            Log.LogInformation($"Writing '{contents}' to '{fullPath}'");
            File.WriteAllText(fullPath, contents, System.Text.Encoding.UTF8);
        }
    }
}
