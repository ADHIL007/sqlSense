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

            // Add User Message
            AddMessage(text, true);
            InputTextBox.Text = "";

            // Show a temporary thinking message
            var loadingBorder = AddMessage("🤔 Thinking...", false, true);
            
            try
            {
                // Call actual AI Service
                string aiResponse = await sqlSense.Services.AiService.SendMessageAsync(text);
                
                // Replace loading text with actual AI response
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
