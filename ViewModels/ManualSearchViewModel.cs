using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ManualSearchResultItem : ObservableObject
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReleaseYear { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public string BackdropUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "Movie";
        public string Overview { get; set; } = string.Empty;
        public double Rating { get; set; } = 0;

        [ObservableProperty]
        private bool _isInTracker;

        [ObservableProperty]
        private bool _isAdding;

        public string MediaTypeDisplay => MediaType == "Series" ? "سریال" : "فیلم سینمایی";

        [RelayCommand]
        private async Task ToggleTrackerAsync()
        {
            if (IsAdding) return;
            IsAdding = true;

            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();
                    var existing = db.VideoFiles.FirstOrDefault(v => (v.TmdbId.HasValue && v.TmdbId.Value == Id) || (v.FormattedTitle.ToLower() == Title.ToLower() && v.MediaType == MediaType));

                    if (IsInTracker)
                    {
                        // Remove from tracker
                        if (existing != null)
                        {
                            if (existing.FilePath == "[Manual Tracker]" || string.IsNullOrEmpty(existing.FilePath))
                            {
                                db.VideoFiles.Remove(existing);
                            }
                            else
                            {
                                existing.IsTracked = false;
                                existing.IsWatchlist = false;
                            }
                            await db.SaveChangesAsync();
                        }
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            IsInTracker = false;
                            ToastService.Instance.ShowSuccess($"«{Title}» از ردیاب حذف شد.");
                        });
                    }
                    else
                    {
                        // Add to tracker
                        var target = existing;
                        if (target == null)
                        {
                            target = new VideoFile
                            {
                                TmdbId = Id,
                                FormattedTitle = Title,
                                FileName = Title,
                                FilePath = "[Manual Tracker]",
                                MediaType = MediaType,
                                Year = ReleaseYear,
                                PosterUrl = PosterUrl,
                                BackdropUrl = BackdropUrl,
                                Rating = Rating,
                                Overview = Overview,
                                DateAdded = DateTime.Now,
                                IsTracked = true,
                                IsWatchlist = true
                            };
                            db.VideoFiles.Add(target);
                        }
                        else
                        {
                            target.IsTracked = true;
                            target.IsWatchlist = true;
                            if (string.IsNullOrEmpty(target.PosterUrl)) target.PosterUrl = PosterUrl;
                            if (string.IsNullOrEmpty(target.BackdropUrl)) target.BackdropUrl = BackdropUrl;
                            if (string.IsNullOrEmpty(target.Overview)) target.Overview = Overview;
                        }

                        await db.SaveChangesAsync();

                        // Fetch extra series metadata if it's TV
                        if (MediaType == "Series")
                        {
                            var identifySvc = new IdentifyMediaService();
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var settings = SettingsManager.LoadSettings();
                                    string apiKey = SettingsManager.GetTmdbApiKey();
                                    string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;

                                    await identifySvc.IdentifySeriesDetailsAsync(target, apiKey, language);

                                    using (var db2 = new AppDbContext())
                                    {
                                        var dbTarget = db2.VideoFiles.FirstOrDefault(v => v.Id == target.Id);
                                        if (dbTarget != null)
                                        {
                                            dbTarget.FirstAirDate = target.FirstAirDate;
                                            dbTarget.LastAirDate = target.LastAirDate;
                                            dbTarget.NetworkName = target.NetworkName;
                                            dbTarget.AirDay = target.AirDay;
                                            dbTarget.AirTime = target.AirTime;
                                            dbTarget.TotalSeasonsCount = target.TotalSeasonsCount;
                                            dbTarget.TotalEpisodesCount = target.TotalEpisodesCount;
                                            dbTarget.NextEpisodeDate = target.NextEpisodeDate;
                                            dbTarget.NextEpisodeNumber = target.NextEpisodeNumber;
                                            dbTarget.SeriesStatus = target.SeriesStatus;
                                            if (string.IsNullOrWhiteSpace(dbTarget.Year) && target.FirstAirDate.HasValue)
                                                dbTarget.Year = target.FirstAirDate.Value.Year.ToString();
                                            await db2.SaveChangesAsync();
                                        }

                                        if (target.TmdbId.HasValue)
                                        {
                                            var (sList, eList) = await identifySvc.FetchSeriesDetailsAsync(target.TmdbId.Value);
                                            if (sList.Count > 0)
                                            {
                                                var oldS = db2.TvSeasons.Where(s => s.TmdbSeriesId == target.TmdbId.Value).ToList();
                                                var oldE = db2.TvEpisodes.Where(e => e.TmdbSeriesId == target.TmdbId.Value).ToList();
                                                db2.TvSeasons.RemoveRange(oldS);
                                                db2.TvEpisodes.RemoveRange(oldE);
                                                db2.TvSeasons.AddRange(sList);
                                                db2.TvEpisodes.AddRange(eList);
                                                await db2.SaveChangesAsync();
                                            }
                                        }
                                    }

                                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                                }
                                catch { }
                            });
                        }

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            IsInTracker = true;
                            ToastService.Instance.ShowSuccess($"«{Title}» با موفقیت به ردیاب اضافه شد.");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error toggling tracker", ex);
                ToastService.Instance.ShowError("خطا در به‌روزرسانی ردیاب.");
            }
            finally
            {
                IsAdding = false;
            }
        }
    }

    public partial class ManualSearchViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _identifyService;
        private readonly bool _returnToTracker;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _hasSearched;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // 0: همه, 1: فیلم‌ها, 2: سریال‌ها, 3: انیمه
        [ObservableProperty]
        private int _selectedFilterIndex = 0;

        public ObservableCollection<ManualSearchResultItem> Results { get; } = new();

        public ManualSearchViewModel(string initialQuery = "", bool returnToTracker = true)
        {
            _identifyService = new IdentifyMediaService();
            _returnToTracker = returnToTracker;
            _searchQuery = initialQuery;
            if (!string.IsNullOrWhiteSpace(initialQuery))
            {
                _ = SearchAsync();
            }
        }

        partial void OnSelectedFilterIndexChanged(int value)
        {
            if (HasSearched && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ToastService.Instance.ShowWarning("لطفاً نام فیلم یا سریال را وارد کنید.");
                return;
            }

            if (IsSearching) return;
            IsSearching = true;
            HasSearched = true;
            StatusMessage = "در حال جستجو در پایگاه داده TMDb...";
            Results.Clear();

            try
            {
                var searchResults = await Task.Run(async () =>
                {
                    List<TmdbSearchResult>? list = null;
                    if (SelectedFilterIndex == 3)
                    {
                        list = await _identifyService.SearchAnimeManualAsync(SearchQuery);
                    }
                    else
                    {
                        list = await _identifyService.SearchMediaAsync(SearchQuery);
                        if (list != null)
                        {
                            if (SelectedFilterIndex == 1)
                                list = list.Where(r => (r.MediaType ?? "").ToLower() != "tv" && (r.MediaType ?? "").ToLower() != "series").ToList();
                            else if (SelectedFilterIndex == 2)
                                list = list.Where(r => (r.MediaType ?? "").ToLower() == "tv" || (r.MediaType ?? "").ToLower() == "series").ToList();
                        }
                    }

                    if (list == null || list.Count == 0) return new List<ManualSearchResultItem>();

                    using var db = new AppDbContext();
                    var trackedIds = db.VideoFiles
                        .Where(v => v.IsTracked && v.TmdbId.HasValue)
                        .Select(v => v.TmdbId!.Value)
                        .ToHashSet();

                    var trackedTitles = db.VideoFiles
                        .Where(v => v.IsTracked)
                        .Select(v => v.FormattedTitle.ToLower())
                        .ToHashSet();

                    return list.Select(r =>
                    {
                        string mediaType = ((r.MediaType ?? "").ToLower() == "tv" || (r.MediaType ?? "").ToLower() == "series") ? "Series" : "Movie";
                        string poster = r.PosterUrl;
                        if (!string.IsNullOrEmpty(poster) && poster.Contains("/w92/"))
                            poster = poster.Replace("/w92/", "/w500/");

                        bool inTracker = trackedIds.Contains(r.Id) || trackedTitles.Contains((r.Title ?? "").ToLower());

                        return new ManualSearchResultItem
                        {
                            Id = r.Id,
                            Title = string.IsNullOrWhiteSpace(r.Title) ? r.OriginalTitle : r.Title,
                            ReleaseYear = r.ReleaseYear,
                            PosterUrl = poster,
                            MediaType = mediaType,
                            Overview = r.Overview,
                            IsInTracker = inTracker
                        };
                    }).ToList();
                });

                foreach (var item in searchResults)
                {
                    Results.Add(item);
                }

                StatusMessage = Results.Count > 0 
                    ? $"{Results.Count} نتیجه یافت شد." 
                    : $"موردی برای «{SearchQuery}» یافت نشد.";
            }
            catch (Exception ex)
            {
                LoggerService.Error("Search error", ex);
                StatusMessage = "خطا در برقراری ارتباط با سرور TMDb.";
                ToastService.Instance.ShowError("خطا در دریافت نتایج جستجو.");
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_returnToTracker)
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new TrackerViewModel()));
            else
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
