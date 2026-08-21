using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class GenreStatItem : ObservableObject
    {
        [ObservableProperty]
        private string _genreName = string.Empty;

        [ObservableProperty]
        private int _count;

        [ObservableProperty]
        private double _percentage;

        [ObservableProperty]
        private string _barColor = "#8854D0";
    }

    public partial class SimpleStatItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _count;

        [ObservableProperty]
        private double _percentage;

        [ObservableProperty]
        private string _color = "#3A86FF";
    }

    public partial class PersonStatItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private int _worksCount;
    }

    public class AnalyticsDataResult
    {
        public int MovCount { get; set; }
        public int SerCount { get; set; }
        public int EpCount { get; set; }
        public int FileCount { get; set; }
        public double MovPct { get; set; }
        public double SerPct { get; set; }
        public int FavCount { get; set; }
        public int WatchCount { get; set; }
        public int MovWatched { get; set; }
        public int EpWatched { get; set; }
        public int CompSeries { get; set; }
        public double OverallPct { get; set; }
        public string WatchTimeStr { get; set; } = "۰ ساعت";
        public string ArchiveDurationStr { get; set; } = "۰ ساعت";
        public string StorageStr { get; set; } = "۰ GB";
        public string AvgRatingStr { get; set; } = "۰.۰";
        public int TopRateCnt { get; set; }
        public List<GenreStatItem> TopGenresList { get; set; } = new();
        public List<SimpleStatItem> QualityList { get; set; } = new();
        public List<SimpleStatItem> DecadeList { get; set; } = new();
        public List<PersonStatItem> TopDirList { get; set; } = new();
        public List<PersonStatItem> TopActList { get; set; } = new();
    }

    public partial class AnalyticsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading = false;

        // 1. Counts
        [ObservableProperty]
        private int _totalMoviesCount;

        [ObservableProperty]
        private int _totalSeriesCount;

        [ObservableProperty]
        private int _totalEpisodesCount;

        [ObservableProperty]
        private int _totalFilesCount;

        // 2. Watched Stats
        [ObservableProperty]
        private int _totalMoviesWatched;

        [ObservableProperty]
        private int _totalEpisodesWatched;

        [ObservableProperty]
        private int _completedSeriesCount;

        [ObservableProperty]
        private double _overallWatchPercentage;

        // 3. Time & Size
        [ObservableProperty]
        private string _totalWatchTimeText = "۰ ساعت";

        [ObservableProperty]
        private string _totalArchiveDurationText = "۰ ساعت";

        [ObservableProperty]
        private string _totalStorageSize = "۰ GB";

        [ObservableProperty]
        private string _averageRating = "۰.۰";

        [ObservableProperty]
        private int _topRatedCount; // Ratings >= 8.0

        // 4. Ratios
        [ObservableProperty]
        private double _moviePercentage = 50;

        [ObservableProperty]
        private double _seriesPercentage = 50;

        [ObservableProperty]
        private int _totalFavoritesCount;

        [ObservableProperty]
        private int _totalWatchlistCount;

        // 5. Collections
        public ObservableCollection<GenreStatItem> TopGenres { get; } = new();
        public ObservableCollection<SimpleStatItem> QualityBreakdown { get; } = new();
        public ObservableCollection<SimpleStatItem> DecadeBreakdown { get; } = new();
        public ObservableCollection<PersonStatItem> TopDirectors { get; } = new();
        public ObservableCollection<PersonStatItem> TopActors { get; } = new();

        private readonly string[] _genreColors = { "#8854D0", "#FF4757", "#2ED573", "#FFA502", "#1E90FF", "#20BF6B", "#EB3B5A", "#4B7BEC", "#A55EEA", "#FD9644" };
        private readonly string[] _qualityColors = { "#00D2D3", "#3A86FF", "#54A0FF", "#5F27CD", "#FF9F43" };
        private readonly string[] _decadeColors = { "#10AC84", "#01A3A4", "#2E86DE", "#341F97", "#EE5253", "#FFA502" };

        public AnalyticsViewModel()
        {
            _ = LoadAnalyticsAsync();

            WeakReferenceMessenger.Default.Register<MediaUpdatedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = LoadAnalyticsAsync();
                });
            });
        }

        [RelayCommand]
        public async Task RefreshAnalyticsAsync()
        {
            await LoadAnalyticsAsync();
            ToastService.Instance.ShowSuccess("آمار و تحلیل‌های آرشیو با موفقیت به‌روزرسانی شد.");
        }

        public async Task LoadAnalyticsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var result = await Task.Run<AnalyticsDataResult?>(() =>
                {
                    using var db = new AppDbContext();
                    if (!db.Database.CanConnect()) return null;

                    var allFiles = db.VideoFiles.AsNoTracking().ToList();
                    if (allFiles.Count == 0) return null;

                    var res = new AnalyticsDataResult();

                    // 1. Group Distinct Movies and Series exactly like MoviesViewModel
                    var allDistinct = allFiles
                        .GroupBy(v => new
                        {
                            Title = (v.FormattedTitle ?? v.FileName ?? "ناشناس").Trim().ToLowerInvariant(),
                            Type = string.Equals(v.MediaType, "Series", StringComparison.OrdinalIgnoreCase) || v.Season != null ? "Series" : "Movie"
                        })
                        .ToList();

                    res.MovCount = allDistinct.Count(g => g.Key.Type == "Movie");
                    res.SerCount = allDistinct.Count(g => g.Key.Type == "Series");
                    res.EpCount = allFiles.Count(f => string.Equals(f.MediaType, "Series", StringComparison.OrdinalIgnoreCase) || f.Season != null);
                    res.FileCount = allFiles.Count;

                    int totalDistinctMedia = res.MovCount + res.SerCount;
                    if (totalDistinctMedia > 0)
                    {
                        res.MovPct = Math.Round((double)res.MovCount / totalDistinctMedia * 100, 1);
                        res.SerPct = Math.Round((double)res.SerCount / totalDistinctMedia * 100, 1);
                    }
                    else
                    {
                        res.MovPct = 50;
                        res.SerPct = 50;
                    }

                    // 2. Favorites & Watchlist
                    res.FavCount = allDistinct.Count(g => g.Any(f => f.IsFavorite));
                    res.WatchCount = allDistinct.Count(g => g.Any(f => f.IsWatchlist));

                    // 3. Watched stats
                    res.MovWatched = allDistinct.Count(g => g.Key.Type == "Movie" && g.Any(m => m.IsWatched || m.WatchProgressPercent >= 85));
                    res.EpWatched = allFiles.Count(f => (string.Equals(f.MediaType, "Series", StringComparison.OrdinalIgnoreCase) || f.Season != null) && (f.IsWatched || f.WatchProgressPercent >= 85));
                    res.CompSeries = allDistinct.Count(g => g.Key.Type == "Series" && g.All(ep => ep.IsWatched || ep.WatchProgressPercent >= 85));

                    int totalWatchUnits = res.MovCount + res.EpCount;
                    int totalWatchedUnits = res.MovWatched + res.EpWatched;
                    res.OverallPct = totalWatchUnits > 0 ? Math.Round((double)totalWatchedUnits / totalWatchUnits * 100, 1) : 0;

                    // 4. Real Time & Real Size
                    long watchedSeconds = 0;
                    long totalArchiveSeconds = 0;
                    long totalBytes = 0;

                    foreach (var f in allFiles)
                    {
                        bool isMovie = !string.Equals(f.MediaType, "Series", StringComparison.OrdinalIgnoreCase) && f.Season == null;
                        long fileDuration = f.TotalDurationSeconds > 0
                            ? f.TotalDurationSeconds
                            : (isMovie ? 105 * 60 : 45 * 60);

                        totalArchiveSeconds += fileDuration;

                        if (f.WatchProgressSeconds > 0)
                        {
                            watchedSeconds += f.WatchProgressSeconds;
                        }
                        else if (f.IsWatched || f.WatchProgressPercent >= 85)
                        {
                            watchedSeconds += fileDuration;
                        }
                        else if (f.WatchProgressPercent > 0)
                        {
                            watchedSeconds += (long)(fileDuration * (f.WatchProgressPercent / 100.0));
                        }

                        // Storage calculation with on-disk fallback
                        if (f.FileSizeBytes > 0)
                        {
                            totalBytes += f.FileSizeBytes;
                        }
                        else if (!string.IsNullOrWhiteSpace(f.FilePath) && File.Exists(f.FilePath))
                        {
                            try
                            {
                                long size = new FileInfo(f.FilePath).Length;
                                totalBytes += size;
                            }
                            catch { }
                        }
                    }

                    res.WatchTimeStr = FormatSecondsToPersianDuration(watchedSeconds);
                    res.ArchiveDurationStr = FormatSecondsToPersianDuration(totalArchiveSeconds);

                    res.StorageStr = "۰ GB";
                    if (totalBytes > 0)
                    {
                        double gigabytes = (double)totalBytes / (1024.0 * 1024.0 * 1024.0);
                        if (gigabytes >= 1024.0)
                        {
                            double terabytes = gigabytes / 1024.0;
                            res.StorageStr = $"{terabytes:0.##} TB ({gigabytes:N0} GB)";
                        }
                        else
                        {
                            res.StorageStr = $"{gigabytes:0.#} GB";
                        }
                    }

                    // 5. Ratings & Masterpieces (100% Null-safe)
                    var ratedDistinct = allDistinct
                        .Select(g => g.Where(v => v.Rating.HasValue && v.Rating.Value > 0).Select(v => (double)v.Rating!.Value).DefaultIfEmpty(0.0).Max())
                        .Where(r => r > 0)
                        .ToList();

                    res.AvgRatingStr = "۰.۰";
                    res.TopRateCnt = 0;
                    if (ratedDistinct.Any())
                    {
                        res.AvgRatingStr = ratedDistinct.Average().ToString("0.0");
                        res.TopRateCnt = ratedDistinct.Count(r => r >= 8.0);
                    }

                    // 6. Genres Breakdown (Based on Distinct Titles)
                    var genreMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in allDistinct)
                    {
                        var first = g.First();
                        if (!string.IsNullOrWhiteSpace(first.Genres) && first.Genres != "N/A")
                        {
                            var split = first.Genres.Split(new[] { ',', '،', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var rawG in split)
                            {
                                var gen = GenreTranslatorService.Translate(rawG.Trim());
                                if (!string.IsNullOrWhiteSpace(gen))
                                {
                                    genreMap[gen] = genreMap.GetValueOrDefault(gen, 0) + 1;
                                }
                            }
                        }
                    }

                    int genreColorIdx = 0;
                    int maxGenreCount = genreMap.Values.DefaultIfEmpty(1).Max();
                    foreach (var kvp in genreMap.OrderByDescending(x => x.Value).Take(10))
                    {
                        double pct = Math.Round((double)kvp.Value / maxGenreCount * 100, 1);
                        res.TopGenresList.Add(new GenreStatItem
                        {
                            GenreName = kvp.Key,
                            Count = kvp.Value,
                            Percentage = pct,
                            BarColor = _genreColors[genreColorIdx % _genreColors.Length]
                        });
                        genreColorIdx++;
                    }

                    // 7. Quality / Resolution Breakdown
                    var qualityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in allFiles)
                    {
                        string qr = ExtractNormalizedResolution(f.Resolution, f.Quality);
                        qualityMap[qr] = qualityMap.GetValueOrDefault(qr, 0) + 1;
                    }

                    int qColorIdx = 0;
                    foreach (var kvp in qualityMap.OrderByDescending(x => x.Value))
                    {
                        double pct = Math.Round((double)kvp.Value / allFiles.Count * 100, 1);
                        res.QualityList.Add(new SimpleStatItem
                        {
                            Name = kvp.Key,
                            Count = kvp.Value,
                            Percentage = pct,
                            Color = _qualityColors[qColorIdx % _qualityColors.Length]
                        });
                        qColorIdx++;
                    }

                    // 8. Decades Breakdown (Based on Distinct Titles)
                    var decadeMap = new Dictionary<string, int>();
                    foreach (var g in allDistinct)
                    {
                        var first = g.First();
                        string? yearVal = first.Year;
                        if (string.IsNullOrWhiteSpace(yearVal) && first.FirstAirDate.HasValue)
                        {
                            yearVal = first.FirstAirDate.Value.Year.ToString();
                        }

                        string decade = ExtractDecade(yearVal);
                        decadeMap[decade] = decadeMap.GetValueOrDefault(decade, 0) + 1;
                    }

                    int dColorIdx = 0;
                    foreach (var kvp in decadeMap.OrderByDescending(x => x.Key))
                    {
                        double pct = Math.Round((double)kvp.Value / allDistinct.Count * 100, 1);
                        res.DecadeList.Add(new SimpleStatItem
                        {
                            Name = kvp.Key,
                            Count = kvp.Value,
                            Percentage = pct,
                            Color = _decadeColors[dColorIdx % _decadeColors.Length]
                        });
                        dColorIdx++;
                    }

                    // 9. Top Directors & Actors (Based on Distinct Titles)
                    var directorMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var actorMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var g in allDistinct)
                    {
                        var first = g.First();
                        if (!string.IsNullOrWhiteSpace(first.Director) && first.Director != "N/A")
                        {
                            foreach (var d in first.Director.Split(new[] { ',', '،', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var trimmed = d.Trim();
                                if (trimmed.Length > 2)
                                    directorMap[trimmed] = directorMap.GetValueOrDefault(trimmed, 0) + 1;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(first.Actors) && first.Actors != "N/A")
                        {
                            foreach (var a in first.Actors.Split(new[] { ',', '،', '|', ';' }, StringSplitOptions.RemoveEmptyEntries).Take(4))
                            {
                                var trimmed = a.Trim();
                                if (trimmed.Length > 2)
                                    actorMap[trimmed] = actorMap.GetValueOrDefault(trimmed, 0) + 1;
                            }
                        }
                    }

                    res.TopDirList = directorMap.OrderByDescending(x => x.Value).Take(5)
                        .Select(kvp => new PersonStatItem { Name = kvp.Key, Role = "کارگردان", WorksCount = kvp.Value }).ToList();

                    res.TopActList = actorMap.OrderByDescending(x => x.Value).Take(5)
                        .Select(kvp => new PersonStatItem { Name = kvp.Key, Role = "بازیگر", WorksCount = kvp.Value }).ToList();

                    return res;
                });

                if (result != null)
                {
                    TotalMoviesCount = result.MovCount;
                    TotalSeriesCount = result.SerCount;
                    TotalEpisodesCount = result.EpCount;
                    TotalFilesCount = result.FileCount;

                    MoviePercentage = result.MovPct;
                    SeriesPercentage = result.SerPct;
                    TotalFavoritesCount = result.FavCount;
                    TotalWatchlistCount = result.WatchCount;

                    TotalMoviesWatched = result.MovWatched;
                    TotalEpisodesWatched = result.EpWatched;
                    CompletedSeriesCount = result.CompSeries;
                    OverallWatchPercentage = result.OverallPct;

                    TotalWatchTimeText = result.WatchTimeStr;
                    TotalArchiveDurationText = result.ArchiveDurationStr;
                    TotalStorageSize = result.StorageStr;
                    AverageRating = result.AvgRatingStr;
                    TopRatedCount = result.TopRateCnt;

                    TopGenres.Clear();
                    foreach (var g in result.TopGenresList) TopGenres.Add(g);

                    QualityBreakdown.Clear();
                    foreach (var q in result.QualityList) QualityBreakdown.Add(q);

                    DecadeBreakdown.Clear();
                    foreach (var d in result.DecadeList) DecadeBreakdown.Add(d);

                    TopDirectors.Clear();
                    foreach (var dir in result.TopDirList) TopDirectors.Add(dir);

                    TopActors.Clear();
                    foreach (var act in result.TopActList) TopActors.Add(act);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error calculating analytics", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string FormatSecondsToPersianDuration(long totalSeconds)
        {
            if (totalSeconds <= 0) return "۰ ساعت";

            long totalMinutes = totalSeconds / 60;
            if (totalMinutes < 60)
            {
                return $"{totalMinutes} دقیقه";
            }
            if (totalMinutes < 1440)
            {
                long hours = totalMinutes / 60;
                long mins = totalMinutes % 60;
                return mins > 0 ? $"{hours} ساعت و {mins} دقیقه" : $"{hours} ساعت";
            }
            int days = (int)(totalMinutes / 1440);
            int remainingHours = (int)((totalMinutes % 1440) / 60);
            return remainingHours > 0 ? $"{days} روز و {remainingHours} ساعت" : $"{days} روز کامل";
        }

        private static string ExtractNormalizedResolution(string? resolution, string? quality)
        {
            string combined = $"{resolution} {quality}".ToLowerInvariant();
            if (combined.Contains("2160") || combined.Contains("4k") || combined.Contains("uhd")) return "4K Ultra HD";
            if (combined.Contains("1080") || combined.Contains("fhd")) return "1080p Full HD";
            if (combined.Contains("720") || combined.Contains("hd")) return "720p HD";
            if (combined.Contains("480") || combined.Contains("sd")) return "480p SD";
            return "کیفیت استاندارد";
        }

        private static string ExtractDecade(string? yearStr)
        {
            if (int.TryParse(yearStr?.Trim(), out int year) && year > 1900 && year < 2100)
            {
                if (year >= 2020) return "دهه ۲۰۲۰ (۲۰۲۰ تا اکنون)";
                if (year >= 2010) return "دهه ۲۰۱۰ (۲۰۱۰ تا ۲۰۱۹)";
                if (year >= 2000) return "دهه ۲۰۰۰ (۲۰۰۰ تا ۲۰۰۹)";
                if (year >= 1990) return "دهه ۹۰ (۱۹۹۰ تا ۱۹۹۹)";
                if (year >= 1980) return "دهه ۸۰ (۱۹۸۰ تا ۱۹۸۹)";
                return "کلاسیک (قبل از ۱۹۸۰)";
            }
            return "نامشخص";
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
