using System;
using System.IO;
using System.Diagnostics;

namespace sqlSense.Services
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Sql
    }

    public static class LoggerService
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sqlsense.log");
        private static readonly object LogLock = new object();

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {message}";
            
            // Output to Debug Console
            Debug.WriteLine(logEntry);
            Console.WriteLine(logEntry);

            // Append to Log File
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch { /* Ignore log file errors to prevent crashing */ }
        }

        public static void LogSql(string sql)
        {
            Log($"Executing SQL:\n{sql}\n", LogLevel.Sql);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log(errorMessage, LogLevel.Error);
        }
    }
}
