using System;
using System.Collections.Generic;
using System.Linq;

namespace MovieManagerDesktop.Services
{
    public static class GenreTranslatorService
    {
        private static readonly Dictionary<int, string> TmdbGenreIdMap = new()
        {
            { 28, "Action" },
            { 12, "Adventure" },
            { 16, "Animation" },
            { 35, "Comedy" },
            { 80, "Crime" },
            { 99, "Documentary" },
            { 18, "Drama" },
            { 10751, "Family" },
            { 14, "Fantasy" },
            { 36, "History" },
            { 27, "Horror" },
            { 10402, "Music" },
            { 9648, "Mystery" },
            { 10749, "Romance" },
            { 878, "Science Fiction" },
            { 10770, "TV Movie" },
            { 53, "Thriller" },
            { 10752, "War" },
            { 37, "Western" },
            { 10759, "Action & Adventure" },
            { 10762, "Kids" },
            { 10763, "News" },
            { 10764, "Reality" },
            { 10765, "Sci-Fi & Fantasy" },
            { 10766, "Soap" },
            { 10767, "Talk" },
            { 10768, "War & Politics" }
        };

        private static readonly Dictionary<string, string> EnglishToPersianMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Action", "اکشن" },
            { "Adventure", "ماجراجویی" },
            { "Animation", "انیمیشن" },
            { "Comedy", "کمدی" },
            { "Crime", "جنایی" },
            { "Documentary", "مستند" },
            { "Drama", "درام" },
            { "Family", "خانوادگی" },
            { "Fantasy", "فانتزی" },
            { "History", "تاریخی" },
            { "Horror", "ترسناک" },
            { "Music", "موسیقی" },
            { "Mystery", "معمایی" },
            { "Romance", "عاشقانه" },
            { "Science Fiction", "علمی تخیلی" },
            { "Sci-Fi", "علمی تخیلی" },
            { "TV Movie", "تله فیلم" },
            { "Thriller", "هیجان‌انگیز" },
            { "War", "جنگی" },
            { "Western", "وسترن" },
            { "Anime", "انیمه" },
            { "Kids", "کودک" },
            { "News", "اخبار" },
            { "Reality", "ریلیتی شو" },
            { "Soap", "سریال آبکی" },
            { "Talk", "گفتگو محور" },
            { "War & Politics", "جنگی و سیاسی" },
            { "Action & Adventure", "اکشن و ماجراجویی" },
            { "Sci-Fi & Fantasy", "علمی تخیلی و فانتزی" },
            { "Biography", "زندگینامه" },
            { "Sport", "ورزشی" },
            { "Musical", "موزیکال" },
            { "Short", "فیلم کوتاه" }
        };

        private static readonly Dictionary<string, string> PersianToEnglishMap = new(StringComparer.OrdinalIgnoreCase);

        static GenreTranslatorService()
        {
            foreach (var kvp in EnglishToPersianMap)
            {
                if (!PersianToEnglishMap.ContainsKey(kvp.Value))
                {
                    PersianToEnglishMap[kvp.Value] = kvp.Key;
                }
            }
        }

        public static string ResolveRawGenre(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre)) return string.Empty;
            string trimmed = genre.Trim();

            // If it is a numeric TMDB ID (e.g. "28" or "12")
            if (int.TryParse(trimmed, out int id) && TmdbGenreIdMap.TryGetValue(id, out var standardName))
            {
                return standardName;
            }

            return trimmed;
        }

        public static string Translate(string? genre)
        {
            if (string.IsNullOrWhiteSpace(genre)) return string.Empty;

            var settings = SettingsManager.LoadSettings();
            string lang = settings.GenreLanguageOverride?.ToLowerInvariant() ?? "auto";

            // If "auto", determine based on TmdbLanguage or default to Persian
            if (lang == "auto")
            {
                bool isEn = string.Equals(settings.TmdbLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
                lang = isEn ? "en" : "fa";
            }

            if (lang == "en")
            {
                return TranslateToEnglish(genre);
            }
            else
            {
                return TranslateToPersian(genre);
            }
        }

        public static string TranslateToPersian(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre)) return string.Empty;
            string resolved = ResolveRawGenre(genre);

            if (EnglishToPersianMap.TryGetValue(resolved, out var fa))
            {
                return fa;
            }

            return resolved;
        }

        public static string TranslateToEnglish(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre)) return string.Empty;
            string resolved = ResolveRawGenre(genre);

            if (PersianToEnglishMap.TryGetValue(resolved, out var en))
            {
                return en;
            }

            if (EnglishToPersianMap.ContainsKey(resolved))
            {
                return resolved;
            }

            return resolved;
        }

        public static string TranslateList(string? genresCsv)
        {
            if (string.IsNullOrWhiteSpace(genresCsv)) return string.Empty;

            var parts = genresCsv.Split(new[] { ',', '،', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(g => g.Trim())
                                 .Where(g => !string.IsNullOrEmpty(g));

            var translated = parts.Select(Translate).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct();
            return string.Join("، ", translated);
        }

        public static bool MatchesGenre(string? mediaGenresCsv, string? selectedGenre)
        {
            if (string.IsNullOrWhiteSpace(selectedGenre) || selectedGenre == "همه ژانرها" || selectedGenre == "All Genres")
                return true;

            if (string.IsNullOrWhiteSpace(mediaGenresCsv))
                return false;

            string targetPersian = TranslateToPersian(selectedGenre);
            string targetEnglish = TranslateToEnglish(selectedGenre);

            var parts = mediaGenresCsv.Split(new[] { ',', '،', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(g => g.Trim())
                                      .Where(g => !string.IsNullOrEmpty(g));

            foreach (var p in parts)
            {
                if (string.Equals(p, selectedGenre, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(TranslateToPersian(p), targetPersian, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(TranslateToEnglish(p), targetEnglish, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
