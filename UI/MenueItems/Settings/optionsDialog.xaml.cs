using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace sqlSense.UI.MenueItems.Settings
{
    /// <summary>
    /// Interaction logic for optionsDialog.xaml
    /// </summary>
    public partial class optionsDialog : Window
    {
        public optionsDialog()
        {
            InitializeComponent();
        }

        public void SelectAIAssistantPage()
        {
            RbAIAssistant.IsChecked = true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Save AI Settings
            var appSettings = sqlSense.Services.SettingsManager.Current;
            appSettings.AiEnableCodeCompletion = PageAI.CbEnableAiCompletion.IsChecked ?? false;
            appSettings.AiEnableNlToSql = PageAI.CbEnableNlToSql.IsChecked ?? false;
            appSettings.AiProvider = ((ComboBoxItem)PageAI.CmbProvider.SelectedItem)?.Content?.ToString() ?? "None";
            appSettings.AiBaseUrl = PageAI.TbBaseUrl.Text;
            appSettings.AiApiKey = PageAI.PbApiKey.Password;
            appSettings.AiModelName = PageAI.CmbModelName.Text;
            appSettings.AiDeploymentName = PageAI.TbDeploymentName.Text;
            appSettings.AiApiVersion = PageAI.TbApiVersion.Text;
            appSettings.AiSendSchema = PageAI.CbSendSchema.IsChecked ?? false;
            appSettings.AiFastMode = PageAI.CbFastMode.IsChecked ?? false;

            sqlSense.Services.SettingsManager.Save();

            this.Close();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentArea == null) return;

            var button = sender as RadioButton;
            var category = button.Content.ToString();

            // Hide all pages
            foreach (UIElement child in ContentArea.Children)
            {
                child.Visibility = Visibility.Collapsed;
            }

            // Show selected page
            switch (category)
            {
                case "General": PageGeneral.Visibility = Visibility.Visible; break;
                case "Editor": PageEditor.Visibility = Visibility.Visible; break;
                case "AI Assistant": PageAI.Visibility = Visibility.Visible; break;
                case "Connection":
                case "DB Connection": PageConnection.Visibility = Visibility.Visible; break;
                case "Theme": PageTheme.Visibility = Visibility.Visible; break;
                case "Canvas": PageCanvas.Visibility = Visibility.Visible; break;
                case "SQL Engine": PageSQLEngine.Visibility = Visibility.Visible; break;
                case "Keyboard": PageKeyboard.Visibility = Visibility.Visible; break;
                case "About": PageAbout.Visibility = Visibility.Visible; break;
            }
        }
    }
}
