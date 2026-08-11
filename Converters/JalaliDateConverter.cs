using System;
using System.Globalization;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class JalaliDateConverter : IValueConverter
    {
        private static readonly string[] PersianMonths = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                if (dt == DateTime.MinValue || dt.Year < 1900) return "نامشخص";
                
                var pc = new PersianCalendar();
                int year = pc.GetYear(dt);
                int month = pc.GetMonth(dt);
                int day = pc.GetDayOfMonth(dt);
                
                string param = parameter as string;
                if (param == "Short")
                {
                    return $"{year}/{month:D2}/{day:D2}";
                }
                
                return $"{day} {PersianMonths[month]} {year}";
            }
            
            if (value is string strDate && DateTime.TryParse(strDate, out DateTime parsedDt))
            {
                if (parsedDt == DateTime.MinValue || parsedDt.Year < 1900) return "نامشخص";
                
                var pc = new PersianCalendar();
                int year = pc.GetYear(parsedDt);
                int month = pc.GetMonth(parsedDt);
                int day = pc.GetDayOfMonth(parsedDt);
                
                string param = parameter as string;
                if (param == "Short")
                {
                    return $"{year}/{month:D2}/{day:D2}";
                }
                
                return $"{day} {PersianMonths[month]} {year}";
            }
            
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
