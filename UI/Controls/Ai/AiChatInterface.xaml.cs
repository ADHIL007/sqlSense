using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Linq;
using System.Threading.Tasks;
using sqlSense.Services;
using sqlSense.Services.Ai;
using sqlSense.Services.Configuration;
using sqlSense.Services.Ai;
using sqlSense.Controllers;

namespace sqlSense.UI.Controls.Ai
{
    public partial class AiChatInterface : UserControl
    {
        private readonly AiChatController _controller = new();

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
            var settings = SettingsManager.Current;
            FastModeText.Text = settings.AiFastMode ? "Fast" : "Reason";
            FastModeText.Foreground = settings.AiFastMode ? new SolidColorBrush(Color.FromRgb(136, 136, 136)) : new SolidColorBrush(Color.FromRgb(79, 195, 247));
            
            bool isConnected = !string.IsNullOrEmpty(settings.AiProvider) && !settings.AiProvider.Equals("None", StringComparison.OrdinalIgnoreCase);

            if (!isConnected)
            {
                DisconnectedOverlay.Visibility = Visibility.Visible;
                CurrentModelLabel.Text = "";
            }
            else
            {
                DisconnectedOverlay.Visibility = Visibility.Collapsed;
                CurrentModelLabel.Text = string.IsNullOrEmpty(settings.AiModelName) ? settings.AiProvider : settings.AiModelName;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new sqlSense.UI.MenueItems.Settings.optionsDialog();
            dlg.SelectAIAssistantPage();
            dlg.ShowDialog();
            UpdateToolbarUI();
        }

        private void FastModeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;
            ModeReasonBtn.Tag = settings.AiFastMode ? "" : "Selected";
            ModeFastBtn.Tag = settings.AiFastMode ? "Selected" : "";
            ModePopup.IsOpen = true;
        }

