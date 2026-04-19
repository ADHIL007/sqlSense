using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Markdig;

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
            FastModeText.Foreground = settings.AiFastMode ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 195, 247));
            
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

        private void UpdateModePopupSelection()
        {
            var settings = sqlSense.Services.SettingsManager.Current;
            ModeReasonBtn.Tag = settings.AiFastMode ? "" : "Selected";
            ModeFastBtn.Tag = settings.AiFastMode ? "Selected" : "";
        }

        private void FastModeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateModePopupSelection();
            ModePopup.IsOpen = true;
        }

        private void ModeReasonBtn_Click(object sender, RoutedEventArgs e)
        {
            sqlSense.Services.SettingsManager.Current.AiFastMode = false;
            sqlSense.Services.SettingsManager.Save();
            UpdateToolbarUI();
            ModePopup.IsOpen = false;
        }

        private void ModeFastBtn_Click(object sender, RoutedEventArgs e)
        {
            sqlSense.Services.SettingsManager.Current.AiFastMode = true;
            sqlSense.Services.SettingsManager.Save();
            UpdateToolbarUI();
            ModePopup.IsOpen = false;
        }

        private async void ModelSelectorBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = sqlSense.Services.SettingsManager.Current;
            
            ModelListPanel.Children.Clear();
            var loadingText = new TextBlock { Text = "Fetching models...", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)), Margin = new Thickness(8) };
            ModelListPanel.Children.Add(loadingText);
            
            ModelPopup.IsOpen = true;

            var models = await sqlSense.Services.AiService.FetchAvailableModelsAsync(settings.AiProvider, settings.AiApiKey, settings.AiBaseUrl);
            
            ModelListPanel.Children.Clear();
            if (models != null && models.Count > 0)
            {
                foreach (var m in models)
                {
                    var btn = new Button { Style = (Style)FindResource("PopupItemButton") };
                    if (m == settings.AiModelName) btn.Tag = "Selected";
                    
                    var txt = new TextBlock { Text = m, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224)) };
                    btn.Content = txt;
                    
                    btn.Click += (s, args) => 
                    {
                        settings.AiModelName = m;
                        sqlSense.Services.SettingsManager.Save();
                        UpdateToolbarUI();
                        ModelPopup.IsOpen = false;
                    };
                    ModelListPanel.Children.Add(btn);
                }
            }
            else
            {
                var errorText = new TextBlock { Text = "No models available / Check Settings", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)), Margin = new Thickness(8) };
                ModelListPanel.Children.Add(errorText);
            }
        }

        private void NewChatBtn_Click(object sender, RoutedEventArgs e)
        {
            ChatMessagesPanel.Children.Clear();
            HistoryOverlay.Visibility = Visibility.Collapsed;
        }

        private void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            HistoryOverlay.Visibility = HistoryOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void MoreOptionsBtn_Click(object sender, RoutedEventArgs e)
        {
            // Future implementation
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ChatUI.Visibility = Visibility.Collapsed;
            FloatingAIButton.Visibility = Visibility.Visible;
            HistoryOverlay.Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FloatingAIButton_Click(object sender, RoutedEventArgs e)
        {
            FloatingAIButton.Visibility = Visibility.Collapsed;
            ChatUI.Visibility = Visibility.Visible;
        }

        private string GetInputText()
        {
            var text = new System.Windows.Documents.TextRange(InputTextBox.Document.ContentStart, InputTextBox.Document.ContentEnd).Text;
            if (text.EndsWith(Environment.NewLine)) text = text.Substring(0, text.Length - Environment.NewLine.Length);
            return text;
        }

        private void SetInputText(string text)
        {
            InputTextBox.Document.Blocks.Clear();
            InputTextBox.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text ?? "")) { Margin = new Thickness(0) });
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WatermarkText != null)
            {
                WatermarkText.Visibility = string.IsNullOrWhiteSpace(GetInputText()) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift && (Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
                {
                    e.Handled = true;
                    ProcessMessage(GetInputText().Trim());
                }
            }
        }

        private bool _cancelStream = false;

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendBtn != null && SendBtn.Tag?.ToString() == "Stop")
            {
                _cancelStream = true;
                return;
            }
            ProcessMessage(GetInputText().Trim());
        }

        private async void ProcessMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            AddMessage(text, true);
            SetInputText("");
            InputTextBox.IsEnabled = false; // Prevent typing while waiting
            _cancelStream = false;
            SendBtn.Tag = "Stop";

            var loadingBorder = AddMessage("", false, false);
            
            try
            {
                if (loadingBorder.Child is StackPanel container)
                {
                    Markdig.Wpf.MarkdownViewer currentTxt = (Markdig.Wpf.MarkdownViewer)container.Children[0];
                    currentTxt.Visibility = Visibility.Collapsed; // Hide empty text block initially
                    System.Windows.Controls.Expander thinkExpander = null;
                    Markdig.Wpf.MarkdownViewer thinkTxt = null;
                    bool isThinking = false;
                    string buffer = "";
                    string currentTextBuffer = "";
                    string thinkTextBuffer = "";
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

                    var dotsBorder = new Border { Height = 20, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
                    var typingPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    
                    var brush1 = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
                    var brush2 = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
                    var brush3 = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
                    
                    var dot1 = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = brush1, Margin = new Thickness(3,0,3,0), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), RenderTransform = new System.Windows.Media.ScaleTransform() };
                    var dot2 = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = brush2, Margin = new Thickness(3,0,3,0), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), RenderTransform = new System.Windows.Media.ScaleTransform() };
                    var dot3 = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = brush3, Margin = new Thickness(3,0,3,0), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), RenderTransform = new System.Windows.Media.ScaleTransform() };
                    
                    typingPanel.Children.Add(dot1);
                    typingPanel.Children.Add(dot2);
                    typingPanel.Children.Add(dot3);
                    dotsBorder.Child = typingPanel;
                    container.Children.Add(dotsBorder);

                    var da1 = new System.Windows.Media.Animation.DoubleAnimation { From = 1.0, To = 1.2, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                    var da2 = new System.Windows.Media.Animation.DoubleAnimation { From = 1.0, To = 1.2, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(150) };
                    var da3 = new System.Windows.Media.Animation.DoubleAnimation { From = 1.0, To = 1.2, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(300) };
                    
                    ((System.Windows.Media.ScaleTransform)dot1.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, da1);
                    ((System.Windows.Media.ScaleTransform)dot1.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, da1);
                    ((System.Windows.Media.ScaleTransform)dot2.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, da2);
                    ((System.Windows.Media.ScaleTransform)dot2.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, da2);
                    ((System.Windows.Media.ScaleTransform)dot3.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, da3);
                    ((System.Windows.Media.ScaleTransform)dot3.RenderTransform).BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, da3);
                    
                    brush1.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation { From = System.Windows.Media.Color.FromRgb(156,163,175), To = System.Windows.Media.Colors.White, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                    brush2.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation { From = System.Windows.Media.Color.FromRgb(156,163,175), To = System.Windows.Media.Colors.White, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(150) });
                    brush3.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation { From = System.Windows.Media.Color.FromRgb(156,163,175), To = System.Windows.Media.Colors.White, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(300) });
                    bool isFirstChunk = true;

                    await foreach (var chunk in sqlSense.Services.AiService.SendMessageStreamAsync(text))
                    {
                        if (_cancelStream) break;
                        
                        if (isFirstChunk)
                        {
                            isFirstChunk = false;
                            container.Children.Remove(dotsBorder);
                        }
                        string process = buffer + chunk;
                        buffer = "";
                        
                        while (process.Length > 0)
                        {
                            if (!isThinking)
                            {
                                int idx = process.IndexOf("<think>");
                                if (idx >= 0)
                                {
                                    isThinking = true;
                                    if (idx > 0) { 
                                        currentTxt.Visibility = Visibility.Visible; 
                                        currentTextBuffer += process.Substring(0, idx);
                                        currentTxt.Markdown = currentTextBuffer;
                                    }
                                    
                                    thinkTxt = new Markdig.Wpf.MarkdownViewer { 
                                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)), 
                                        Margin = new Thickness(8,4,8,4), Background = Brushes.Transparent, BorderThickness = new Thickness(0), 
                                        Pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables().Build() };
                                    ScrollViewer.SetVerticalScrollBarVisibility(thinkTxt, ScrollBarVisibility.Disabled);
                                    thinkTxt.PreviewMouseWheel += (s, e) => { e.Handled = true; var ev = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = s }; ChatScrollViewer.RaiseEvent(ev); };
                                    thinkExpander = new System.Windows.Controls.Expander { 
                                        Header = "Thinking...", 
                                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                                        Content = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0, 0, 0)), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x10, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = thinkTxt },
                                        Margin = new Thickness(0, 4, 0, 8),
                                        IsExpanded = true
                                    };
                                    container.Children.Add(thinkExpander);
                                    
                                    sw.Restart();
                                    process = process.Substring(idx + 7).TrimStart('\r', '\n');
                                }
                                else
                                {
                                    int pIdx = -1;
                                    for (int i = 1; i <= 6 && i <= process.Length; i++) {
                                        if ("<think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                    }
                                    if (pIdx >= 0) {
                                        if (pIdx > 0) { 
                                            currentTxt.Visibility = Visibility.Visible; 
                                            currentTextBuffer += process.Substring(0, pIdx);
                                            currentTxt.Markdown = currentTextBuffer;
                                        }
                                        buffer = process.Substring(pIdx);
                                        process = "";
                                    } else {
                                        currentTxt.Visibility = Visibility.Visible;
                                        currentTextBuffer += process;
                                        currentTxt.Markdown = currentTextBuffer;
                                        process = "";
                                    }
                                }
                            }
                            else
                            {
                                int idx = process.IndexOf("</think>");
                                if (idx >= 0)
                                {
                                    isThinking = false;
                                    sw.Stop();
                                    if (thinkExpander != null) {
                                        thinkExpander.Header = $"Thought for {sw.Elapsed.TotalSeconds:F1}s";
                                        thinkExpander.IsExpanded = false;
                                    }
                                    thinkTextBuffer += process.Substring(0, idx).TrimEnd('\r', '\n');
                                    thinkTxt.Markdown = thinkTextBuffer;
                                    process = process.Substring(idx + 8).TrimStart('\r', '\n');
                                    
                                    currentTxt = new Markdig.Wpf.MarkdownViewer { 
                                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xED, 0xF3)), 
                                        Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0),
                                        Pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables().Build() };
                                    ScrollViewer.SetVerticalScrollBarVisibility(currentTxt, ScrollBarVisibility.Disabled);
                                    currentTxt.PreviewMouseWheel += (s, e) => { e.Handled = true; var ev = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = s }; ChatScrollViewer.RaiseEvent(ev); };
                                    container.Children.Add(currentTxt);
                                }
                                else
                                {
                                    int pIdx = -1;
                                    for (int i = 1; i <= 7 && i <= process.Length; i++) {
                                        if ("</think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                    }
                                    if (pIdx >= 0) {
                                        if (pIdx > 0) thinkTextBuffer += process.Substring(0, pIdx);
                                        thinkTxt.Markdown = thinkTextBuffer;
                                        buffer = process.Substring(pIdx);
                                        process = "";
                                    } else {
                                        thinkTextBuffer += process;
                                        thinkTxt.Markdown = thinkTextBuffer;
                                        process = "";
                                    }
                                }
                            }
                        }

                        ChatScrollViewer.ScrollToEnd();
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                    
                    if (isThinking && buffer.Length > 0 && buffer != "</think") 
                    {
                        thinkTextBuffer += buffer;
                        thinkTxt.Markdown = thinkTextBuffer;
                        sw.Stop();
                        if (thinkExpander != null) thinkExpander.Header = $"Thought for {sw.Elapsed.TotalSeconds:F1}s";
                    }
                    else if (!isThinking && buffer.Length > 0 && buffer != "<think")
                    {
                        currentTxt.Visibility = Visibility.Visible;
                        currentTextBuffer += buffer;
                        currentTxt.Markdown = currentTextBuffer;
                    }
                    
                    if (isFirstChunk)
                    {
                        container.Children.Remove(dotsBorder);
                    }
                }
            }
            catch (Exception ex)
            {
                if (loadingBorder.Child is StackPanel container)
                {
                    if (container.Children.Count > 1 && container.Children[container.Children.Count - 1] is Border dotsBorder && dotsBorder.Child is StackPanel)
                        container.Children.Remove(dotsBorder);

                    Markdig.Wpf.MarkdownViewer txt = (Markdig.Wpf.MarkdownViewer)container.Children[0];
                    txt.Visibility = Visibility.Visible;
                    txt.Markdown += $"\n[Chat Error: {ex.Message}]";
                }
            }
            finally
            {
                if (SendBtn != null) SendBtn.Tag = null;
                InputTextBox.IsEnabled = true;
                InputTextBox.Focus();
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private Border AddMessage(string text, bool isUser, bool isTemporary = false)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 2),
                Margin = new Thickness(isUser ? 50 : 0, 4, isUser ? 0 : 16, 8),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = isUser ? 350 : 500,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Colors.Black, BlurRadius = 15, ShadowDepth = 5, Opacity = 0.3 }
            };

            if (isUser)
            {
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
                border.CornerRadius = new CornerRadius(12, 12, 4, 12);
            }
            else
            {
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
                border.CornerRadius = new CornerRadius(12, 12, 12, 4);
            }

            var container = new StackPanel { Orientation = Orientation.Vertical };
            var textBox = new Markdig.Wpf.MarkdownViewer
            {
                Markdown = text,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xED, 0xF3)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                Pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables().Build()
            };
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Disabled);
            textBox.PreviewMouseWheel += (s, e) => { e.Handled = true; var ev = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = s }; ChatScrollViewer.RaiseEvent(ev); };

            container.Children.Add(textBox);
            border.Child = container;
            ChatMessagesPanel.Children.Add(border);

            // Add slide and fade animation
            border.Opacity = 0;
            border.RenderTransform = new System.Windows.Media.TranslateTransform { Y = 10 };
            
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150) };
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new System.Windows.Media.Animation.QuadraticEase() };
            
            border.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            border.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);

            ChatScrollViewer.ScrollToEnd();
            return border;
        }
    }
}
