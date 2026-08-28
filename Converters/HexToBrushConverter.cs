using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MovieManagerDesktop.Converters
{
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly BrushConverter Converter = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var brush = (Brush?)Converter.ConvertFromString(hex);
                    if (brush != null)
                    {
                        if (brush.CanFreeze) brush.Freeze();
                        return brush;
                    }
                }
                catch { }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
