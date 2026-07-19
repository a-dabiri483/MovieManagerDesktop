using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.Controls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FuzzySharp;
using MovieManagerDesktop.Services;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public partial class MoviesViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pageTitle = "کالکشن رسانه";

        [ObservableProperty]
        private bool _showFilters = true;

        [ObservableProperty]
        private string _searchQuery = string.Empty;
        
        [ObservableProperty]
        private int _mediaTypeFilterIndex = 0; // 0: All, 1: Movies, 2: Series
        partial void OnMediaTypeFilterIndexChanged(int value) => SaveAndLoad();
        
        [ObservableProperty]
        private int _watchedFilterIndex = 0; // 0: All, 1: Watched, 2: Unwatched
        partial void OnWatchedFilterIndexChanged(int value) => SaveAndLoad();
        
        [ObservableProperty]
        private int _listFilterIndex = 0; // 0: All, 1: Favorites, 2: Watchlist
        partial void OnListFilterIndexChanged(int value) => SaveAndLoad();

        [ObservableProperty]
        private int _sortIndex = 0; // 0: Date Added, 1: Name, 2: Year, 3: Rating

        partial void OnSortIndexChanged(int value) => SaveAndLoad();

        [ObservableProperty]
        private int _sortDirectionIndex = 0; // 0: نزولی, 1: صعودی
        
        partial void OnSortDirectionIndexChanged(int value) => SaveAndLoad();

        [ObservableProperty]
        private int _selectedGenreIndex = 0;

        partial void OnSelectedGenreIndexChanged(int value) => SaveAndLoad();

        [ObservableProperty]
        private bool _isQuickFilterMovies = false;
        partial void OnIsQuickFilterMoviesChanged(bool value) => SaveAndLoad();

        [ObservableProperty]
        private double _scrollPosition = 0;

        // Index of the last item clicked to open details, used for scroll restoration
        public int LastClickedIndex { get; set; } = -1;

        [ObservableProperty]
        private bool _isQuickFilterSeries = false;
        partial void OnIsQuickFilterSeriesChanged(bool value) => SaveAndLoad();

        [ObservableProperty]
        private bool _isQuickFilterUnwatched = false;
        partial void OnIsQuickFilterUnwatchedChanged(bool value) => SaveAndLoad();

        private void SaveAndLoad()
        {
            var settings = SettingsManager.LoadSettings();
            settings.MediaTypeFilterIndex = MediaTypeFilterIndex;
            settings.WatchedFilterIndex = WatchedFilterIndex;
            settings.ListFilterIndex = ListFilterIndex;
            settings.SortIndex = SortIndex;
            settings.SortDirectionIndex = SortDirectionIndex;
            settings.SelectedGenreIndex = SelectedGenreIndex;
            settings.IsQuickFilterMovies = IsQuickFilterMovies;
            settings.IsQuickFilterSeries = IsQuickFilterSeries;
            settings.IsQuickFilterUnwatched = IsQuickFilterUnwatched;
            SettingsManager.SaveSettings(settings);
            
            _ = LoadMoviesAsync();
        }

        public string PersonFilterName { get; set; } = string.Empty;
        public string PersonFilterType { get; set; } = string.Empty; // "Actor" or "Director"
        public string CollectionFilter { get; set; } = string.Empty;

        [ObservableProperty]
        private int _posterSize = 220; // Default width 220
        public int PosterHeight => (int)(PosterSize * 1.5);

        partial void OnPosterSizeChanged(int value)
        {
            OnPropertyChanged(nameof(PosterHeight));
            var settings = SettingsManager.LoadSettings();
            if (settings.PosterSize != value)
            {
                settings.PosterSize = value;
                SettingsManager.SaveSettings(settings);
            }
        }

        [ObservableProperty]
        private bool _isRefreshing = false;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isInSelectionMode = false;

        [ObservableProperty]
        private int _selectedCount = 0;

        [ObservableProperty]
        private bool _hasNoMovies = false;

        [ObservableProperty]
        private bool _isBulkActionRunning = false;

        [ObservableProperty]
        private double _bulkActionProgress = 0;

        [ObservableProperty]
        private string _bulkActionText = string.Empty;

        private System.Threading.CancellationTokenSource? _bulkActionCts;

        [RelayCommand]
        private void CancelBulkAction()
        {
            _bulkActionCts?.Cancel();
        }

        public ObservableCollection<string> SearchHistory { get; } = new();

        public ObservableCollection<GalleryItemViewModel> Movies { get; } = new();

        public ObservableCollection<string> Genres { get; } = new();

        public MoviesViewModel()
        {
            LoadSearchHistory();
            var settings = SettingsManager.LoadSettings();
            PosterSize = settings.PosterSize > 50 ? settings.PosterSize : 220;
            
            _mediaTypeFilterIndex = settings.MediaTypeFilterIndex;
            _watchedFilterIndex = settings.WatchedFilterIndex;
            _listFilterIndex = settings.ListFilterIndex;
            _sortIndex = settings.SortIndex;
            _sortDirectionIndex = settings.SortDirectionIndex;
            _selectedGenreIndex = settings.SelectedGenreIndex;
            _isQuickFilterMovies = settings.IsQuickFilterMovies;
            _isQuickFilterSeries = settings.IsQuickFilterSeries;
            _isQuickFilterUnwatched = settings.IsQuickFilterUnwatched;
            
            _ = LoadGenresAsync();
            _ = LoadMoviesAsync();
            
            WeakReferenceMessenger.Default.Register<MovieManagerDesktop.Messages.MediaUpdatedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = LoadGenresAsync();
                    _ = LoadMoviesAsync();
                });
            });
        }

        private async Task LoadGenresAsync()
        {
            try
            {
                var genres = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var allGenres = db.VideoFiles
                        .Where(v => !string.IsNullOrEmpty(v.Genres))
                        .Select(v => v.Genres)
                        .ToList();
                    
                    var uniqueGenres = allGenres
                        .SelectMany(g => g.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(g => g.Trim())
                        .Where(g => !string.IsNullOrEmpty(g))
                        .Distinct()
                        .OrderBy(g => g)
                        .ToList();
                    return uniqueGenres;
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Genres.Clear();
                    Genres.Add("همه ژانرها");
                    foreach (var g in genres)
                    {
                        Genres.Add(g);
                    }
                    if (SelectedGenreIndex >= Genres.Count) SelectedGenreIndex = 0;
                });
            }
            catch { }
        }

        public async Task LoadMoviesAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            Movies.Clear();
            
            try
            {
                var grouped = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var query = db.VideoFiles.AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    string FixPersian(string? text) => (text ?? "").ToLowerInvariant().Replace("ي", "ی").Replace("ك", "ک");
                    
                    var searchTerms = FixPersian(SearchQuery).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (searchTerms.Length > 0)
                    {
                        var allItems = query.ToList(); // Load into memory for string matching
                        
                        query = allItems.Where(v => 
                        {
                            string title = FixPersian(v.FormattedTitle);
                            string fileName = FixPersian(v.FileName);
                            string actors = FixPersian(v.Actors);
                            string director = FixPersian(v.Director);
                            string collection = FixPersian(v.CollectionName);

                            return searchTerms.All(term => 
                                title.Contains(term) || 
                                fileName.Contains(term) ||
                                actors.Contains(term) ||
                                director.Contains(term) ||
                                collection.Contains(term)
                            );
                        }).AsQueryable();
                    }
                }

                if (MediaTypeFilterIndex == 1) query = query.Where(v => v.MediaType == "Movie");
                else if (MediaTypeFilterIndex == 2) query = query.Where(v => v.MediaType == "Series");
                
                if (WatchedFilterIndex == 1) query = query.Where(v => v.IsWatched);
                else if (WatchedFilterIndex == 2) query = query.Where(v => !v.IsWatched);
                
                if (ListFilterIndex == 1) query = query.Where(v => v.IsFavorite);
                else if (ListFilterIndex == 2) query = query.Where(v => v.IsWatchlist);

                if (IsQuickFilterMovies) query = query.Where(v => v.MediaType == "Movie");
                if (IsQuickFilterSeries) query = query.Where(v => v.MediaType == "Series");
                if (IsQuickFilterUnwatched) query = query.Where(v => !v.IsWatched);

                var allFiles = query.ToList();

                if (!string.IsNullOrWhiteSpace(PersonFilterName))
                {
                    allFiles = allFiles.Where(v => 
                    {
                        var data = PersonFilterType == "Actor" ? v.Actors : v.Director;
                        if (string.IsNullOrWhiteSpace(data)) return false;
                        var parts = data.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                        return parts.Any(p => p.Equals(PersonFilterName, StringComparison.OrdinalIgnoreCase));
                    }).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(CollectionFilter))
                {
                    allFiles = allFiles.Where(v => v.CollectionName != null && v.CollectionName.Equals(CollectionFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (SelectedGenreIndex > 0 && SelectedGenreIndex < Genres.Count)
                {
                    string selectedGenre = Genres[SelectedGenreIndex];
                    allFiles = allFiles.Where(v => 
                    {
                        if (string.IsNullOrWhiteSpace(v.Genres)) return false;
                        var parts = v.Genres.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                        return parts.Any(p => p.Equals(selectedGenre, StringComparison.OrdinalIgnoreCase));
                    }).ToList();
                }
                
                var resultList = allFiles
                    .GroupBy(v => new { Title = (v.FormattedTitle ?? "ناشناس").ToLowerInvariant(), Type = v.MediaType })
                    .Select(g => 
                    {
                        var first = g.First();
                        if (g.Key.Type == "Series")
                        {
                            first.NumberOfEpisodes = g.Count();
                            first.NumberOfSeasons = g.Select(x => x.Season).Distinct().Count(s => s != null);
                        }
                        first.IsFavorite = g.Any(x => x.IsFavorite);
                        first.IsWatchlist = g.Any(x => x.IsWatchlist);
                        return new GalleryItemViewModel(first, UpdateSelectionState, async (item) => await ToggleFavoriteAsync(item));
                    });

                bool isAscending = SortDirectionIndex == 1;

                if (SortIndex == 1) // Name
                    resultList = isAscending ? resultList.OrderBy(v => v.File.FormattedTitle) : resultList.OrderByDescending(v => v.File.FormattedTitle);
                else if (SortIndex == 2) // Year
                    resultList = isAscending ? resultList.OrderBy(v => v.File.Year) : resultList.OrderByDescending(v => v.File.Year);
                else if (SortIndex == 3) // Rating
                    resultList = isAscending ? resultList.OrderBy(v => v.File.Rating) : resultList.OrderByDescending(v => v.File.Rating);
                else // Date Added
                    resultList = isAscending ? resultList.OrderBy(v => v.File.DateAdded) : resultList.OrderByDescending(v => v.File.DateAdded);

                return resultList.ToList();
                });

                foreach (var m in grouped)
                {
                    Movies.Add(m);
                }
            }
            catch { }
            finally
            {
                HasNoMovies = Movies.Count == 0;
                IsLoading = false;
            }
        }

        partial void OnSearchQueryChanged(string value) => _ = LoadMoviesAsync();


        private void UpdateSelectionState()
        {
            SelectedCount = Movies.Count(m => m.IsSelected);
            IsInSelectionMode = SelectedCount > 0;
        }

        [RelayCommand]
        private void OpenDetails(GalleryItemViewModel item)
        {
            if (item != null && item.File != null)
            {
                LastClickedIndex = Movies.IndexOf(item);
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MediaDetailsViewModel(item.File, this)));
            }
        }

        [RelayCommand]
        private void GoToScan()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ScanViewModel()));
        }

        [RelayCommand]
        private void SaveSearchHistory()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchHistoryService.AddSearch(SearchQuery);
                LoadSearchHistory();
            }
        }

        [RelayCommand]
        private void ClearSearchHistory()
        {
            SearchHistoryService.ClearHistory();
            LoadSearchHistory();
        }

        [RelayCommand]
        private void ResetSize()
        {
            PosterSize = 220;
        }

        [RelayCommand]
        private async Task ToggleFavoriteAsync(GalleryItemViewModel item)
        {
            item.IsFavorite = !item.IsFavorite;
            await SaveFileStateAsync(item.File);
        }

        [RelayCommand]
        private async Task ToggleWatchlistAsync(GalleryItemViewModel item)
        {
            item.IsWatchlist = !item.IsWatchlist;
            await SaveFileStateAsync(item.File);
        }

        private async Task SaveFileStateAsync(VideoFile file)
        {
            try
            {
                using var db = new AppDbContext();
                // If this is a series, update all files in this series
                var filesToUpdate = db.VideoFiles.Where(f => f.FormattedTitle == file.FormattedTitle && f.MediaType == file.MediaType).ToList();
                foreach(var f in filesToUpdate)
                {
                    f.IsFavorite = file.IsFavorite;
                    f.IsWatchlist = file.IsWatchlist;
                }
                await db.SaveChangesAsync();
            }
            catch { }
        }

        [RelayCommand]
        private async Task RefreshSelectedAsync()
        {
            var selectedItems = Movies.Where(m => m.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                ToastService.Instance.ShowWarning("هیچ آیتمی انتخاب نشده است.");
                return;
            }

            IsRefreshing = true;
            IdentifyMediaService identifyService = new IdentifyMediaService();

            await Task.Run(async () =>
            {
                using var db = new AppDbContext();
                foreach (var item in selectedItems)
                {
                    Application.Current.Dispatcher.Invoke(() => item.IsUpdating = true);

                    var identified = await identifyService.IdentifyMediaAsync(item.File);
                    bool hasData = (identified.TmdbId.HasValue && identified.TmdbId > 0) || !string.IsNullOrWhiteSpace(identified.PosterUrl);

                    if (hasData)
                    {
                        var filesToUpdate = db.VideoFiles.Where(f => f.FormattedTitle == item.File.FormattedTitle && f.MediaType == item.File.MediaType).ToList();
                        foreach(var f in filesToUpdate)
                        {
                            f.TmdbId = identified.TmdbId;
                            f.PosterUrl = identified.PosterUrl;
                            f.Rating = identified.Rating;
                            f.Overview = identified.Overview;
                            f.BackdropUrl = identified.BackdropUrl;
                            f.Genres = identified.Genres;
                            f.Actors = identified.Actors;
                            f.Director = identified.Director;
                            if (!string.IsNullOrWhiteSpace(identified.Year)) f.Year = identified.Year;
                            
                            db.VideoFiles.Update(f);
                        }
                    }
                    
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        item.IsSelected = false;
                        item.IsUpdating = false;
                        // Trigger UI update
                        item.NotifyFileChanged();
                    });
                }
                await db.SaveChangesAsync();
            });

            IsRefreshing = false;
            await LoadMoviesAsync(); // Reload to refresh visually
            ToastService.Instance.ShowSuccess("بروزرسانی اطلاعات با موفقیت به پایان رسید.");
        }
        
        [RelayCommand]
        private void SelectAll()
        {
            foreach(var item in Movies) item.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach(var item in Movies) item.IsSelected = false;
        }

        [RelayCommand]
        private void ExitSelectionMode()
        {
            DeselectAll();
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var selectedItems = Movies.Where(m => m.IsSelected).ToList();
            if (!selectedItems.Any()) return;

            var dialog = new ConfirmDialog($"آیا از حذف {selectedItems.Count} مورد اطمینان دارید؟");
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is bool res && res)
            {
                await RunBulkActionAsync("حذف", (db, f) => db.VideoFiles.Remove(f));
            }
        }

        private async Task RunBulkActionAsync(string actionName, Action<AppDbContext, Models.VideoFile> action)
        {
            var selectedItems = Movies.Where(m => m.IsSelected).ToList();
            if (!selectedItems.Any()) return;
            
            IsBulkActionRunning = true;
            BulkActionProgress = 0;
            BulkActionText = $"در حال انجام {actionName} ({selectedItems.Count} مورد)";
            _bulkActionCts = new System.Threading.CancellationTokenSource();
            var token = _bulkActionCts.Token;

            try 
            {
                await Task.Run(async () => 
                {
                    using var db = new AppDbContext();
                    int processed = 0;
                    foreach (var item in selectedItems)
                    {
                        if (token.IsCancellationRequested) break;
                        
                        var files = db.VideoFiles.Where(f => f.FormattedTitle == item.File.FormattedTitle && f.MediaType == item.File.MediaType).ToList();
                        foreach (var f in files) action(db, f);
                        
                        processed++;
                        Application.Current.Dispatcher.Invoke(() => {
                            BulkActionProgress = (double)processed / selectedItems.Count * 100;
                        });
                    }
                    await db.SaveChangesAsync();
                }, token);
                
                if (!token.IsCancellationRequested)
                    ToastService.Instance.ShowSuccess($"{selectedItems.Count} مورد ({actionName}) با موفقیت اعمال شد.");
                else
                    ToastService.Instance.ShowWarning($"عملیات {actionName} لغو شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error($"Bulk action failed: {actionName}", ex);
            }
            finally 
            {
                IsBulkActionRunning = false;
                ExitSelectionMode();
                await LoadMoviesAsync();
            }
        }



        private void LoadSearchHistory()
        {
            SearchHistory.Clear();
            foreach (var item in SearchHistoryService.GetHistory())
            {
                SearchHistory.Add(item);
            }
        }

        [RelayCommand]
        private async Task ToggleFavoritesSelectedAsync()
        {
            await RunBulkActionAsync("علاقه‌مندی‌ها", (db, f) => f.IsFavorite = !f.IsFavorite);
        }

        [RelayCommand]
        private async Task ToggleWatchlistSelectedAsync()
        {
            await RunBulkActionAsync("لیست تماشا", (db, f) => f.IsWatchlist = !f.IsWatchlist);
        }

        [RelayCommand]
        private async Task ToggleWatchedSelectedAsync()
        {
            await RunBulkActionAsync("وضعیت مشاهده", (db, f) => f.IsWatched = !f.IsWatched);
        }
    }
}
