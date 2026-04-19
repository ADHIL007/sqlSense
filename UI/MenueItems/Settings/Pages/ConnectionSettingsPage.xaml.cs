using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace sqlSense.UI.MenueItems.Settings.Pages
{
    public partial class ConnectionSettingsPage : UserControl
    {
        private static readonly string HistoryFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "sqlSense", "server_history.json");

        public ConnectionSettingsPage()
        {
            InitializeComponent();
            this.Loaded += ConnectionSettingsPage_Loaded;
        }

        private void ConnectionSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
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
                            ServerNameComboBox.Items.Add(server);
                        }
                        
                        // Select the first one and populate text
                        ServerNameComboBox.SelectedIndex = 0;
                        ServerNameComboBox.Text = history[0];
                    }
                }
            }
            catch { }
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

                history.RemoveAll(s => s.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                history.Insert(0, serverName);

                if (history.Count > 10)
                    history = history.Take(10).ToList();

                string? dir = Path.GetDirectoryName(HistoryFilePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(HistoryFilePath, JsonConvert.SerializeObject(history));
            }
            catch { }
        }

        private void EditConnection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            EditSettingsPanel.IsEnabled = !EditSettingsPanel.IsEnabled;
            
            if (TestConnectionResult != null)
            {
                TestConnectionResult.Text = "";
            }
        }

        private void ServerName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (CurrentServerText == null || ConnectionTypeBadge == null) return;
            
            string serverName = ServerNameComboBox.Text.Trim();
            CurrentServerText.Text = string.IsNullOrEmpty(serverName) ? "(None)" : serverName;

            bool isLocal = serverName.Equals(".") || serverName.Equals("(local)") || serverName.Equals("localhost") || serverName.ToLower().Contains("sqlexpress") || serverName.StartsWith(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            
            ConnectionTypeBadge.Text = isLocal ? "Local" : "Remote";
            ConnectionTypeBadge.Foreground = isLocal ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7")) 
                                                     : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C586C0"));
        }

        private void AuthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentAuthText == null || AuthComboBox.SelectedItem == null) return;
            
            var selectedItem = (ComboBoxItem)AuthComboBox.SelectedItem;
            CurrentAuthText.Text = selectedItem.Content.ToString();

            bool isWindowsAuth = AuthComboBox.SelectedIndex == 0;
            if (UserGrid != null && PasswordGrid != null)
            {
                UserGrid.Visibility = isWindowsAuth ? Visibility.Collapsed : Visibility.Visible;
                PasswordGrid.Visibility = isWindowsAuth ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private string GetConnectionString()
        {
            string server = ServerNameComboBox.Text.Trim();
            if (string.IsNullOrEmpty(server)) return "";

            bool isWindowsAuth = AuthComboBox.SelectedIndex == 0;
            string encrypt = EncryptCheckBox.IsChecked == true ? "True" : "False";
            string trustCert = TrustServerCheckBox.IsChecked == true ? "True" : "False";

            if (isWindowsAuth)
            {
                return $"Server={server};Database=master;Trusted_Connection=True;Encrypt={encrypt};TrustServerCertificate={trustCert};";
            }
            else
            {
                string user = UserTextBox.Text.Trim();
                string pass = PasswordBox.Password;
                return $"Server={server};Database=master;User Id={user};Password={pass};Encrypt={encrypt};TrustServerCertificate={trustCert};";
            }
        }

        private async void TestConnection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string connStr = GetConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                TestConnectionResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
                TestConnectionResult.Text = "Please enter a server name.";
                return;
            }

            TestConnectionResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            TestConnectionResult.Text = "Testing connection...";

            bool success = false;
            string errorMsg = "";

            try
            {
                // To avoid hanging the UI, we wrap connection test in Task.Run
                await Task.Run(() => 
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        // Enforce a quick timeout for checking
                        conn.ConnectionString += "Connection Timeout=5;";
                        conn.Open();
                        success = true;
                    }
                });
            }
            catch (Exception ex)
            {
                // Clean the SQL connection error a bit
                errorMsg = ex.Message.Split('\n')[0].Trim();
            }

            if (success)
            {
                TestConnectionResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                TestConnectionResult.Text = "✓ Connection successful";
            }
            else
            {
                TestConnectionResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
                TestConnectionResult.Text = $"✗ Failed: {errorMsg}";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string serverName = ServerNameComboBox.Text.Trim();
            if (!string.IsNullOrEmpty(serverName))
            {
                SaveServerHistory(serverName);
                
                // You can also raise an event here to notify the MainWindow to use this connection, 
                // close the options dialog, or simply show a saved message.
                TestConnectionResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7"));
                TestConnectionResult.Text = "Settings applied & saved!";
            }
        }
    }
}
