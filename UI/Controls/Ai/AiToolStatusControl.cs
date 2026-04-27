using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace sqlSense.Services.Ai.UI.Controls
{
    public class AiToolStatusControl : UserControl
    {
        public string StatusId { get; }

        private readonly ContentControl _iconContainer;
        private readonly TextBlock _textBlock;

        public AiToolStatusControl(string id, string message)
        {
            StatusId = id;

            _iconContainer = new ContentControl
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            row.Children.Add(_iconContainer);
            row.Children.Add(_textBlock);

            Content = row;

            UpdateState(StatusState.Loading, message);
        }

        // ── State API ─────────────────────────────

        public enum StatusState
        {
            Loading,
            Success,
            Error
        }

        public void UpdateState(string state, string message)
        {
            if (!Enum.TryParse<StatusState>(state, true, out var parsed))
                parsed = StatusState.Loading;

            UpdateState(parsed, message);
        }

        public void UpdateState(StatusState state, string message)
        {
            _textBlock.Text = message;

            switch (state)
            {
                case StatusState.Loading:
                    _iconContainer.Content = BuildSpinner();
                    _textBlock.Foreground = Brushes.Gray;
                    break;

                case StatusState.Success:
                    _iconContainer.Content = BuildSymbol("✔", Brushes.Green);
                    _textBlock.Foreground = Brushes.Gray;
                    break;

                case StatusState.Error:
                    _iconContainer.Content = BuildSymbol("✖", Brushes.Red);
                    _textBlock.Foreground = Brushes.Red;
                    break;
            }
        }

        // ── UI Builders ───────────────────────────

        private FrameworkElement BuildSpinner()
        {
            var arc = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = Brushes.Gray,
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(0)
            };

            arc.RenderTransform.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                });

            return arc;
        }

        private FrameworkElement BuildDot(Color color)
        {
            return new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        private FrameworkElement BuildSymbol(string symbol, Brush color)
        {
            return new Grid
            {
                Width = 16,
                Height = 16,
                Children =
        {
            new TextBlock
            {
                Text = symbol,
                Foreground = color,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        }
            };
        }
    }
}