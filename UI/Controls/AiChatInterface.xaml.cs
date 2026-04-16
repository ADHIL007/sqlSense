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
                    ProcessMessage(InputTextBox.Text.Trim());
                }
                // Shift+Enter falls through to native behaviour (new line)
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessMessage(InputTextBox.Text.Trim());
        }

        private async void ProcessMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            AddMessage(text, true);
            InputTextBox.Text = "";
            InputTextBox.IsEnabled = false; // Prevent typing while waiting

            var loadingBorder = AddMessage("", false, false);
            
            try
            {
                if (loadingBorder.Child is StackPanel container)
                {
                    TextBlock currentTxt = (TextBlock)container.Children[0];
                    System.Windows.Controls.Expander thinkExpander = null;
                    TextBlock thinkTxt = null;
                    bool isThinking = false;
                    string buffer = "";
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

                    await foreach (var chunk in sqlSense.Services.AiService.SendMessageStreamAsync(text))
                    {
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
                                    if (idx > 0) currentTxt.Text += process.Substring(0, idx);
                                    
                                    thinkTxt = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)), Margin = new Thickness(12), FontSize = 12, LineHeight = 18 };
                                    thinkExpander = new System.Windows.Controls.Expander { 
                                        Header = "Thinking...", 
                                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                                        Content = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(15, 255, 255, 255)), CornerRadius = new CornerRadius(6), Child = thinkTxt },
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
                                        if (pIdx > 0) currentTxt.Text += process.Substring(0, pIdx);
                                        buffer = process.Substring(pIdx);
                                        process = "";
                                    } else {
                                        currentTxt.Text += process;
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
                                    thinkTxt.Text += process.Substring(0, idx).TrimEnd('\r', '\n');
                                    process = process.Substring(idx + 8).TrimStart('\r', '\n');
                                    
                                    currentTxt = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"], FontSize = 13, LineHeight = 20 };
                                    container.Children.Add(currentTxt);
                                }
                                else
                                {
                                    int pIdx = -1;
                                    for (int i = 1; i <= 7 && i <= process.Length; i++) {
                                        if ("</think>".StartsWith(process.Substring(process.Length - i))) { pIdx = process.Length - i; break; }
                                    }
                                    if (pIdx >= 0) {
                                        if (pIdx > 0) thinkTxt.Text += process.Substring(0, pIdx);
                                        buffer = process.Substring(pIdx);
                                        process = "";
                                    } else {
                                        thinkTxt.Text += process;
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
                        thinkTxt.Text += buffer;
                        sw.Stop();
                        if (thinkExpander != null) thinkExpander.Header = $"Thought for {sw.Elapsed.TotalSeconds:F1}s";
                    }
                    else if (!isThinking && buffer.Length > 0 && buffer != "<think")
                    {
                        currentTxt.Text += buffer;
                    }
                }
            }
            catch (Exception ex)
            {
                if (loadingBorder.Child is StackPanel container)
                {
                    TextBlock txt = (TextBlock)container.Children[0];
                    txt.Text += $"\n[Chat Error: {ex.Message}]";
                }
            }
            finally
            {
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
                border.Background = System.Windows.Media.Brushes.Transparent;
                border.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
                border.CornerRadius = new CornerRadius(8, 8, 8, 0);
            }

            var container = new StackPanel { Orientation = Orientation.Vertical };
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources[isUser ? "TextPrimaryBrush" : "TextSecondaryBrush"],
                FontSize = 13,
                LineHeight = 20
            };

            container.Children.Add(textBlock);
            border.Child = container;
            ChatMessagesPanel.Children.Add(border);

            ChatScrollViewer.ScrollToEnd();
            return border;
        }
    }
}
