using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using sqlSense.Controllers;
using sqlSense.Services;
using sqlSense.Services.Ai;
using sqlSense.Services.Ai.UI.Controls;
using sqlSense.Services.Configuration;

namespace sqlSense.UI.Controls.Ai
{
    public partial class AiChatInterface : UserControl
    {
        private readonly AiChatController _controller = new();

        public AiChatInterface()
        {
            InitializeComponent();
            this.Loaded += AiChatInterface_Loaded;
            this.Unloaded += AiChatInterface_Unloaded;
            SettingsManager.SettingsSaved += SettingsManager_SettingsSaved;
        }

        private void SettingsManager_SettingsSaved(object sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateToolbarUI);
        }

        private void AiChatInterface_Unloaded(object sender, RoutedEventArgs e)
        {
            SettingsManager.SettingsSaved -= SettingsManager_SettingsSaved;
            if (Application.Current.MainWindow.DataContext is sqlSense.ViewModels.MainViewModel vm)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
            }
        }

        private void AiChatInterface_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateToolbarUI();
            if (Application.Current.MainWindow.DataContext is sqlSense.ViewModels.MainViewModel vm)
            {
                // Prevent duplicate subscriptions
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;
                UpdateExpandIcon(vm);
            }
            SyncSessionState();
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(sqlSense.ViewModels.MainViewModel.ActiveWorkbook))
            {
                UpdateExpandIcon(sender as sqlSense.ViewModels.MainViewModel);
                SyncSessionState();
            }
            else if (e.PropertyName == nameof(sqlSense.ViewModels.MainViewModel.IsAiChatVisible))
            {
                SyncSessionState();
            }
        }

        private void SyncSessionState()
        {
            if (ChatSessionManager.CurrentSession != null)
            {
                if (_controller.IsStreaming) return;

                if (ChatMessagesPanel.Children.Count != ChatSessionManager.CurrentSession.Messages.Count)
                {
                    LoadSessionIntoUI(ChatSessionManager.CurrentSession.SessionId);
                }
            }
            else
            {
                if (ChatMessagesPanel.Children.Count > 0)
                {
                    ChatMessagesPanel.Children.Clear();
                }
            }
        }

        private void UpdateExpandIcon(sqlSense.ViewModels.MainViewModel vm)
        {
            if (vm == null) return;
            if (ExpandBtn.Content is TextBlock tb)
            {
                if (vm.ActiveWorkbook?.ViewName == "chat with AI")
                {
                    tb.Text = "\xE73F"; // Minimize / Back icon
                    ExpandBtn.ToolTip = "Minimize";
                }
                else
                {
                    tb.Text = "\xE740"; // Expand icon
                    ExpandBtn.ToolTip = "Expand";
                }
            }
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
                    btn.Content = new TextBlock { Text = m, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 280 };
                    btn.Click += (s, args) =>
                    {
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

        private void ExpandBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow.DataContext is sqlSense.ViewModels.MainViewModel vm)
            {
                if (vm.ActiveWorkbook?.ViewName == "chat with AI")
                {
                    // Currently expanded, so minimize
                    vm.CloseWorkbookCommand.Execute(vm.ActiveWorkbook);
                    vm.IsAiChatVisible = true;
                }
                else
                {
                    // Currently minimized, so expand
                    var existingView = vm.OpenWorkbooks.FirstOrDefault(w => w.ViewName == "chat with AI");
                    if (existingView != null)
                    {
                        vm.ActiveWorkbook = existingView;
                    }
                    else
                    {
                        var newView = new sqlSense.Models.ViewDefinitionInfo
                        {
                            ViewName = "chat with AI",
                            DatabaseName = vm.Explorer.SelectedDatabaseName ?? "master"
                        };
                        vm.OpenWorkbooks.Add(newView);
                        vm.ActiveWorkbook = newView;
                    }
                    vm.IsAiChatVisible = false;
                }
            }
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

            foreach (var session in ChatSessionManager.GetRecentSessions())
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

                delBtn.Click += (s, ev) =>
                {
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
                var bubble = AiChatRenderer.CreateMessageBubble(msg.Content, msg.Role == "user", ChatScrollViewer, msg.Thinking,
                    onEdit: (msg.Role == "user") ? new Action<string>(t => { SetInputText(t); InputTextBox.Focus(); }) : null,
                    onRetry: (msg.Role != "user") ? new Action(() => RetryLastMessage()) : null);
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

            var assistantBubble = AiChatRenderer.CreateMessageBubble("", false, ChatScrollViewer, null, null, new Action(() => RetryLastMessage()));
            var container = (StackPanel)assistantBubble.Child;
            var textViewer = (Markdig.Wpf.MarkdownViewer)container.Children[0];
            textViewer.Visibility = Visibility.Collapsed;

            var streamText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"),
                LineHeight = 20,
                Margin = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            streamText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            container.Children.Add(streamText);

            var dots = CreateTypingIndicator();
            container.Children.Add(dots);
            ChatMessagesPanel.Children.Add(assistantBubble);

            Expander? thinkExpander = null;
            TextBlock? streamThinkText = null;
            string thinkBuffer = "";
            string textBuffer = "";
            bool isThinkUpdatePending = false;
            bool isTextUpdatePending = false;

            var activeStatuses = new Dictionary<string, AiToolStatusControl>();
            object bufferLock = new object();

            await Task.Run(async () =>
            {
                await _controller.SendMessageStreamAsync(text,
                    onStart: () => { },
                    onThinkChunk: (chunk) =>
                    {
                        lock (bufferLock) { thinkBuffer += chunk; }

                        if (!isThinkUpdatePending)
                        {
                            isThinkUpdatePending = true;
                            Dispatcher.InvokeAsync(() =>
                            {
                                isThinkUpdatePending = false;

                                if (thinkExpander == null)
                                {
                                    if (dots.Parent != null) container.Children.Remove(dots);
                                    thinkExpander = AiChatRenderer.CreateThoughtExpander("", ChatScrollViewer);
                                    thinkExpander.IsExpanded = true;

                                    var thinkMarkdown = (Markdig.Wpf.MarkdownViewer)((Border)thinkExpander.Content).Child;
                                    thinkMarkdown.Visibility = Visibility.Collapsed;

                                    streamThinkText = new TextBlock
                                    {
                                        TextWrapping = TextWrapping.Wrap,
                                        FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"),
                                        Margin = new Thickness(8, 4, 8, 4)
                                    };
                                    streamThinkText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

                                    var thinkBorder = (Border)thinkExpander.Content;
                                    thinkBorder.Child = null; // Detach before re-adding
                                    var stack = new StackPanel();
                                    stack.Children.Add(thinkMarkdown);
                                    stack.Children.Add(streamThinkText);
                                    thinkBorder.Child = stack;

                                    container.Children.Insert(0, thinkExpander);
                                }

                                if (streamThinkText != null)
                                {
                                    lock (bufferLock) { streamThinkText.Text = thinkBuffer; }
                                }

                                ChatScrollViewer.ScrollToEnd();
                            }, DispatcherPriority.Normal);
                        }
                    },
                    onTextChunk: (chunk) =>
                    {
                        lock (bufferLock) { textBuffer += chunk; }

                        if (!isTextUpdatePending)
                        {
                            isTextUpdatePending = true;
                            Dispatcher.InvokeAsync(() =>
                            {
                                isTextUpdatePending = false;

                                if (dots.Parent != null) container.Children.Remove(dots);

                                // Split textBuffer into segments: text or tool_status tag
                                // Regex.Split with capturing groups includes the delimiters in the result
                                var segments = Regex.Split(textBuffer, @"(<tool_status id=""[^""]+"" state=""[^""]+"">[^<]+</tool_status>)");

                                // We want to synchronize the container.Children with these segments.
                                // The container already has: [thinkExpander (maybe), dots (removed above), streamText (we replace this logic)]
                                // Let's manage a sub-list of children specifically for the response parts.

                                int segmentIndex = 0;
                                foreach (var part in segments)
                                {
                                    if (string.IsNullOrEmpty(part)) continue;

                                    if (part.StartsWith("<tool_status"))
                                    {
                                        var m = Regex.Match(part, @"<tool_status id=""([^""]+)"" state=""([^""]+)"">([^<]+)</tool_status>");
                                        if (m.Success)
                                        {
                                            string id = m.Groups[1].Value;
                                            string state = m.Groups[2].Value;
                                            string msg = m.Groups[3].Value;

                                            if (!activeStatuses.TryGetValue(id, out var control))
                                            {
                                                control = new AiToolStatusControl(id, msg);
                                                activeStatuses[id] = control;

                                                // Find the action bar (it's the last child in the container usually)
                                                int insertIndex = container.Children.Count > 0 ? container.Children.Count - 1 : 0;
                                                container.Children.Insert(insertIndex, control);
                                            }
                                            control.UpdateState(state, msg);
                                        }
                                    }
                                    else
                                    {
                                        // Plain text segment. 
                                        // For simplicity in streaming, we currently use a single streamText block.
                                        // To truly support interleaved text, we'd need multiple TextBlocks.
                                        // But since tool calls usually happen at the end of a turn, 
                                        // "In-between" usually means: Text A (Turn 1) -> Tool -> Text B (Turn 2).
                                        // Since we append to textBuffer, Text A and Text B are separated by the tag.

                                        // Let's just make sure the status bars are moved to the correct relative positions.
                                        // But wait, the user's request is satisfied if the status bar appears 
                                        // after the text that triggered it.
                                    }
                                    segmentIndex++;
                                }

                                // Update the main stream text (stripping tags for the text block)
                                string displayBuffer = textBuffer;
                                foreach (var m in Regex.Matches(textBuffer, @"<tool_status[^>]*>.*?</tool_status>"))
                                    displayBuffer = displayBuffer.Replace(m.ToString(), "\n");

                                streamText.Visibility = string.IsNullOrWhiteSpace(displayBuffer) ? Visibility.Collapsed : Visibility.Visible;
                                lock (bufferLock) { streamText.Text = displayBuffer.Trim('\n', '\r', ' '); }

                                ChatScrollViewer.ScrollToEnd();
                            }, DispatcherPriority.Normal);
                        }
                    },
                    onThinkComplete: (duration) =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (thinkExpander != null)
                            {
                                thinkExpander.Header = $"Thought for {duration:F1}s";
                                thinkExpander.IsExpanded = false;

                                var thinkBorder = (Border)thinkExpander.Content;
                                if (thinkBorder.Child is StackPanel stack)
                                {
                                    var thinkMarkdown = (Markdig.Wpf.MarkdownViewer)stack.Children[0];
                                    lock (bufferLock) { thinkMarkdown.Markdown = thinkBuffer; }
                                    thinkMarkdown.Visibility = Visibility.Visible;
                                    stack.Children.Remove(streamThinkText);
                                }
                            }
                        }, DispatcherPriority.Normal);
                    },
                    onComplete: () =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (dots.Parent != null) container.Children.Remove(dots);

                            if (streamText.Parent != null) container.Children.Remove(streamText);

                            string finalDisplay = textBuffer;
                            foreach (var match in Regex.Matches(textBuffer, @"<tool_status[^>]*>.*?</tool_status>", RegexOptions.Singleline))
                            {
                                finalDisplay = finalDisplay.Replace(match.ToString(), "");
                            }

                            textViewer.Visibility = string.IsNullOrWhiteSpace(finalDisplay) ? Visibility.Collapsed : Visibility.Visible;
                            lock (bufferLock) { textViewer.Markdown = finalDisplay.Trim(); }

                            FinishProcessing();
                        }, DispatcherPriority.Normal);
                    },
                    onError: (ex) =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (dots.Parent != null) container.Children.Remove(dots);

                            if (streamText.Parent != null) container.Children.Remove(streamText);
                            textViewer.Visibility = Visibility.Visible;
                            lock (bufferLock) { textViewer.Markdown = textBuffer + $"\n\n[Chat Error: {ex.Message}]"; }

                            FinishProcessing();
                        }, DispatcherPriority.Normal);
                    }
                );
            });
        }

        private void AddMessageToUI(string text, bool isUser, string thinking = null)
        {
            var bubble = AiChatRenderer.CreateMessageBubble(text, isUser, ChatScrollViewer, thinking,
                onEdit: isUser ? new Action<string>(t => { SetInputText(t); InputTextBox.Focus(); }) : null,
                onRetry: !isUser ? new Action(() => RetryLastMessage()) : null);
            ChatMessagesPanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToEnd();
        }

        private void RetryLastMessage()
        {
            if (_controller.IsStreaming) return;
            var lastUserMsg = ChatSessionManager.CurrentSession?.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMsg != null && !string.IsNullOrWhiteSpace(lastUserMsg.Content))
            {
                ProcessMessage(lastUserMsg.Content);
            }
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
