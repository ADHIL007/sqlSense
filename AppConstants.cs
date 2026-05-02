using System;
using System.IO;

namespace sqlSense
{
    public static class AppConstants
    {
        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sqlSense");
        
        public static readonly string LocalAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sqlSense");
        
        static AppConstants()
        {
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
            if (!Directory.Exists(LocalAppDataFolder)) Directory.CreateDirectory(LocalAppDataFolder);
        }
    }
}
