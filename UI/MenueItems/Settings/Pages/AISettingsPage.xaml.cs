using System.Windows.Controls;

namespace sqlSense.UI.MenueItems.Settings.Pages
{
    public partial class AISettingsPage : UserControl
    {
        public AISettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var appSettings = sqlSense.Services.SettingsManager.Current;
            CbEnableAiCompletion.IsChecked = appSettings.AiEnableCodeCompletion;
            CbEnableNlToSql.IsChecked = appSettings.AiEnableNlToSql;
            PbApiKey.Password = appSettings.AiApiKey;
            TbModelName.Text = appSettings.AiModelName;
            CbSendSchema.IsChecked = appSettings.AiSendSchema;

            foreach (ComboBoxItem item in CmbProvider.Items)
            {
                if (item.Content.ToString() == appSettings.AiProvider)
                {
                    CmbProvider.SelectedItem = item;
                    break;
                }
            }
        }
    }
}
