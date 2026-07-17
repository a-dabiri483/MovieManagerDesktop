using System;
using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public static class SmartGroupingService
    {
        // آستانه شباهت - عناوین با شباهت بالای این درصد در یک گروه قرار می‌گیرند
        private const int SimilarityThreshold = 85;

        /// <summary>
        /// گروه‌بندی هوشمند فایل‌ها بر اساس شباهت نام
        /// </summary>
        public static List<List<VideoFile>> SmartGroupFiles(List<VideoFile> files)
        {
            if (files == null || !files.Any())
                return new List<List<VideoFile>>();

            // گروه‌ها: هر گروه یک لیست از فایل‌ها + عنوان نرمال‌شده
            var groups = new List<GroupInfo>();

            foreach (var file in files)
            {
                // نرمال‌سازی عنوان برای مقایسه
                string normalizedTitle = NormalizeTitle(file.FormattedTitle);
                
                // استخراج تمام اعداد از عنوان برای جلوگیری از ادغام قسمت 1 و 2
                var numbersInTitle = ExtractNumbers(normalizedTitle);
                
                // جستجو برای گروه موجود با شباهت بالا
                GroupInfo bestMatch = null;
                int bestScore = 0;

                foreach (var group in groups)
                {
                    // فقط فایل‌های هم‌نوع (Series با Series، Movie با Movie)
                    if (group.MediaType != file.MediaType)
                        continue;
                        
                    // برای فیلم‌ها، سال ساخت هم باید یکی باشد (اگر هر دو سال دارند)
                    if (file.MediaType == "Movie" && !string.IsNullOrEmpty(file.Year) && !string.IsNullOrEmpty(group.Year))
                    {
                        if (file.Year != group.Year)
                            continue;
                    }

                    // مقایسه دقیق
                    if (normalizedTitle == group.NormalizedTitle)
                    {
                        bestMatch = group;
                        break;
                    }
                    
                    // اگر اعداد داخل نام‌ها متفاوت باشد، به هیچ وجه نباید با هم ادغام شوند (مثل پارت 1 و 2)
                    if (!numbersInTitle.SequenceEqual(group.NumbersInTitle))
                        continue;

                    // مقایسه فازی
                    int score = Fuzz.Ratio(normalizedTitle, group.NormalizedTitle);
                    
                    // برای فیلم‌ها سخت‌گیری بیشتر چون نام‌های مشابه زیادی دارند
                    int threshold = file.MediaType == "Movie" ? 90 : SimilarityThreshold;
                    
                    if (score > bestScore && score >= threshold)
                    {
                        bestScore = score;
                        bestMatch = group;
                    }
                }

                if (bestMatch != null)
                {
                    // اضافه کردن به گروه موجود
                    bestMatch.Files.Add(file);
                }
                else
                {
                    // ساخت گروه جدید
                    groups.Add(new GroupInfo
                    {
                        NormalizedTitle = normalizedTitle,
                        NumbersInTitle = numbersInTitle,
                        MediaType = file.MediaType,
                        Year = file.Year,
                        Files = new List<VideoFile> { file }
                    });
                }
            }

            return groups.Select(g => g.Files).ToList();
        }

        private static List<int> ExtractNumbers(string text)
        {
            var numbers = new List<int>();
            if (string.IsNullOrWhiteSpace(text)) return numbers;
            
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\d+");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (int.TryParse(match.Value, out int num))
                {
                    numbers.Add(num);
                }
            }
            return numbers;
        }

        /// <summary>
        /// نرمال‌سازی عنوان برای مقایسه بهتر
        /// حذف اسپیس‌های اضافی، تبدیل به حروف کوچک، حذف کاراکترهای خاص
        /// </summary>
        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            return title
                .ToLowerInvariant()
                .Replace(".", " ")
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("[", " ")
                .Replace("]", " ")
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace(":", " ")
                .Replace("&", " and ")
                .Replace("'", "")
                .Replace("  ", " ")  // حذف اسپیس‌های دوتایی
                .Replace("  ", " ")  // مجدداً برای اطمینان
                .Replace("   ", " ") // حذف اسپیس‌های سه‌تایی
                .Trim();
        }

        /// <summary>
        /// اطلاعات یک گروه
        /// </summary>
        private class GroupInfo
        {
            public string NormalizedTitle { get; set; }
            public List<int> NumbersInTitle { get; set; }
            public string MediaType { get; set; }
            public string Year { get; set; }
            public List<VideoFile> Files { get; set; }
        }
    }
}
