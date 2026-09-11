using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace MovieManagerDesktop.Models
{
    public partial class DataCleanupOptions : ObservableObject
    {
        [ObservableProperty]
        private bool _cleanDatabase = true;

        [ObservableProperty]
        private long _databaseSizeBytes;

        [ObservableProperty]
        private int _databaseItemsCount;

        [ObservableProperty]
        private bool _cleanImages = true;

        [ObservableProperty]
        private long _imagesSizeBytes;

        [ObservableProperty]
        private int _imagesCount;

        [ObservableProperty]
        private bool _cleanImageCache = true;

        [ObservableProperty]
        private long _imageCacheSizeBytes;

        [ObservableProperty]
        private int _imageCacheCount;

        [ObservableProperty]
        private bool _cleanWatchHistory = false;

        [ObservableProperty]
        private long _watchHistorySizeBytes;

        [ObservableProperty]
        private bool _cleanLogs = false;

        [ObservableProperty]
        private long _logsSizeBytes;

        public string DatabaseSizeFormatted => FormatSize(DatabaseSizeBytes);
        public string ImagesSizeFormatted => FormatSize(ImagesSizeBytes);
        public string ImageCacheSizeFormatted => FormatSize(ImageCacheSizeBytes);
        public string WatchHistorySizeFormatted => FormatSize(WatchHistorySizeBytes);
        public string LogsSizeFormatted => FormatSize(LogsSizeBytes);

        public long TotalFreeableBytes
        {
            get
            {
                long total = 0;
                if (CleanDatabase) total += DatabaseSizeBytes;
                if (CleanImages) total += ImagesSizeBytes;
                if (CleanImageCache) total += ImageCacheSizeBytes;
                if (CleanWatchHistory) total += WatchHistorySizeBytes;
                if (CleanLogs) total += LogsSizeBytes;
                return total;
            }
        }

        public string TotalFreeableFormatted => FormatSize(TotalFreeableBytes);

        public bool HasAnySelected => CleanDatabase || CleanImages || CleanImageCache || CleanWatchHistory || CleanLogs;

        partial void OnCleanDatabaseChanged(bool value) => NotifyTotalsChanged();
        partial void OnCleanImagesChanged(bool value) => NotifyTotalsChanged();
        partial void OnCleanImageCacheChanged(bool value) => NotifyTotalsChanged();
        partial void OnCleanWatchHistoryChanged(bool value) => NotifyTotalsChanged();
        partial void OnCleanLogsChanged(bool value) => NotifyTotalsChanged();

        private void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalFreeableBytes));
            OnPropertyChanged(nameof(TotalFreeableFormatted));
            OnPropertyChanged(nameof(HasAnySelected));
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "۰ بایت";
            if (bytes < 1024) return $"{bytes} بایت";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} کیلوبایت";
            if (bytes < 1024L * 1024L * 1024L) return $"{bytes / (1024.0 * 1024.0):F1} مگابایت";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} گیگابایت";
        }
    }
}
