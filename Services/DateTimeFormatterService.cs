using System;
using System.Globalization;

namespace MovieManagerDesktop.Services
{
    public static class DateTimeFormatterService
    {
        private static readonly string[] JalaliMonthNames = new[]
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        private static readonly string[] GregorianMonthNames = new[]
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public static bool IsJalali
        {
            get
            {
                var settings = SettingsManager.LoadSettings();
                return string.Equals(settings.DateFormatOverride, "jalali", StringComparison.OrdinalIgnoreCase) ||
                       string.IsNullOrEmpty(settings.DateFormatOverride) ||
                       string.Equals(settings.DateFormatOverride, "auto", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string GetJalaliMonthName(int month)
        {
            if (month >= 1 && month <= 12) return JalaliMonthNames[month - 1];
            return string.Empty;
        }

        public static string GetGregorianMonthName(int month)
        {
            if (month >= 1 && month <= 12) return GregorianMonthNames[month - 1];
            return string.Empty;
        }

        public static bool IsGregorianLeapYear(int year)
        {
            return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        }

        public static bool IsJalaliLeapYear(int year)
        {
            int[] a = { 0, 4, 8, 12, 16, 20, 24, 29, 33, 37, 41, 45, 49, 53, 57, 62, 66, 70, 74, 78, 82, 86, 90, 94, 99, 103, 107, 111, 115, 119, 124, 128 };
            int mod = year % 128;
            return Array.IndexOf(a, mod) >= 0;
        }

        public static (int Year, int Month, int Day) GregorianToJalali(int gYear, int gMonth, int gDay)
        {
            int gy = gYear - 1600;
            int gm = gMonth - 1;
            int gd = gDay - 1;

            int gDayNo = 365 * gy + ((gy + 3) / 4) - ((gy + 99) / 100) + ((gy + 399) / 400);

            for (int i = 0; i < gm; i++)
            {
                gDayNo += (i == 1) ? (IsGregorianLeapYear(gYear) ? 29 : 28) : ((i == 3 || i == 5 || i == 8 || i == 10) ? 30 : 31);
            }
            gDayNo += gd;

            int jDayNo = gDayNo - 79;
            int jNp = jDayNo / 12053;
            jDayNo %= 12053;

            int jy = 979 + 33 * jNp + 4 * (jDayNo / 1461);
            jDayNo %= 1461;

            if (jDayNo >= 366)
            {
                jy += (jDayNo - 1) / 365;
                jDayNo = (jDayNo - 1) % 365;
            }

            int jm = 0;
            for (int i = 0; i < 11 && jDayNo >= (i < 6 ? 31 : 30); i++)
            {
                jDayNo -= (i < 6 ? 31 : 30);
                jm = i + 1;
            }
            jm += 1;
            int jd = jDayNo + 1;

            return (jy, jm, jd);
        }

        public static (int Year, int Month, int Day) JalaliToGregorian(int jYear, int jMonth, int jDay)
        {
            int jy = jYear - 979;
            int jm = jMonth - 1;
            int jd = jDay - 1;

            int jDayNo = 365 * jy + (jy / 33) * 8 + ((jy % 33 + 3) / 4);

            for (int i = 0; i < jm; i++)
            {
                jDayNo += (i < 6 ? 31 : 30);
            }
            jDayNo += jd;

            int gDayNo = jDayNo + 79;
            int gy = 1600 + 400 * (gDayNo / 146097);
            gDayNo %= 146097;

            if (gDayNo >= 36525)
            {
                gDayNo--;
                gy += 100 * (gDayNo / 36524);
                gDayNo %= 36524;

                if (gDayNo >= 365)
                {
                    gDayNo++;
                }
            }

            gy += 4 * (gDayNo / 1461);
            gDayNo %= 1461;

            if (gDayNo >= 366)
            {
                gy += (gDayNo - 1) / 365;
                gDayNo = (gDayNo - 1) % 365;
            }

            int gm = 0;
            int[] gDaysInMonth = { 31, IsGregorianLeapYear(gy) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            for (int i = 0; i < 12 && gDayNo >= gDaysInMonth[i]; i++)
            {
                gDayNo -= gDaysInMonth[i];
                gm = i + 1;
            }
            gm += 1;
            int gd = gDayNo + 1;

            return (gy, gm, gd);
        }

        /// <summary>
        /// Formats any year string (e.g. "2024" or "1403" or "2024-05-12") based on calendar preference.
        /// Always outputs English digits.
        /// </summary>
        public static string FormatYear(string? yearOrDate)
        {
            if (string.IsNullOrWhiteSpace(yearOrDate) || yearOrDate == "0") return string.Empty;

            string raw = yearOrDate.Trim();
            if (raw.Length >= 4 && int.TryParse(raw.Substring(0, 4), out int y))
            {
                if (IsJalali)
                {
                    // If Gregorian (e.g. 2024), convert to Jalali (1403)
                    if (y > 1800)
                    {
                        return (y - 621).ToString(CultureInfo.InvariantCulture);
                    }
                    return y.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // If Gregorian is selected, return Gregorian (e.g. 2024)
                    if (y < 1700 && y > 1200)
                    {
                        return (y + 621).ToString(CultureInfo.InvariantCulture);
                    }
                    return y.ToString(CultureInfo.InvariantCulture);
                }
            }

            return raw;
        }

        /// <summary>
        /// Formats a full date string (e.g. "2024-10-15") into Jalali (e.g. "24 مهر 1403") or Gregorian ("Oct 15, 2024").
        /// Uses English digits.
        /// </summary>
        public static string FormatDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString)) return string.Empty;

            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return FormatDateTime(dt);
            }

            return dateString;
        }

        /// <summary>
        /// Formats a DateTime object into Jalali (e.g. "24 مهر 1403") or Gregorian ("Oct 15, 2024").
        /// Uses English digits.
        /// </summary>
        public static string FormatDateTime(DateTime dt)
        {
            if (IsJalali)
            {
                var (jYear, jMonth, jDay) = GregorianToJalali(dt.Year, dt.Month, dt.Day);
                string monthName = GetJalaliMonthName(jMonth);
                return $"{jDay} {monthName} {jYear}";
            }
            else
            {
                return dt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Formats date to standard YYYY/MM/DD format with English digits.
        /// </summary>
        public static string FormatShortDate(DateTime dt)
        {
            if (IsJalali)
            {
                var (jYear, jMonth, jDay) = GregorianToJalali(dt.Year, dt.Month, dt.Day);
                return $"{jYear:D4}/{jMonth:D2}/{jDay:D2}";
            }
            else
            {
                return dt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
        }
    }
}
