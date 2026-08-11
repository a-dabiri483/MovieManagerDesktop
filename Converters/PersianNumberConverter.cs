using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.RegularExpressions;

namespace MovieManagerDesktop.Converters
{
    public class PersianNumberConverter : IValueConverter
    {
        private static readonly string[] EnglishDigits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        private static readonly string[] PersianDigits = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            
            string strValue = value.ToString();
            
            for (int i = 0; i < 10; i++)
            {
                strValue = strValue.Replace(EnglishDigits[i], PersianDigits[i]);
            }
            
            return strValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
