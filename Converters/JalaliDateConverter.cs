using System;
using System.Globalization;
using System.Windows.Data;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Converters
{
    public class JalaliDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                if (dt == DateTime.MinValue || dt.Year < 1800) return "نامشخص";
                
                string param = parameter as string;
                if (param == "Short")
                {
                    return DateTimeFormatterService.FormatShortDate(dt);
                }
                
                return DateTimeFormatterService.FormatDateTime(dt);
            }
            
            if (value is string strDate && !string.IsNullOrWhiteSpace(strDate))
            {
                if (DateTime.TryParse(strDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDt))
                {
                    if (parsedDt == DateTime.MinValue || parsedDt.Year < 1800) return "نامشخص";
                    
                    string param = parameter as string;
                    if (param == "Short")
                    {
                        return DateTimeFormatterService.FormatShortDate(parsedDt);
                    }
                    
                    return DateTimeFormatterService.FormatDateTime(parsedDt);
                }
                return DateTimeFormatterService.FormatDate(strDate);
            }
            
            return value ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
