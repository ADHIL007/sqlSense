using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace sqlSense.Converters
{
    /// <summary>
    /// Converts a hex color string (e.g. "#4FC3F7") to a SolidColorBrush
    /// </summary>
    public class StringToBrushConverter : IValueConverter
    {
        public static readonly StringToBrushConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorStr);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
