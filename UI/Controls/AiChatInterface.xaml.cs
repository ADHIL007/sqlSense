using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace sqlSense.UI.Controls
{
    public partial class AiChatInterface : UserControl
    {
        public event EventHandler CloseRequested;

        public AiChatInterface()
        {
            InitializeComponent();
            this.Loaded += AiChatInterface_Loaded;
        }

        private void AiChatInterface_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateToolbarUI();
        }

        private void UpdateToolbarUI()
        {
            var settings = sqlSense.Services.SettingsManager.Current;
            FastModeText.Text = settings.AiFastMode ? "Fast" : "Reason";
            FastModeText.Foreground = settings.AiFastMode ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 195, 247)); // Highlight when reasoning
            
            CurrentModelLabel.Text = string.IsNullOrEmpty(settings.AiModelName) ? settings.AiProvider : settings.AiModelName;
        }

        private void FastModeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = sqlSense.Services.SettingsManager.Current;
            settings.AiFastMode = !settings.AiFastMode;
            sqlSense.Services.SettingsManager.Save();
            UpdateToolbarUI();
        }

        private async void ModelSelectorBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var contextMenu = new ContextMenu();
                var loadingItem = new MenuItem { Header = "Fetching models...", IsEnabled = false };
                contextMenu.Items.Add(loadingItem);
                
                contextMenu.PlacementTarget = btn;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                contextMenu.IsOpen = true;

                var settings = sqlSense.Services.SettingsManager.Current;
                var models = await sqlSense.Services.AiService.FetchAvailableModelsAsync(settings.AiProvider, settings.AiApiKey, settings.AiBaseUrl);
                
                contextMenu.Items.Clear();
                if (models != null && models.Count > 0)
                {
                    foreach (var m in models)
                    {
                        var item = new MenuItem { Header = m };
                        if (m == settings.AiModelName) item.IsChecked = true;
                        
                        item.Click += (s, args) => 
                        {
                            settings.AiModelName = m;
                            sqlSense.Services.SettingsManager.Save();
                            UpdateToolbarUI();
                        };
                        contextMenu.Items.Add(item);
                    }
                }
                else
                {
                    contextMenu.Items.Add(new MenuItem { Header = "No models available / Check Settings", IsEnabled = false });
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ChatUI.Visibility = Visibility.Collapsed;
            FloatingAIButton.Visibility = Visibility.Visible;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FloatingAIButton_Click(object sender, RoutedEventArgs e)
        {
            FloatingAIButton.Visibility = Visibility.Collapsed;
            ChatUI.Visibility = Visibility.Visible;
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    int caretIndex = InputTextBox.CaretIndex;
                    InputTextBox.Text = InputTextBox.Text.Insert(caretIndex, Environment.NewLine);
                    InputTextBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                    e.Handled = true;
                }
                else if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
                {
                    // Naked Enter -> Send message
                    e.Handled = true;
                    SendMessage();
                }
                // Shift+Enter falls through to native behaviour (new line)
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private async void SendMessage()
        {
            string text = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            AddMessage(text, true);
            InputTextBox.Text = "";

            var loadingBorder = AddMessage("Thinking...", false, true);
            
            try
            {
                string aiResponse = await sqlSense.Services.AiService.SendMessageAsync(text);
                
                if (loadingBorder.Child is TextBlock txt)
                {
                    txt.Text = aiResponse;
                }
            }
            catch (Exception ex)
            {
                if (loadingBorder.Child is TextBlock txt)
                {
                    txt.Text = "Error: " + ex.Message;
                }
            }
        }

        private Border AddMessage(string text, bool isUser, bool isTemporary = false)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(isUser ? 30 : 0, 0, isUser ? 0 : 30, 10),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            if (Application.Current.Resources.Contains("NodeShadow"))
            {
                border.Effect = (System.Windows.Media.Effects.DropShadowEffect)Application.Current.Resources["NodeShadow"];
            }

            if (isUser)
            {
                border.Background = (Brush)Application.Current.Resources["PanelBrush"];
                border.BorderBrush = (Brush)Application.Current.Resources["PrimaryAccentBrush"];
                border.CornerRadius = new CornerRadius(8, 8, 0, 8);
            }
            else
            {
                border.Background = (Brush)Application.Current.Resources["PanelLightBrush"];
                border.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
                border.CornerRadius = new CornerRadius(8, 8, 8, 0);
            }

            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources[isUser ? "TextPrimaryBrush" : "TextSecondaryBrush"],
                FontSize = 12
            };

            if (isTemporary)
            {
                textBlock.FontStyle = FontStyles.Italic;
                textBlock.Opacity = 0.8;
            }

            border.Child = textBlock;
            ChatMessagesPanel.Children.Add(border);

            ChatScrollViewer.ScrollToEnd();
            return border;
        }
    }
}
