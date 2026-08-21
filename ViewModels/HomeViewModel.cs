using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MovieManagerDesktop.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _movieCount;

        [ObservableProperty]
        private int _seriesCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private string _averageRating = "0.0";

        [ObservableProperty]
        private string _totalFileSize = "0 GB";

        [ObservableProperty]
        private string _topGenres = "موردی یافت نشد";

        [ObservableProperty]
        private double _moviePercentage = 0;

        [ObservableProperty]
        private double _seriesPercentage = 0;

        [ObservableProperty]
        private string _featuredTitle = "";

        [ObservableProperty]
        private string? _featuredBackdropUrl;

        [ObservableProperty]
        private string? _featuredPosterUrl;

        [ObservableProperty]
        private string _featuredGenres = "";

        [ObservableProperty]
        private string _featuredRating = "";

        [ObservableProperty]
        private string _featuredMediaType = "Movie";

        [ObservableProperty]
        private VideoFile? _featuredVideoFile;

        [ObservableProperty]
        private bool _hasContinueWatching = false;

        public ObservableCollection<VideoFile> ContinueWatchingItems { get; } = new();

        public HomeViewModel()
        {
            LoadHomeDataDirect();

            WeakReferenceMessenger.Default.Register<MediaUpdatedMessage>(this, (r, m) =>
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    LoadHomeDataDirect();
                });
            });
        }

        public void LoadHomeDataDirect()
        {
            try
            {
                using var db = new AppDbContext();
                if (!db.Database.CanConnect()) return;

                var allFiles = db.VideoFiles.AsNoTracking().ToList();
                int totalFilesCount = allFiles.Count;

                var grouped = allFiles
                    .GroupBy(v => new { Title = (v.FormattedTitle ?? "ناشناس").ToLowerInvariant(), Type = v.MediaType })
                    .ToList();

                int movies = grouped.Count(g => g.Key.Type == "Movie");
                int series = grouped.Count(g => g.Key.Type == "Series");
                int totalUnique = movies + series;

                double moviePct = totalUnique > 0 ? Math.Round((double)movies / totalUnique * 100, 1) : 0;
                double seriesPct = totalUnique > 0 ? Math.Round((double)series / totalUnique * 100, 1) : 0;

                // Featured Random Media
                var withBackdrop = allFiles.Where(f => !string.IsNullOrEmpty(f.BackdropUrl)).ToList();
                var rand = new Random();
                var featuredCandidate = (withBackdrop.Count > 0 ? withBackdrop[rand.Next(withBackdrop.Count)] : null)
                    ?? allFiles.Where(f => !string.IsNullOrEmpty(f.PosterUrl)).OrderBy(_ => rand.Next()).FirstOrDefault()
                    ?? allFiles.FirstOrDefault();

                string featuredTitle = "";
                string? featuredBackdrop = null;
                string? featuredPoster = null;
                string featuredGenres = "";
                string featuredRating = "";
                string featuredMediaType = "Movie";

                if (featuredCandidate != null)
                {
                    featuredTitle = string.IsNullOrWhiteSpace(featuredCandidate.FormattedTitle) ? featuredCandidate.FileName : featuredCandidate.FormattedTitle;
                    string? backdrop = featuredCandidate.BackdropUrl;
                    if (!string.IsNullOrEmpty(backdrop))
                    {
                        if (backdrop.Contains("/w500/")) backdrop = backdrop.Replace("/w500/", "/w1280/");
                        else if (backdrop.Contains("/w300/")) backdrop = backdrop.Replace("/w300/", "/w1280/");
                        else if (backdrop.Contains("/w780/")) backdrop = backdrop.Replace("/w780/", "/w1280/");
                    }
                    else
                    {
                        backdrop = featuredCandidate.PosterUrl;
                        if (!string.IsNullOrEmpty(backdrop))
                        {
                            if (backdrop.Contains("/w500/")) backdrop = backdrop.Replace("/w500/", "/w1280/");
                            else if (backdrop.Contains("/w342/")) backdrop = backdrop.Replace("/w342/", "/w780/");
                            else if (backdrop.Contains("/w185/")) backdrop = backdrop.Replace("/w185/", "/w500/");
                        }
                    }

                    featuredBackdrop = backdrop;
                    featuredPoster = featuredCandidate.PosterUrl;
                    featuredGenres = !string.IsNullOrEmpty(featuredCandidate.Genres)
                        ? GenreTranslatorService.TranslateList(featuredCandidate.Genres).Replace("،", " • ")
                        : (featuredCandidate.MediaType == "Series" ? "سریال" : "فیلم سینمایی");

                    featuredRating = (featuredCandidate.Rating.HasValue && featuredCandidate.Rating.Value > 0)
                        ? featuredCandidate.Rating.Value.ToString("0.0")
                        : "";

                    featuredMediaType = featuredCandidate.MediaType ?? "Movie";
                }

                // Rating
                string avgRating = "0.0";
                var validRatings = allFiles.Where(f => f.Rating.HasValue && f.Rating.Value > 0).Select(f => f.Rating.Value).ToList();
                if (validRatings.Any())
                {
                    avgRating = validRatings.Average().ToString("0.0");
                }

                // Size
                long totalBytes = allFiles.Sum(f => f.FileSizeBytes);
                string fileSize = FormatBytes(totalBytes);

                var genres = allFiles
                    .Where(f => !string.IsNullOrEmpty(f.Genres))
                    .SelectMany(f => f.Genres!.Split(new[] { ',', '،', '/' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(g => GenreTranslatorService.Translate(g.Trim()))
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .GroupBy(g => g)
                    .OrderByDescending(g => g.Count())
                    .Take(4)
                    .Select(g => g.Key)
                    .ToList();

                string topGenresText = "ثبت نشده";
                if (genres.Any())
                {
                    topGenresText = string.Join("، ", genres);
                }

                // Continue Watching:
                // 1. Movies with progress > 0 and not fully watched
                var continueMovies = allFiles
                    .Where(f => f.MediaType != "Series" && f.WatchProgressPercent > 0 && f.WatchProgressPercent < 95 && !f.IsWatched)
                    .ToList();

                // 2. Series:
                // Group all series by FormattedTitle
                var seriesGroups = allFiles
                    .Where(f => f.MediaType == "Series")
                    .GroupBy(f => (!string.IsNullOrWhiteSpace(f.FormattedTitle) ? f.FormattedTitle : f.FileName).ToLowerInvariant());

                var continueSeries = new List<VideoFile>();
                foreach (var sGroup in seriesGroups)
                {
                    var eps = sGroup.OrderBy(e => e.Season ?? 1).ThenBy(e => e.Episode ?? 1).ToList();
                    bool hasAnyActivity = eps.Any(e => e.IsWatched || e.WatchProgressPercent > 0 || e.LastPlayedAt.HasValue);
                    if (hasAnyActivity)
                    {
                        // Check if there is an in-progress episode
                        var inProgressEp = eps.FirstOrDefault(e => !e.IsWatched && e.WatchProgressPercent > 0 && e.WatchProgressPercent < 95);
                        if (inProgressEp != null)
                        {
                            continueSeries.Add(inProgressEp);
                        }
                        else
                        {
                            // Otherwise find next unwatched episode
                            var nextUnwatchedEp = eps.FirstOrDefault(e => !e.IsWatched);
                            if (nextUnwatchedEp != null)
                            {
                                continueSeries.Add(nextUnwatchedEp);
                            }
                        }
                    }
                }

                var combinedContinueWatching = continueMovies
                    .Concat(continueSeries)
                    .OrderByDescending(f => f.LastPlayedAt ?? (f.WatchProgressPercent > 0 ? DateTime.Now.AddDays(-1) : f.DateAdded))
                    .Take(20)
                    .ToList();

                TotalCount = totalFilesCount;
                MovieCount = movies;
                SeriesCount = series;
                MoviePercentage = moviePct;
                SeriesPercentage = seriesPct;

                FeaturedVideoFile = featuredCandidate;
                FeaturedTitle = featuredTitle;
                FeaturedBackdropUrl = featuredBackdrop;
                FeaturedPosterUrl = featuredPoster;
                FeaturedGenres = featuredGenres;
                FeaturedRating = featuredRating;
                FeaturedMediaType = featuredMediaType;

                AverageRating = avgRating;
                TotalFileSize = fileSize;
                TopGenres = topGenresText;

                // Update Continue Watching collection
                ContinueWatchingItems.Clear();
                foreach (var item in combinedContinueWatching)
                {
                    ContinueWatchingItems.Add(item);
                }
                HasContinueWatching = ContinueWatchingItems.Count > 0;
            }
            catch { }
        }

        private string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }
            return $"{dblSByte:0.##} {Suffix[i]}";
        }

        [RelayCommand]
        private void PlayContinueWatching(VideoFile? file)
        {
            if (file != null)
            {
                file.LastPlayedAt = DateTime.Now;
                PlaybackService.PlayMedia(file);

                // Move played item to position 0 (beginning of the list)
                if (ContinueWatchingItems.Contains(file))
                {
                    ContinueWatchingItems.Remove(file);
                    ContinueWatchingItems.Insert(0, file);
                }
            }
        }

        [RelayCommand]
        private void OpenContinueWatchingDetails(VideoFile? file)
        {
            if (file != null)
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(file, this)));
            }
        }

        [RelayCommand]
        private void GoToScan()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ScanViewModel()));
        }

        [RelayCommand]
        private void GoToMovies()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new MoviesViewModel()));
        }

        [RelayCommand]
        private void GoToManualSearch()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ManualSearchViewModel()));
        }

        [RelayCommand]
        private void GoToAnalytics()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new AnalyticsViewModel()));
        }

        [RelayCommand]
        private void GoToTracker()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new TrackerViewModel()));
        }

        [RelayCommand]
        private void GoToCalendar()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CalendarViewModel()));
        }

        [RelayCommand]
        private void GoToTools()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        [RelayCommand]
        private void GoToCollections()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CollectionsViewModel()));
        }

        [RelayCommand]
        private void GoToActors()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new PeopleViewModel("Actor")));
        }

        [RelayCommand]
        private void GoToDirectors()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new PeopleViewModel("Director")));
        }

        [RelayCommand]
        private void GoToCinemaHub()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CinemaHubViewModel(0)));
        }

        [RelayCommand]
        private void GoToCinemaNews()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CinemaHubViewModel(0)));
        }

        [RelayCommand]
        private void GoToBoxOffice()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CinemaHubViewModel(1)));
        }

        [RelayCommand]
        private void GoToReleases()
        {
            WeakReferenceMessenger.Default.Send(new CinemaHubViewModel(2) != null ? new NavigationMessage(new CinemaHubViewModel(2)) : new NavigationMessage(new CalendarViewModel()));
        }

        [RelayCommand]
        private void PlayFeatured()
        {
            OpenFeaturedDetails();
        }

        [RelayCommand]
        private void OpenFeaturedDetails()
        {
            if (FeaturedVideoFile != null)
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(FeaturedVideoFile, this)));
            }
            else
            {
                GoToMovies();
            }
        }
    }
}
