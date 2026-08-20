using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    public partial class AnalyticsViewModel : ObservableObject
    {
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
        private readonly string[] _decadeColors = { "#10AC84", "#01A3A4", "#2E86DE", "#341F97", "#EE5253" };

        public AnalyticsViewModel()
        {
            LoadAnalytics();
        }

        public void LoadAnalytics()
        {
            try
            {
                using var db = new AppDbContext();
                if (!db.Database.CanConnect()) return;

                var allFiles = db.VideoFiles.AsNoTracking().ToList();
                if (allFiles.Count == 0) return;

                TotalFilesCount = allFiles.Count;

                // Group by Unique Title and Type
                var movieFiles = allFiles.Where(f => (f.MediaType ?? "Movie").Equals("Movie", StringComparison.OrdinalIgnoreCase)).ToList();
                var seriesFiles = allFiles.Where(f => (f.MediaType ?? "Movie").Equals("Series", StringComparison.OrdinalIgnoreCase)).ToList();

                var groupedMovies = movieFiles
                    .GroupBy(v => (v.FormattedTitle ?? v.FileName ?? "ناشناس").Trim().ToLowerInvariant())
                    .ToList();

                var groupedSeries = seriesFiles
                    .GroupBy(v => (v.FormattedTitle ?? v.FileName ?? "ناشناس").Trim().ToLowerInvariant())
                    .ToList();

                TotalMoviesCount = groupedMovies.Count;
                TotalSeriesCount = groupedSeries.Count;
                TotalEpisodesCount = seriesFiles.Count;

                int totalUniqueMedia = TotalMoviesCount + TotalSeriesCount;
                if (totalUniqueMedia > 0)
                {
                    MoviePercentage = Math.Round((double)TotalMoviesCount / totalUniqueMedia * 100, 1);
                    SeriesPercentage = Math.Round((double)TotalSeriesCount / totalUniqueMedia * 100, 1);
                }

                // Favorites & Watchlist
                TotalFavoritesCount = allFiles.Count(f => f.IsFavorite);
                TotalWatchlistCount = allFiles.Count(f => f.IsWatchlist);

                // Watched calculations (considering IsWatched OR WatchProgressPercent >= 85)
                TotalMoviesWatched = groupedMovies.Count(g => g.Any(m => m.IsWatched || m.WatchProgressPercent >= 85));
                TotalEpisodesWatched = seriesFiles.Count(e => e.IsWatched || e.WatchProgressPercent >= 85);

                // Completed Series count (series where all episodes in library are watched)
                int completedSeries = 0;
                foreach (var sg in groupedSeries)
                {
                    if (sg.Any() && sg.All(ep => ep.IsWatched || ep.WatchProgressPercent >= 85))
                    {
                        completedSeries++;
                    }
                }
                CompletedSeriesCount = completedSeries;

                // Overall archive watch completion percentage
                int totalWatchableUnits = TotalMoviesCount + TotalEpisodesCount;
                int totalWatchedUnits = TotalMoviesWatched + TotalEpisodesWatched;
                OverallWatchPercentage = totalWatchableUnits > 0
                    ? Math.Round((double)totalWatchedUnits / totalWatchableUnits * 100, 1)
                    : 0;

                // Watch time calculation (accurate seconds calculation)
                long watchedSeconds = 0;
                long totalArchiveSeconds = 0;

                foreach (var f in allFiles)
                {
                    bool isMovie = (f.MediaType ?? "Movie").Equals("Movie", StringComparison.OrdinalIgnoreCase);
                    long fileDuration = f.TotalDurationSeconds > 0
                        ? f.TotalDurationSeconds
                        : (isMovie ? 110 * 60 : 45 * 60);

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
                }

                TotalWatchTimeText = FormatSecondsToPersianDuration(watchedSeconds);
                TotalArchiveDurationText = FormatSecondsToPersianDuration(totalArchiveSeconds);

                // Ratings
                var validRatings = allFiles.Where(f => f.Rating.HasValue && f.Rating.Value > 0).Select(f => f.Rating.Value).ToList();
                if (validRatings.Any())
                {
                    AverageRating = validRatings.Average().ToString("0.0");
                    TopRatedCount = groupedMovies.Count(g => g.Any(m => m.Rating >= 8.0)) + groupedSeries.Count(g => g.Any(s => s.Rating >= 8.0));
                }

                // Storage Size
                long totalBytes = allFiles.Sum(f => f.FileSizeBytes);
                if (totalBytes > 0)
                {
                    double gigabytes = (double)totalBytes / (1024 * 1024 * 1024);
                    TotalStorageSize = gigabytes >= 1024
                        ? $"{(gigabytes / 1024):0.2} TB"
                        : $"{gigabytes:0.1} GB";
                }

                // 1. Genre Distribution
                var genreMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in allFiles)
                {
                    if (!string.IsNullOrWhiteSpace(f.Genres) && f.Genres != "N/A")
                    {
                        var split = f.Genres.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var rawG in split)
                        {
                            var g = GenreTranslatorService.Translate(rawG.Trim());
                            if (!string.IsNullOrWhiteSpace(g))
                            {
                                genreMap[g] = genreMap.GetValueOrDefault(g, 0) + 1;
                            }
                        }
                    }
                }

                TopGenres.Clear();
                int maxGenreCount = genreMap.Values.DefaultIfEmpty(1).Max();
                int genreColorIdx = 0;
                foreach (var kvp in genreMap.OrderByDescending(x => x.Value).Take(10))
                {
                    double pct = Math.Round((double)kvp.Value / maxGenreCount * 100, 1);
                    TopGenres.Add(new GenreStatItem
                    {
                        GenreName = kvp.Key,
                        Count = kvp.Value,
                        Percentage = pct,
                        BarColor = _genreColors[genreColorIdx % _genreColors.Length]
                    });
                    genreColorIdx++;
                }

                // 2. Quality / Resolution Breakdown
                var qualityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in allFiles)
                {
                    string res = ExtractNormalizedResolution(f.Resolution, f.Quality);
                    qualityMap[res] = qualityMap.GetValueOrDefault(res, 0) + 1;
                }

                QualityBreakdown.Clear();
                int maxQualityCount = qualityMap.Values.DefaultIfEmpty(1).Max();
                int qColorIdx = 0;
                foreach (var kvp in qualityMap.OrderByDescending(x => x.Value))
                {
                    double pct = Math.Round((double)kvp.Value / allFiles.Count * 100, 1);
                    QualityBreakdown.Add(new SimpleStatItem
                    {
                        Name = kvp.Key,
                        Count = kvp.Value,
                        Percentage = pct,
                        Color = _qualityColors[qColorIdx % _qualityColors.Length]
                    });
                    qColorIdx++;
                }

                // 3. Decades Breakdown
                var decadeMap = new Dictionary<string, int>();
                foreach (var f in allFiles)
                {
                    string decade = ExtractDecade(f.Year);
                    decadeMap[decade] = decadeMap.GetValueOrDefault(decade, 0) + 1;
                }

                DecadeBreakdown.Clear();
                int dColorIdx = 0;
                foreach (var kvp in decadeMap.OrderByDescending(x => x.Key))
                {
                    double pct = Math.Round((double)kvp.Value / allFiles.Count * 100, 1);
                    DecadeBreakdown.Add(new SimpleStatItem
                    {
                        Name = kvp.Key,
                        Count = kvp.Value,
                        Percentage = pct,
                        Color = _decadeColors[dColorIdx % _decadeColors.Length]
                    });
                    dColorIdx++;
                }

                // 4. Top Directors & Actors
                var directorMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var actorMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var g in groupedMovies.Concat(groupedSeries))
                {
                    var first = g.First();
                    if (!string.IsNullOrWhiteSpace(first.Director) && first.Director != "N/A")
                    {
                        foreach (var d in first.Director.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var trimmed = d.Trim();
                            if (trimmed.Length > 2)
                                directorMap[trimmed] = directorMap.GetValueOrDefault(trimmed, 0) + 1;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(first.Actors) && first.Actors != "N/A")
                    {
                        foreach (var a in first.Actors.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Take(4))
                        {
                            var trimmed = a.Trim();
                            if (trimmed.Length > 2)
                                actorMap[trimmed] = actorMap.GetValueOrDefault(trimmed, 0) + 1;
                        }
                    }
                }

                TopDirectors.Clear();
                foreach (var kvp in directorMap.OrderByDescending(x => x.Value).Take(5))
                {
                    TopDirectors.Add(new PersonStatItem
                    {
                        Name = kvp.Key,
                        Role = "کارگردان",
                        WorksCount = kvp.Value
                    });
                }

                TopActors.Clear();
                foreach (var kvp in actorMap.OrderByDescending(x => x.Value).Take(5))
                {
                    TopActors.Add(new PersonStatItem
                    {
                        Name = kvp.Key,
                        Role = "بازیگر",
                        WorksCount = kvp.Value
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error calculating analytics", ex);
            }
        }

        private static string FormatSecondsToPersianDuration(long totalSeconds)
        {
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
