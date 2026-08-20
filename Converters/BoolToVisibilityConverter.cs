using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool val && val;
            string? param = parameter?.ToString()?.ToLowerInvariant();
            bool invert = param == "inverse" || param == "not" || param == "false";
            if (invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
            {
                bool b = vis == Visibility.Visible;
                string? param = parameter?.ToString()?.ToLowerInvariant();
                bool invert = param == "inverse" || param == "not" || param == "false";
                return invert ? !b : b;
            }
            return false;
        }
    }
}
