using System;
using System.Windows;
using System.Windows.Controls;
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

        public static Border CreateMessageBubble(string content, bool isUser, ScrollViewer parentScroll, string thinking = null)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 2),
                Margin = new Thickness(isUser ? 50 : 0, 4, isUser ? 0 : 16, 8),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = isUser ? 350 : 500,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, ShadowDepth = 5, Opacity = 0.3 }
            };

            border.Background = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            border.CornerRadius = isUser ? new CornerRadius(12, 12, 4, 12) : new CornerRadius(12, 12, 12, 4);

            var container = new StackPanel { Orientation = Orientation.Vertical };
            
            if (!string.IsNullOrEmpty(thinking))
            {
                var expander = CreateThoughtExpander(thinking, parentScroll);
                expander.IsExpanded = false;
                container.Children.Add(expander);
            }

            var textBox = CreateMarkdownViewer(content, parentScroll);
            container.Children.Add(textBox);
            
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
