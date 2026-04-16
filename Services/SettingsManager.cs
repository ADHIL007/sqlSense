using System;
using System.IO;
using Newtonsoft.Json;

namespace sqlSense.Services
{
    public class AppSettings
    {
        public bool AiEnableCodeCompletion { get; set; } = true;
        public bool AiEnableNlToSql { get; set; } = true;
        public string AiProvider { get; set; } = "OpenAI";
        public string AiApiKey { get; set; } = "";
        public string AiModelName { get; set; } = "";
        public bool AiSendSchema { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Current { get; private set; }

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to save settings: " + ex.Message);
            }
        }
    }
}
