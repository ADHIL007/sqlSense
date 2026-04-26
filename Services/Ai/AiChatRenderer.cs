using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Markdig;
using Markdig.Wpf;

namespace sqlSense.Services.Ai
{
    public class AiChatRenderer
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .Build();

        public static Border CreateMessageBubble(string content, bool isUser, ScrollViewer parentScroll, string thinking = null, Action<string> onEdit = null, Action onRetry = null)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(isUser ? 1 : 0),
                Padding = new Thickness(isUser ? 12 : 0, isUser ? 10 : 4, isUser ? 12 : 0, isUser ? 2 : 2),
                Margin = new Thickness(  0, 4, isUser ? 0 : 0, 8),
                HorizontalAlignment =  HorizontalAlignment.Stretch,
                MaxWidth =  double.PositiveInfinity,
                Effect = isUser ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, ShadowDepth = 5, Opacity = 0.3 } : null
            };

            border.Background = isUser ? new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent;
            border.BorderBrush = isUser ? new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent;
            border.CornerRadius = isUser ? new CornerRadius(12, 12, 4, 12) : new CornerRadius(0);

            var container = new StackPanel { Orientation = Orientation.Vertical };
            
            if (!string.IsNullOrEmpty(thinking))
            {
                var expander = CreateThoughtExpander(thinking, parentScroll);
                expander.IsExpanded = false;
                container.Children.Add(expander);
            }

            var textBox = CreateMarkdownViewer(content, parentScroll);
            
            if (isUser)
            {
                var textContainer = new Grid();
                textContainer.Children.Add(textBox);

                bool isExpanded = false;
                bool isInitialized = false;

                textBox.SizeChanged += (s, e) =>
                {
                    if (isInitialized || isExpanded) return;
                    if (e.NewSize.Height > 120)
                    {
                        isInitialized = true;
                        textContainer.MaxHeight = 120;
                        
                        var mask = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(0, 1),
                            GradientStops = new GradientStopCollection
                            {
                                new GradientStop(Colors.Black, 0),
                                new GradientStop(Colors.Black, 0.6),
                                new GradientStop(Colors.Transparent, 1)
                            }
                        };
                        textContainer.OpacityMask = mask;

                        var clickOverlay = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Click to expand" };
                        clickOverlay.MouseLeftButtonDown += (sender, args) =>
                        {
                            isExpanded = true;
                            textContainer.MaxHeight = double.PositiveInfinity;
                            textContainer.OpacityMask = null;
                            clickOverlay.Visibility = Visibility.Collapsed;
                        };
                        textContainer.Children.Add(clickOverlay);
                    }
                };

                container.Children.Add(textContainer);
            }
            else
            {
                container.Children.Add(textBox);
            }
            
            // Action Bar
            var actionBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Hidden };
            
            border.MouseEnter += (s, e) => actionBar.Visibility = Visibility.Visible;
            border.MouseLeave += (s, e) => actionBar.Visibility = Visibility.Hidden;

            var copyBtn = new Button { Content = new TextBlock { Text = "\xE8C8", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Copy" };
            copyBtn.Click += (s, e) => { 
                try { Clipboard.SetText(content ?? ""); } catch { } 
            };
            actionBar.Children.Add(copyBtn);

            if (isUser)
            {
                var editBtn = new Button { Content = new TextBlock { Text = "\xE70F", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Edit" };
                editBtn.Click += (s, e) => { onEdit?.Invoke(content); };
                actionBar.Children.Add(editBtn);
            }
            else 
            {
                var retryBtn = new Button { Content = new TextBlock { Text = "\xE895", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Retry" };
                retryBtn.Click += (s, e) => { onRetry?.Invoke(); };
                actionBar.Children.Add(retryBtn);
            }
            
            container.Children.Add(actionBar);

            border.Child = container;
            return border;
        }

        public static MarkdownViewer CreateMarkdownViewer(string markdown, ScrollViewer parentScroll)
        {
            var viewer = new MarkdownViewer
            {
                Markdown = markdown,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                Pipeline = _pipeline,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI")
            };
            
            viewer.SetResourceReference(MarkdownViewer.ForegroundProperty, "TextPrimaryBrush");
            
            ScrollViewer.SetVerticalScrollBarVisibility(viewer, ScrollBarVisibility.Disabled);
            viewer.PreviewMouseWheel += (s, e) => 
            {
                e.Handled = true;
                var ev = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) 
                { 
                    RoutedEvent = UIElement.MouseWheelEvent, 
                    Source = s 
                };
                parentScroll.RaiseEvent(ev);
            };
            
            return viewer;
        }

        public static Expander CreateThoughtExpander(string thinkPart, ScrollViewer parentScroll)
        {
            var thinkTxt = CreateMarkdownViewer(thinkPart, parentScroll);
            thinkTxt.SetResourceReference(MarkdownViewer.ForegroundProperty, "MutedBrush");
            thinkTxt.Margin = new Thickness(8, 4, 8, 4);

            var expander = new Expander
            {
                Header = "Thought Process",
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x10, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child = thinkTxt
                },
                Margin = new Thickness(0, 4, 0, 8),
                IsExpanded = false
            };
            
            expander.SetResourceReference(Expander.ForegroundProperty, "MutedBrush");
            return expander;
        }
    }
}