        private void ModeReasonBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.AiFastMode = false;
            SettingsManager.Save();
            UpdateToolbarUI();
            ModePopup.IsOpen = false;
        }

        private void ModeFastBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.AiFastMode = true;
            SettingsManager.Save();
            UpdateToolbarUI();
            ModePopup.IsOpen = false;
        }

        private async void ModelSelectorBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Current;
            ModelListPanel.Children.Clear();
            ModelListPanel.Children.Add(new TextBlock { Text = "Fetching models...", Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Margin = new Thickness(8) });
            ModelPopup.IsOpen = true;

            var models = await AiService.FetchAvailableModelsAsync(settings.AiProvider, settings.AiApiKey, settings.AiBaseUrl);
            
            ModelListPanel.Children.Clear();
            if (models?.Count > 0)
            {
                foreach (var m in models)
                {
                    var btn = new Button { Style = (Style)FindResource("PopupItemButton"), Tag = (m == settings.AiModelName ? "Selected" : "") };
                    btn.Content = new TextBlock { Text = m, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)) };
                    btn.Click += (s, args) => {
                        settings.AiModelName = m;
                        SettingsManager.Save();
                        UpdateToolbarUI();
                        ModelPopup.IsOpen = false;
                    };
                    ModelListPanel.Children.Add(btn);
                }
            }
            else
            {
                ModelListPanel.Children.Add(new TextBlock { Text = "No models available", Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Margin = new Thickness(8) });
            }
        }

        private void NewChatBtn_Click(object sender, RoutedEventArgs e)
        {
            ChatMessagesPanel.Children.Clear();
            ChatSessionManager.CreateNewSession();
            HistoryOverlay.Visibility = Visibility.Collapsed;
        }

        private void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryOverlay.Visibility == Visibility.Collapsed)
            {
                RefreshHistoryPanel();
                HistoryOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                HistoryOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshHistoryPanel()
        {
            HistoryListPanel.Children.Clear();
            HistoryListPanel.Children.Add(new TextBlock { Text = "Recent Output", Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), FontSize = 11, Margin = new Thickness(0, 16, 0, 8) });

            foreach(var session in ChatSessionManager.GetRecentSessions())
            {
                var btn = new Button { Style = (Style)FindResource("PopupItemButton"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 2, 0, 2) };
                if (ChatSessionManager.CurrentSession?.SessionId == session.SessionId) btn.Tag = "Selected";

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                grid.Children.Add(new TextBlock { Text = session.Title, Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13 });
                
                var timeTxt = new TextBlock { Text = ChatSessionManager.GetRelativeTime(session.UpdatedAt), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), FontSize = 11, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(timeTxt, 1);
                grid.Children.Add(timeTxt);

                var delBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Cursor = Cursors.Hand };
                delBtn.Content = new TextBlock { Text = "\xE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 };
                Grid.SetColumn(delBtn, 2);
                grid.Children.Add(delBtn);

                delBtn.Click += (s, ev) => {
                    ev.Handled = true;
                    ChatSessionManager.DeleteSession(session.SessionId);
                    RefreshHistoryPanel();
                };

                btn.Content = grid;
                btn.Click += (s, ev) => { LoadSessionIntoUI(session.SessionId); HistoryOverlay.Visibility = Visibility.Collapsed; };
                HistoryListPanel.Children.Add(btn);
            }
        }

        private void LoadSessionIntoUI(string sessionId)
        {
            var session = ChatSessionManager.LoadSession(sessionId);
            if (session == null) return;

            ChatMessagesPanel.Children.Clear();
            foreach (var msg in session.Messages)
            {
                var bubble = AiChatRenderer.CreateMessageBubble(msg.Content, msg.Role == "user", ChatScrollViewer);
                ChatMessagesPanel.Children.Add(bubble);
            }
            ChatScrollViewer.ScrollToEnd();
        }

        private void MoreOptionsBtn_Click(object sender, RoutedEventArgs e) { }

        private string GetInputText()
        {
            var text = new System.Windows.Documents.TextRange(InputTextBox.Document.ContentStart, InputTextBox.Document.ContentEnd).Text;
            return text.EndsWith(Environment.NewLine) ? text.Substring(0, text.Length - Environment.NewLine.Length) : text;
        }

        private void SetInputText(string text)
        {
            InputTextBox.Document.Blocks.Clear();
            InputTextBox.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text ?? "")) { Margin = new Thickness(0) });
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WatermarkText != null) WatermarkText.Visibility = string.IsNullOrWhiteSpace(GetInputText()) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Alt)) == 0)
            {
                e.Handled = true;
                ProcessMessage(GetInputText().Trim());
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendBtn.Tag?.ToString() == "Stop") _controller.StopStreaming();
            else ProcessMessage(GetInputText().Trim());
        }

        private async void ProcessMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            AddMessageToUI(text, true);
            SetInputText("");
            InputTextBox.IsEnabled = false;
            SendBtn.Tag = "Stop";

            var assistantBubble = AiChatRenderer.CreateMessageBubble("", false, ChatScrollViewer);
            var container = (StackPanel)assistantBubble.Child;
            var textViewer = (Markdig.Wpf.MarkdownViewer)container.Children[0];
            textViewer.Visibility = Visibility.Collapsed;

            var dots = CreateTypingIndicator();
            container.Children.Add(dots);
            ChatMessagesPanel.Children.Add(assistantBubble);

            Expander? thinkExpander = null;
            string thinkBuffer = "";
            string textBuffer = "";

            await _controller.SendMessageStreamAsync(text, 
                onStart: () => { },
                onThinkChunk: (chunk) => {
                    if (thinkExpander == null) {
                        container.Children.Remove(dots);
                        thinkExpander = AiChatRenderer.CreateThoughtExpander("", ChatScrollViewer);
                        thinkExpander.IsExpanded = true;
                        container.Children.Add(thinkExpander);
                    }
                    thinkBuffer += chunk;
                    ((Markdig.Wpf.MarkdownViewer)((Border)thinkExpander.Content).Child).Markdown = thinkBuffer;
                },
                onTextChunk: (chunk) => {
                    if (dots.Parent != null) container.Children.Remove(dots);
                    textViewer.Visibility = Visibility.Visible;
                    textBuffer += chunk;
                    textViewer.Markdown = textBuffer;
                },
                onThinkComplete: (duration) => {
                    if (thinkExpander != null) {
                        thinkExpander.Header = $"Thought for {duration:F1}s";
                        thinkExpander.IsExpanded = false;
                    }
                },
                onComplete: () => {
                    if (dots.Parent != null) container.Children.Remove(dots);
                    FinishProcessing();
                },
                onError: (ex) => {
                    if (dots.Parent != null) container.Children.Remove(dots);
                    textViewer.Visibility = Visibility.Visible;
                    textViewer.Markdown += $"\n[Chat Error: {ex.Message}]";
                    FinishProcessing();
                }
            );
        }

        private void AddMessageToUI(string text, bool isUser)
        {
            var bubble = AiChatRenderer.CreateMessageBubble(text, isUser, ChatScrollViewer);
            ChatMessagesPanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToEnd();
        }

        private void FinishProcessing()
        {
            SendBtn.Tag = null;
            InputTextBox.IsEnabled = true;
            InputTextBox.Focus();
            ChatScrollViewer.ScrollToEnd();
        }

        private Border CreateTypingIndicator()
        {
            var dotsBorder = new Border { Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
            var typingPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            for (int i = 0; i < 3; i++)
            {
                var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(156, 163, 175)), Margin = new Thickness(3, 0, 3, 0), RenderTransform = new ScaleTransform() };
                typingPanel.Children.Add(dot);
                
                var anim = new DoubleAnimation { From = 1.0, To = 1.2, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 150) };
                dot.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                dot.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
            dotsBorder.Child = typingPanel;
            return dotsBorder;
        }
    }
}
