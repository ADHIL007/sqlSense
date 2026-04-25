using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using sqlSense.Services.Configuration;

namespace sqlSense.UI.MenueItems.Settings.Pages
{
    public partial class LoggingSettingsPage : UserControl
    {
        private readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sqlSense", "logs");

        public LoggingSettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            TbLogPath.Text = LogDir;
        }

        private void LoadSettings()
        {
            var appSettings = SettingsManager.Current;
            CbEnableLogging.IsChecked = appSettings.EnableHttpLogging;
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                Process.Start("explorer.exe", LogDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete all log files? This action cannot be undone.", "Clear Logs", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (Directory.Exists(LogDir))
                    {
                        var files = Directory.GetFiles(LogDir, "*.log");
                        foreach (var file in files)
                        {
                            try { File.Delete(file); } catch { /* Ignore files in use */ }
                        }
                        MessageBox.Show("Log files cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
