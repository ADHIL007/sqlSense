using System.Windows;
using System.Windows.Controls;

namespace sqlSense.UI.MenueItems.Settings.Pages
{
    public partial class AISettingsPage : UserControl
    {
        public AISettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateDynamicUI();
            UpdateLoadModelsButtonState();
        }

        private void LoadSettings()
        {
            var appSettings = sqlSense.Services.SettingsManager.Current;
            CbEnableAiCompletion.IsChecked = appSettings.AiEnableCodeCompletion;
            CbEnableNlToSql.IsChecked = appSettings.AiEnableNlToSql;
            TbBaseUrl.Text = appSettings.AiBaseUrl;
            PbApiKey.Password = appSettings.AiApiKey;
            CmbModelName.Text = appSettings.AiModelName;
            TbDeploymentName.Text = appSettings.AiDeploymentName;
            TbApiVersion.Text = appSettings.AiApiVersion;
            CbSendSchema.IsChecked = appSettings.AiSendSchema;
            CbFastMode.IsChecked = appSettings.AiFastMode;

            foreach (ComboBoxItem item in CmbProvider.Items)
            {
                if (item.Content.ToString() == appSettings.AiProvider)
                {
                    CmbProvider.SelectedItem = item;
                    break;
                }
            }
        }

        private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProvider.SelectedItem is ComboBoxItem item && item.Content?.ToString() == "None")
            {
                if (PbApiKey != null) PbApiKey.Password = "";
                if (CmbModelName != null) 
                {
                    CmbModelName.Text = "";
                    CmbModelName.Items.Clear();
                }
            }
            
            UpdateDynamicUI();
            UpdateLoadModelsButtonState();
        }

        private void PbApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateLoadModelsButtonState();
        }

        private void UpdateDynamicUI()
        {
            if (CmbProvider.SelectedItem == null || AzureExtraOptionsGrid == null) return;
            
            var provider = ((ComboBoxItem)CmbProvider.SelectedItem).Content.ToString();
            
            if (provider == "Microsoft Azure OpenAI")
            {
                AzureExtraOptionsGrid.Visibility = Visibility.Visible;
            }
            else
            {
                AzureExtraOptionsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateLoadModelsButtonState()
        {
            if (CmbProvider.SelectedItem == null || BtnLoadModels == null || PbApiKey == null) return;
            
            var provider = ((ComboBoxItem)CmbProvider.SelectedItem).Content.ToString();
            if (provider == "None")
            {
                BtnLoadModels.IsEnabled = false;
            }
            else if (provider == "Local Model (Ollama)")
            {
                BtnLoadModels.IsEnabled = true;
            }
            else
            {
                BtnLoadModels.IsEnabled = PbApiKey.Password.Length > 0;
            }
        }

        private async void BtnLoadModels_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            BtnLoadModels.Content = "...";
            BtnLoadModels.IsEnabled = false;

            try
            {
                var provider = ((ComboBoxItem)CmbProvider.SelectedItem)?.Content?.ToString();
                var apiKey = PbApiKey.Password;
                var baseUrl = TbBaseUrl.Text;
                
                var models = await sqlSense.Services.AiService.FetchAvailableModelsAsync(provider, apiKey, baseUrl);
                
                CmbModelName.Items.Clear();
                if (models != null && models.Count > 0)
                {
                    foreach (var m in models) CmbModelName.Items.Add(m);
                    CmbModelName.SelectedIndex = 0;
                }
                else
                {
                    System.Windows.MessageBox.Show("No models found or endpoint doesn't support fetching.", "AI Models", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to fetch models: {ex.Message}", "AI Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                BtnLoadModels.Content = "Load Models";
                UpdateLoadModelsButtonState();
            }
        }
    }
}
