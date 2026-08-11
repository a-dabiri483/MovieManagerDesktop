using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                bool isInverse = parameter?.ToString() == "Inverse";
                if (isInverse)
                    return intValue == 0 ? Visibility.Collapsed : Visibility.Visible;
                else
                    return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
