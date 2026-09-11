using System;

namespace MovieManagerDesktop.Models
{
    public class BackupInspectionResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string FormatType { get; set; } = "بسته پشتیبان";
        public string BackupVersion { get; set; } = "2.0";
        public DateTime? CreatedAt { get; set; }
        public string FormattedDate { get; set; } = "نامشخص";
        
        public int MoviesCount { get; set; }
        public int SeriesCount { get; set; }
        public int TotalVideosCount { get; set; }
        public int TvSeasonsCount { get; set; }
        public int TvEpisodesCount { get; set; }
        public int ImagesCount { get; set; }
        public bool HasSettings { get; set; }
        public bool HasWatchProgress { get; set; }
        public bool IsValid { get; set; } = true;
        public string? ErrorMessage { get; set; }

        public string HasSettingsText => HasSettings ? "شامل تنظیمات برنامه" : "فقط اطلاعات فیلم و سریال";
        public string WatchProgressText => HasWatchProgress ? "شامل سوابق تماشا و ادامه پخش" : "بدون سوابق تماشا";
        public string ImagesText => ImagesCount > 0 ? $"{ImagesCount:N0} تصویر کاور و پس‌زمینه" : "بدون فایل تصویر";
    }
}
