using System;
using System.Globalization;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class PersianNumberConverter : IValueConverter
    {
        private static readonly string[] PersianDigits = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };
        private static readonly string[] ArabicDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
        private static readonly string[] EnglishDigits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            
            string strValue = value.ToString() ?? string.Empty;
            
            // Convert any accidental Persian/Arabic digits to clean English digits
            for (int i = 0; i < 10; i++)
            {
                strValue = strValue.Replace(PersianDigits[i], EnglishDigits[i])
                                   .Replace(ArabicDigits[i], EnglishDigits[i]);
            }
            
            return strValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
