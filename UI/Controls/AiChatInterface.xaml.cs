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

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.IsKeyDown(Key.LeftShift) == false && Keyboard.IsKeyDown(Key.RightShift) == false)
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // Add User Message
            AddMessage(text, true);
            InputTextBox.Text = "";

            // Simulate AI responding
            AddMessage("I'm formulating an answer for your query...", false);
        }

        private void AddMessage(string text, bool isUser)
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

            border.Child = textBlock;
            ChatMessagesPanel.Children.Add(border);

            ChatScrollViewer.ScrollToEnd();
        }
    }
}
