using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace sqlSense
{
    /// <summary>
    /// Converter to calculate tree item indentation based on depth level
    /// </summary>
    public class TreeIndentConverter : IValueConverter
    {
        public static readonly TreeIndentConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TreeViewItem item)
            {
                int depth = GetDepth(item);
                return new Thickness(depth * 16, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static int GetDepth(TreeViewItem item)
        {
            int depth = 0;
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (parent != null)
            {
                if (parent is TreeViewItem)
                    depth++;
                if (parent is TreeView)
                    break;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return depth;
        }
    }
}
