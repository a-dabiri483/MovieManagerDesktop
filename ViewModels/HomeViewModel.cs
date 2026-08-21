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
                string fileSize = "0 GB";
                long totalBytes = allFiles.Sum(f => f.FileSizeBytes);
                if (totalBytes > 0)
                {
                    double tb = totalBytes / 1024.0 / 1024.0 / 1024.0 / 1024.0;
                    double gb = totalBytes / 1024.0 / 1024.0 / 1024.0;
                    fileSize = tb >= 1.0 ? $"{tb:0.##} TB" : $"{gb:0.##} GB";
                }

                // Top Genres
                string topGenresText = "موردی یافت نشد";
                var genres = allFiles
                    .Where(f => !string.IsNullOrEmpty(f.Genres) && f.Genres != "N/A")
                    .SelectMany(f => f.Genres!.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(g => g.Trim())
                    .Where(g => !string.IsNullOrEmpty(g))
                    .Select(GenreTranslatorService.Translate)
                    .GroupBy(g => g)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                if (genres.Any())
                {
                    topGenresText = string.Join("، ", genres);
                }

                // Continue Watching: items with progress > 0 and < 100
                var continueWatchingRaw = allFiles
                    .Where(f => f.WatchProgressPercent > 0 && f.WatchProgressPercent < 100)
                    .ToList();

                // Group series by title, keep only 1 item per series (the most recently played or highest ep)
                var continueWatchingGrouped = continueWatchingRaw
                    .GroupBy(f => string.Equals(f.MediaType, "Series", StringComparison.OrdinalIgnoreCase) 
                        ? (!string.IsNullOrWhiteSpace(f.FormattedTitle) ? f.FormattedTitle : f.FileName).ToLowerInvariant() 
                        : f.Id.ToString())
                    .Select(g => g.OrderByDescending(f => f.LastPlayedAt ?? DateTime.MinValue)
                                  .ThenByDescending(f => f.Season ?? 0)
                                  .ThenByDescending(f => f.LastPlayedEpisode ?? f.Episode ?? 0)
                                  .ThenByDescending(f => f.DateAdded)
                                  .First())
                    .OrderByDescending(f => f.LastPlayedAt ?? f.DateAdded)
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
                foreach (var item in continueWatchingGrouped)
                {
                    ContinueWatchingItems.Add(item);
                }
                HasContinueWatching = ContinueWatchingItems.Count > 0;
            }
            catch { }
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
                if (file.MediaType == "Series")
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesDetailViewModel(file)));
                else
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(file)));
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
            if (FeaturedVideoFile != null)
            {
                PlaybackService.PlayMedia(FeaturedVideoFile);
            }
            else
            {
                GoToMovies();
            }
        }

        [RelayCommand]
        private void OpenFeaturedDetails()
        {
            if (FeaturedVideoFile != null)
            {
                if (FeaturedVideoFile.MediaType == "Series")
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesDetailViewModel(FeaturedVideoFile)));
                else
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(FeaturedVideoFile)));
            }
            else
            {
                GoToMovies();
            }
        }
    }
}
