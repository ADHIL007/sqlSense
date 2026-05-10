using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace sqlSense.Converters
{
    public class PercentageToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // Multiply by a factor (e.g. 100) to keep ratios intact when using Star sizing
                return new GridLength(percentage * 100, GridUnitType.Star);
            }
            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
