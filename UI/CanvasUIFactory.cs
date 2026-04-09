using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace sqlSense.UI
{
    public static class CanvasUIFactory
    {
        public static Border CreateActionButton(string icon, string text, Color color)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            if (!string.IsNullOrEmpty(icon))
            {
                content.Children.Add(new TextBlock {
                    Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = new SolidColorBrush(color), FontSize = 12,
                    Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
                });
            }

            content.Children.Add(new TextBlock {
                Text = text, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(color), FontSize = 12, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var btn = new Border {
                Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 14, 8),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = color, BlurRadius = 12, Opacity = 0.3 },
                Child = content
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B));
            return btn;
        }

        public static void AddClauseEditor(StackPanel parent, string icon, string label, string value, Action<string> setter)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 3) };
            
            if (!string.IsNullOrEmpty(icon))
            {
                header.Children.Add(new TextBlock {
                    Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            header.Children.Add(new TextBlock {
                Text = label, FontFamily = new FontFamily("Segoe UI"),
                Foreground = Brushes.Gray, FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });

            parent.Children.Add(header);

            var box = new TextBox {
                Text = value, Background = new SolidColorBrush(Color.FromArgb(0x44, 0, 0, 0)),
                Foreground = Brushes.Cyan, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                BorderThickness = new Thickness(1), Padding = new Thickness(6),
                MinHeight = 28, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true
            };
            box.LostFocus += (s, e) => setter(box.Text);
            parent.Children.Add(box);
        }

        public static string HslToRgbHex(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0) r = g = b = l;
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360 + 1.0 / 3);
                g = HueToRgb(p, q, h / 360);
                b = HueToRgb(p, q, h / 360 - 1.0 / 3);
            }

            return string.Format("#{0:X2}{1:X2}{2:X2}", (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
