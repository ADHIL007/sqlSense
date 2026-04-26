using System;
using System.IO;
using Newtonsoft.Json;

namespace sqlSense.Services.Configuration
{
    public class AppSettings
    {
        public bool AiEnableCodeCompletion { get; set; } = true;
        public bool AiEnableNlToSql { get; set; } = true;
        public string AiProvider { get; set; } = "None";
        public string AiApiKey { get; set; } = "";
        public string AiBaseUrl { get; set; } = "";
        public string AiModelName { get; set; } = "";
        public string AiDeploymentName { get; set; } = "";
        public string AiApiVersion { get; set; } = "";
        public bool AiSendSchema { get; set; } = true;
        public bool AiFastMode { get; set; } = false;
        public bool EnableHttpLogging { get; set; } = true;
        public System.Collections.Generic.Dictionary<string, string> SavedApiKeys { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Current { get; private set; }

        public static event EventHandler SettingsSaved;

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    Current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    Current = new AppSettings();
                }
            }
            else
            {
                Current = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
                SettingsSaved?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to save settings: " + ex.Message);
            }
        }
    }
}
