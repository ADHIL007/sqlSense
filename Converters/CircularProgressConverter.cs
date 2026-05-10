using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace sqlSense.Converters
{
    public class CircularProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage && parameter is string circumferenceStr && double.TryParse(circumferenceStr, out double circumference))
            {
                double dashLen = circumference * percentage;
                double gapLen = circumference - dashLen;
                return new DoubleCollection(new[] { dashLen, gapLen });
            }
            return new DoubleCollection(new[] { 0.0, 100.0 });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
