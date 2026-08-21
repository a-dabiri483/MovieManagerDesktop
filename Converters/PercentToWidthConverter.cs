using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    /// <summary>
    /// Converts a percentage (0-100) and a container width to the actual pixel width for a progress bar.
    /// values[0] = WatchProgressPercent (double, 0 to 100)
    /// values[1] = ActualWidth of parent container (double)
    /// </summary>
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double percent &&
                values[1] is double containerWidth &&
                containerWidth > 0)
            {
                double clampedPercent = Math.Max(0, Math.Min(100, percent));
                return containerWidth * (clampedPercent / 100.0);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
