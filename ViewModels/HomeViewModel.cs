using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;
using System;
using System.Linq;
using System.Collections.Generic;

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

        public System.Collections.ObjectModel.ObservableCollection<GalleryItemViewModel> ContinueWatchingMovies { get; } = new();

        [ObservableProperty]
        private bool _hasContinueWatching = false;

        public System.Collections.ObjectModel.ObservableCollection<GalleryItemViewModel> NewEpisodes { get; } = new();

        [ObservableProperty]
        private bool _hasNewEpisodes = false;

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

        public HomeViewModel()
        {
            MovieCount = 0;
            SeriesCount = 0;
            TotalCount = 0;

            try 
            {
                using var db = new AppDbContext();
                if (db.Database.CanConnect())
                {
                    var allFiles = db.VideoFiles.ToList();
                    TotalCount = allFiles.Count;
                    
                    var grouped = allFiles
                        .GroupBy(v => new { Title = (v.FormattedTitle ?? "ناشناس").ToLowerInvariant(), Type = v.MediaType })
                        .ToList();
                    
                    MovieCount = grouped.Count(g => g.Key.Type == "Movie");
                    SeriesCount = grouped.Count(g => g.Key.Type == "Series");

                    int totalUnique = MovieCount + SeriesCount;
                    if (totalUnique > 0)
                    {
                        MoviePercentage = Math.Round((double)MovieCount / totalUnique * 100, 1);
                        SeriesPercentage = Math.Round((double)SeriesCount / totalUnique * 100, 1);
                    }

                    // Set Featured Media (Pick a RANDOM movie or series with a backdrop/poster)
                    var withBackdrop = allFiles
                        .Where(f => !string.IsNullOrEmpty(f.BackdropUrl))
                        .ToList();

                    var rand = new Random();
                    var featuredCandidate = (withBackdrop.Count > 0 ? withBackdrop[rand.Next(withBackdrop.Count)] : null)
                        ?? allFiles.Where(f => !string.IsNullOrEmpty(f.PosterUrl)).OrderBy(_ => rand.Next()).FirstOrDefault()
                        ?? allFiles.FirstOrDefault();

                    if (featuredCandidate != null)
                    {
                        FeaturedVideoFile = featuredCandidate;
                        FeaturedTitle = string.IsNullOrWhiteSpace(featuredCandidate.FormattedTitle) ? featuredCandidate.FileName : featuredCandidate.FormattedTitle;
                        
                        // Upgrade TMDb image quality for ultra crisp desktop displays
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

                        FeaturedBackdropUrl = backdrop;
                        FeaturedPosterUrl = featuredCandidate.PosterUrl;
                        FeaturedGenres = !string.IsNullOrEmpty(featuredCandidate.Genres)
                            ? featuredCandidate.Genres.Replace(",", " • ")
                            : (featuredCandidate.MediaType == "Series" ? "سریال" : "فیلم سینمایی");

                        FeaturedRating = (featuredCandidate.Rating.HasValue && featuredCandidate.Rating.Value > 0)
                            ? featuredCandidate.Rating.Value.ToString("0.0")
                            : "";

                        FeaturedMediaType = featuredCandidate.MediaType ?? "Movie";
                    }

                    // Calculate Continue Watching (فقط آیتم‌های با پیشرفت واقعی تماشا)
                    App.Current.Dispatcher.Invoke(() => ContinueWatchingMovies.Clear());
                    var continueWatchingList = new System.Collections.Generic.List<GalleryItemViewModel>();
                    
                    foreach(var g in grouped)
                    {
                        var first = g.First();
                        int tCount = g.Count();
                        int wCount = g.Count(x => x.IsWatched);
                        double progress = tCount > 0 ? (double)wCount / tCount * 100 : 0;
                        
                        if (g.Key.Type == "Movie" && first.WatchProgressPercent > 0 && first.WatchProgressPercent < 100)
                        {
                            progress = first.WatchProgressPercent;
                        }

                        if (progress > 0 && progress < 100)
                        {
                            if (g.Key.Type == "Series")
                            {
                                first.NumberOfEpisodes = tCount;
                                first.NumberOfSeasons = g.Select(x => x.Season).Distinct().Count(s => s != null);
                            }
                            first.WatchProgressPercent = Math.Round(progress, 1);
                            continueWatchingList.Add(new GalleryItemViewModel(first, () => { }));
                        }
                    }

                    App.Current.Dispatcher.Invoke(() => {
                        foreach(var item in continueWatchingList) ContinueWatchingMovies.Add(item);
                        HasContinueWatching = ContinueWatchingMovies.Count > 0;
                    });

                    // Check New Episodes
                    var trackedSeries = grouped.Where(g => g.Key.Type == "Series" && g.First().IsTracked).Select(g => g.First()).ToList();
                    _ = CheckNewEpisodesAsync(trackedSeries);

                    // Average Rating
                    var validRatings = allFiles
                        .Where(f => f.Rating.HasValue && f.Rating.Value > 0)
                        .Select(f => f.Rating.Value)
                        .ToList();
                    
                    if (validRatings.Any())
                    {
                        AverageRating = validRatings.Average().ToString("0.0");
                    }

                    // Total File Size
                    long totalBytes = allFiles.Sum(f => f.FileSizeBytes);
                    if (totalBytes > 0)
                    {
                        double tb = totalBytes / 1024.0 / 1024.0 / 1024.0 / 1024.0;
                        double gb = totalBytes / 1024.0 / 1024.0 / 1024.0;
                        if (tb >= 1.0)
                            TotalFileSize = $"{tb:0.##} TB";
                        else
                            TotalFileSize = $"{gb:0.##} GB";
                    }

                    // Top Genres
                    var genres = allFiles
                        .Where(f => !string.IsNullOrEmpty(f.Genres) && f.Genres != "N/A")
                        .SelectMany(f => f.Genres!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(g => g.Trim())
                        .GroupBy(g => g)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList();
                    
                    if (genres.Any())
                    {
                        TopGenres = string.Join("، ", genres);
                    }
                }
            } 
            catch { }
        }

        private async System.Threading.Tasks.Task CheckNewEpisodesAsync(List<Models.VideoFile> trackedSeries)
        {
            if (trackedSeries == null || !trackedSeries.Any()) return;

            var svc = new MovieManagerDesktop.Services.IdentifyMediaService();
            var newEpList = new List<GalleryItemViewModel>();

            foreach (var series in trackedSeries)
            {
                int currentLocalSeasons = series.NumberOfSeasons ?? 0;
                int currentLocalEpisodes = series.NumberOfEpisodes ?? 0;

                var updated = await svc.UpdateSeriesStatusAsync(series);
                if (updated != null)
                {
                    bool hasNew = false;
                    if (updated.NumberOfSeasons > currentLocalSeasons) hasNew = true;
                    if (updated.NumberOfSeasons == currentLocalSeasons && updated.NumberOfEpisodes > currentLocalEpisodes) hasNew = true;
                    
                    if (hasNew || series.HasNewEpisode)
                    {
                        series.HasNewEpisode = true;
                        newEpList.Add(new GalleryItemViewModel(series, () => { }));
                    }

                    using var db = new AppDbContext();
                    var filesToUpdate = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == series.FormattedTitle.ToLower()).ToList();
                    foreach (var f in filesToUpdate)
                    {
                        f.SeriesStatus = updated.SeriesStatus;
                        f.LastAiredSeason = updated.LastAiredSeason;
                        f.NumberOfSeasons = updated.NumberOfSeasons;
                        f.NumberOfEpisodes = updated.NumberOfEpisodes;
                        f.HasNewEpisode = series.HasNewEpisode;
                    }
                    await db.SaveChangesAsync();
                }
            }

            if (newEpList.Any())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    NewEpisodes.Clear();
                    foreach (var item in newEpList) NewEpisodes.Add(item);
                    HasNewEpisodes = true;
                });
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
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new MoviesViewModel()));
        }

        [RelayCommand]
        private void PlayFeatured()
        {
            if (FeaturedVideoFile != null && !string.IsNullOrEmpty(FeaturedVideoFile.FilePath) && System.IO.File.Exists(FeaturedVideoFile.FilePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = FeaturedVideoFile.FilePath,
                        UseShellExecute = true
                    });
                }
                catch { }
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
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(FeaturedVideoFile)));
            }
            else
            {
                GoToMovies();
            }
        }
    }
}
