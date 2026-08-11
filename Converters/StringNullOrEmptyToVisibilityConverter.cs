using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool stringIsNullOrEmpty = string.IsNullOrEmpty(value as string);
            bool isInverse = parameter?.ToString() == "Inverse";
            
            if (isInverse)
                return stringIsNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
            else
                return stringIsNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
