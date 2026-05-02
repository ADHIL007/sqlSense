using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Newtonsoft.Json;

namespace sqlSense.Views
{
    public partial class ConnectWindow : Window
    {
        public string ConnectionString { get; private set; } = "";
        public bool IsConnected { get; private set; }

        private static readonly string HistoryFilePath = Path.Combine(
            AppConstants.AppDataFolder, "server_history.json");

        public ConnectWindow()
        {
            InitializeComponent();
            if (AuthComboBox != null)
                AuthComboBox.SelectedIndex = 0;

            LoadServerHistory();
        }

        private void LoadServerHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    var history = JsonConvert.DeserializeObject<List<string>>(json);
                    if (history != null && history.Count > 0)
                    {
                        foreach (var server in history)
                        {
                            ServerTextBox.Items.Add(server);
                        }
                        // Pre-select the most recent server
                        ServerTextBox.Text = history[0];
                    }
                }
            }
            catch
            {
                // Silently ignore history loading errors
            }
        }

        private void SaveServerHistory(string serverName)
        {
            try
            {
                var history = new List<string>();

                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    history = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }

                // Remove if already exists, then insert at top
                history.RemoveAll(s => s.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                history.Insert(0, serverName);

                // Keep only the last 10 entries
                if (history.Count > 10)
                    history = history.Take(10).ToList();

                // Ensure directory exists
                string? dir = Path.GetDirectoryName(HistoryFilePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(HistoryFilePath, JsonConvert.SerializeObject(history));
            }
            catch
            {
                // Silently ignore save errors
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void AuthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UserTextBox == null || PasswordTextBox == null || RememberCheckBox == null) return;

            bool isWindowsAuth = AuthComboBox.SelectedIndex == 0;
            
            UserTextBox.IsEnabled = !isWindowsAuth;
            PasswordTextBox.IsEnabled = !isWindowsAuth;
            RememberCheckBox.IsEnabled = !isWindowsAuth;

            if (isWindowsAuth)
            {
                UserTextBox.Text = Environment.MachineName + "\\" + Environment.UserName;
                UserTextBox.Opacity = 0.5;
                PasswordTextBox.Opacity = 0.5;
                UserLabel.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                PasswordLabel.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }
            else
            {
                UserTextBox.Opacity = 1.0;
                PasswordTextBox.Opacity = 1.0;
                UserLabel.Foreground = Brushes.White;
                PasswordLabel.Foreground = Brushes.White;
                UserTextBox.Text = "sa";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string server = ServerTextBox.Text;

            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show("Please enter a server name.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isWindowsAuth = AuthComboBox.SelectedIndex == 0;

            if (isWindowsAuth)
            {
                ConnectionString = $"Server={server};Database=master;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";
            }
            else
            {
                string user = UserTextBox.Text;
                string pass = PasswordTextBox.Password;
                ConnectionString = $"Server={server};Database=master;User Id={user};Password={pass};Encrypt=False;TrustServerCertificate=True;";
            }

            // Save to server history
            SaveServerHistory(server);

            if (RememberCheckBox.IsChecked == true)
            {
                // In a real app, you'd save credentials securely
            }

            IsConnected = true;
            DialogResult = true;
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
