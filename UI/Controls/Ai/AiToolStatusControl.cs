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

        private readonly Border _border;
        private readonly ContentControl _iconContainer;
        private readonly TextBlock _textBlock;

        private static class Theme
        {
            // Backgrounds
            public static readonly Color IdleBg = Color.FromArgb(0xFF, 0x12, 0x12, 0x12); // near-black
            public static readonly Color IdleBorder = Color.FromArgb(0xFF, 0x38, 0x38, 0x38); // grey border
            public static readonly Color ErrorBg = Color.FromArgb(0xFF, 0x14, 0x0A, 0x0A);
            public static readonly Color ErrorBorder = Color.FromArgb(0xFF, 0x55, 0x22, 0x22);

            // Text — small, muted
            public static readonly Color TextMuted = Color.FromRgb(110, 110, 110); // dim grey
            public static readonly Color TextDefault = Color.FromRgb(160, 160, 155); // warm grey-white
            public static readonly Color TextError = Color.FromRgb(200, 80, 80);

            // Dots
            public static readonly Color SuccessFill = Color.FromRgb(60, 160, 90);
            public static readonly Color SuccessBorder = Color.FromRgb(40, 110, 65);
            public static readonly Color ErrorFill = Color.FromRgb(180, 55, 55);
            public static readonly Color ErrorBorderDot = Color.FromRgb(130, 35, 35);

            // Spinner
            public static readonly Color SpinnerArc = Color.FromRgb(90, 140, 200);

            // Sizing — small & tight
            public const double IconSize = 8;
            public const double DotSize = 6;
            public const double LabelSize = 10;   // small text
            public const double IconGap = 6;
            public const double PadH = 10;
            public const double PadV = 5;
            public const double CornerRadius = 20;   // pill / round
            public const double ItemMarginV = 3;
            public const double BorderWidth = 1;
        }

        public AiToolStatusControl(string id, string message)
        {
            StatusId = id;

            _iconContainer = new ContentControl
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, Theme.IconGap, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = Theme.LabelSize,
                VerticalAlignment = VerticalAlignment.Center,
                //CharacterSpacing  = -20
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(_iconContainer);
            row.Children.Add(_textBlock);

            _border = new Border
            {
                Child = row,
                CornerRadius = new CornerRadius(Theme.CornerRadius),
                BorderThickness = new Thickness(Theme.BorderWidth),
                Padding = new Thickness(Theme.PadH, Theme.PadV, Theme.PadH, Theme.PadV),
                Margin = new Thickness(0, Theme.ItemMarginV, 0, Theme.ItemMarginV),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Content = _border;
            ApplyFadeIn();
            UpdateState(StatusState.Loading, message);
        }

        // ── State API ─────────────────────────────────────────────────────────

        public enum StatusState { Loading, Success, Error }

        public void UpdateState(string state, string message)
        {
            if (!Enum.TryParse<StatusState>(state, ignoreCase: true, out var parsed))
                parsed = StatusState.Loading;
            UpdateState(parsed, message);
        }

        public void UpdateState(StatusState state, string message)
        {
            _textBlock.Text = message;

            switch (state)
            {
                case StatusState.Loading:
                    SetBackground(Theme.IdleBg, Theme.IdleBorder);
                    _iconContainer.Content = BuildSpinner();
                    _textBlock.Foreground = Brush(Theme.TextMuted);
                    break;

                case StatusState.Success:
                    SetBackground(Theme.IdleBg, Theme.IdleBorder);
                    _iconContainer.Content = BuildDot(Theme.SuccessFill, Theme.SuccessBorder);
                    _textBlock.Foreground = Brush(Theme.TextDefault);
                    AnimateDotPop(_iconContainer);
                    break;

                case StatusState.Error:
                    SetBackground(Theme.ErrorBg, Theme.ErrorBorder);
                    _iconContainer.Content = BuildDot(Theme.ErrorFill, Theme.ErrorBorderDot);
                    _textBlock.Foreground = Brush(Theme.TextError);
                    AnimateDotPop(_iconContainer);
                    AnimateShake(_border);
                    break;
            }
        }

        // ── Builders ──────────────────────────────────────────────────────────

        private FrameworkElement BuildSpinner()
        {
            var arc = new Ellipse
            {
                Width = Theme.IconSize,
                Height = Theme.IconSize,
                Stroke = Brush(Theme.SpinnerArc),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 2.5, 1.5 },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            arc.RenderTransform.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9))
                { RepeatBehavior = RepeatBehavior.Forever });

            return arc;
        }

        private FrameworkElement BuildDot(Color fill, Color stroke)
        {
            return new Ellipse
            {
                Width = Theme.DotSize,
                Height = Theme.DotSize,
                Fill = Brush(fill),
                Stroke = Brush(stroke),
                StrokeThickness = 1,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        // ── Animations ────────────────────────────────────────────────────────

        private void ApplyFadeIn()
        {
            _border.Opacity = 0;

            _border.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            _border.BeginAnimation(MarginProperty,
                new ThicknessAnimation(
                    new Thickness(0, Theme.ItemMarginV + 5, 0, 0),
                    new Thickness(0, Theme.ItemMarginV, 0, 0),
                    TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private static void AnimateDotPop(ContentControl container)
        {
            var scale = new ScaleTransform(0.4, 0.4, 5, 5);
            container.RenderTransform = scale;

            var pop = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(260))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop.Clone());
        }

        private static void AnimateShake(UIElement element)
        {
            var t = new TranslateTransform();
            element.RenderTransform = t;

            var shake = new DoubleAnimationUsingKeyFrames();
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(55))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(165))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(275))));

            t.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetBackground(Color bg, Color border)
        {
            _border.Background = Brush(bg);
            _border.BorderBrush = Brush(border);
        }

        private static SolidColorBrush Brush(Color c) => new SolidColorBrush(c);
    }
}