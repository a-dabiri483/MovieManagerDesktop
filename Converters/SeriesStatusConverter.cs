using System;
using System.Globalization;
using System.Windows.Data;

namespace MovieManagerDesktop.Converters
{
    public class SeriesStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                if (string.IsNullOrEmpty(status)) return "";
                switch (status.Trim().ToLowerInvariant())
                {
                    case "returning series":
                        return "در حال پخش";
                    case "ended":
                        return "تمام شده";
                    case "canceled":
                    case "cancelled":
                        return "کنسل شده";
                    case "in production":
                        return "در حال ساخت";
                    case "planned":
                        return "برنامه‌ریزی شده";
                    case "pilot":
                        return "قسمت آزمایشی";
                    case "currently airing":
                        return "در حال پخش";
                    case "finished airing":
                        return "تمام شده";
                    case "not yet aired":
                        return "پخش نشده";
                    default:
                        return status;
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SeriesStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status && !string.IsNullOrEmpty(status))
                switch (status.Trim().ToLowerInvariant())
                {
                    case "returning series":
                    case "currently airing":
                        return "#CC4CAF50"; // Green
                    case "ended":
                    case "finished airing":
                        return "#CCFF9800"; // Orange
                    case "canceled":
                    case "cancelled":
                        return "#CCFF5252"; // Red
                    case "planned":
                    case "not yet aired":
                        return "#CC2196F3"; // Blue
                }
            return "#CC00E5FF"; // Default Cyan
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SeasonNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name)
            {
                if (name.Contains("Specials", StringComparison.OrdinalIgnoreCase)) return "ویژه‌برنامه‌ها";
                return name.Replace("Season", "فصل", StringComparison.OrdinalIgnoreCase)
                           .Replace("Part", "فصل", StringComparison.OrdinalIgnoreCase);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
