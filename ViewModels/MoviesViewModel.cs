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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public class CustomTagItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string? PosterUrl { get; set; }
    }

    public partial class MoviesViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pageTitle = "فیلم و سریال ها";

        [ObservableProperty]
        private bool _showFilters = true;

        [ObservableProperty]
        private string _searchQuery = string.Empty;
        
        private System.Threading.CancellationTokenSource? _searchCts;
        
        [ObservableProperty]
        private bool _isGridView = true;

        public bool IsListView => !IsGridView;

        partial void OnIsGridViewChanged(bool value)
        {
            OnPropertyChanged(nameof(IsListView));
            var settings = SettingsManager.LoadSettings();
            settings.IsGridView = value;
            SettingsManager.SaveSettings(settings);
        }

        [RelayCommand]
        private void ToggleViewMode()
        {
            IsGridView = !IsGridView;
        }

        // Hidden Items Toggle (Eye Button)
        [ObservableProperty]
        private bool _showHiddenItems = false;

        partial void OnShowHiddenItemsChanged(bool value)
        {
            _ = LoadMoviesAsync();
        }

        [RelayCommand]
        private void ToggleShowHidden()
        {
            ShowHiddenItems = !ShowHiddenItems;
        }

        // Category Tabs: 0: همه, 1: فیلم‌ها, 2: سریال‌ها, 3: علاقه‌مندی‌ها, 4: دسته‌بندی‌ها
        [ObservableProperty]
        private int _selectedCategoryTabIndex = 0;

        public bool IsWatchSubFilterVisible => SelectedCategoryTabIndex != 4;

        partial void OnSelectedCategoryTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsWatchSubFilterVisible));
            SelectedCustomTag = null;
            SaveAndLoad();
        }

        // Custom Tag Filter inside Category Tab
        [ObservableProperty]
        private string? _selectedCustomTag;

        partial void OnSelectedCustomTagChanged(string? value)
        {
            _ = LoadMoviesAsync();
        }

        [RelayCommand]
        private void SelectCustomTag(string tag)
        {
            SelectedCustomTag = tag;
        }

        public bool HasBackButton => !string.IsNullOrEmpty(SelectedCustomTag) || !string.IsNullOrEmpty(PersonFilterName) || !string.IsNullOrEmpty(CollectionFilter);

        [RelayCommand]
        private void ClearCustomTagFilter()
        {
            if (!string.IsNullOrEmpty(PersonFilterName))
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new PeopleViewModel(PersonFilterType ?? "Actor")));
                return;
            }

            if (!string.IsNullOrEmpty(CollectionFilter))
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new CollectionsViewModel()));
                return;
            }

            SelectedCustomTag = null;
            OnPropertyChanged(nameof(HasBackButton));
        }

        // Watch Sub-Filter Tabs: 0: همه, 1: تماشا شده, 2: تماشا نشده
        [ObservableProperty]
        private int _selectedWatchTabIndex = 0;

        partial void OnSelectedWatchTabIndexChanged(int value) => SaveAndLoad();

        // Dynamic Counts
        [ObservableProperty]
        private int _allCount = 0;

        [ObservableProperty]
        private int _moviesCount = 0;

        [ObservableProperty]
        private int _seriesCount = 0;

        [ObservableProperty]
        private int _favoritesCount = 0;

        [ObservableProperty]
        private int _categoriesCount = 0;

        [ObservableProperty]
        private int _listFilterIndex = 0; // 0: All, 1: Favorites, 2: Watchlist

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
        private double _scrollPosition = 0;

        public int LastClickedIndex { get; set; } = -1;

        protected bool _disableSaveSettings = false;

        private void SaveAndLoad()
        {
            if (!_disableSaveSettings)
            {
                var settings = SettingsManager.LoadSettings();
                settings.SortIndex = SortIndex;
                settings.SortDirectionIndex = SortDirectionIndex;
                settings.SelectedGenreIndex = SelectedGenreIndex;
                settings.IsGridView = IsGridView;
                SettingsManager.SaveSettings(settings);
            }
            
            _ = LoadMoviesAsync();
        }

        public string PersonFilterName { get; set; } = string.Empty;
        public string PersonFilterType { get; set; } = string.Empty; // "Actor" or "Director"
        public string CollectionFilter { get; set; } = string.Empty;

        [ObservableProperty]
        private int _posterSize = 220; // Default width 220
        public int PosterHeight => (int)(PosterSize * 1.5);
        public int CardTotalWidth => PosterSize + 16;
        public int CardTotalHeight => PosterHeight + 92;

        partial void OnPosterSizeChanged(int value)
        {
            OnPropertyChanged(nameof(PosterHeight));
            OnPropertyChanged(nameof(CardTotalWidth));
            OnPropertyChanged(nameof(CardTotalHeight));
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

        [ObservableProperty]
        private int _bulkActionCurrentCount = 0;

        [ObservableProperty]
        private int _bulkActionTotalCount = 0;

        [ObservableProperty]
        private string _bulkActionPercentText = "0%";

        [ObservableProperty]
        private string _bulkActionCountText = "";

        private System.Threading.CancellationTokenSource? _bulkActionCts;

        [RelayCommand]
        private void CancelBulkAction()
        {
            _bulkActionCts?.Cancel();
        }

        public ObservableCollection<string> SearchHistory { get; } = new();
        public ObservableCollection<GalleryItemViewModel> Movies { get; } = new();
        public ObservableCollection<CustomTagItem> CustomTags { get; } = new();
        public ObservableCollection<string> Genres { get; } = new();

        // Manage Tags Modal Dialog State
        [ObservableProperty]
        private bool _isManageTagsDialogOpen = false;

        [ObservableProperty]
        private GalleryItemViewModel? _currentMediaForTags;

        [ObservableProperty]
        private string _tagsInputText = string.Empty;

        public MoviesViewModel()
        {
            LoadSearchHistory();
            var settings = SettingsManager.LoadSettings();
            PosterSize = settings.PosterSize > 50 ? settings.PosterSize : 220;
            _isGridView = settings.IsGridView;
            _sortIndex = settings.SortIndex;
            _sortDirectionIndex = settings.SortDirectionIndex;
            _selectedGenreIndex = settings.SelectedGenreIndex;
            
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
                        .Select(GenreTranslatorService.Translate)
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
            CustomTags.Clear();
            
            try
            {
                var (grouped, allCnt, movCnt, serCnt, favCnt, tagsList) = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var allDbFiles = db.VideoFiles.ToList();

                    // Total distinct items (Non-hidden by default, unless ShowHiddenItems is true)
                    var visibleDbFiles = ShowHiddenItems ? allDbFiles : allDbFiles.Where(v => !v.IsHidden).ToList();

                    var allDistinct = visibleDbFiles
                        .GroupBy(v => new { Title = (v.FormattedTitle ?? "ناشناس").ToLowerInvariant(), Type = v.MediaType })
                        .ToList();

                    int cAll = allDistinct.Count;
                    int cMov = allDistinct.Count(g => g.Key.Type == "Movie");
                    int cSer = allDistinct.Count(g => g.Key.Type == "Series");
                    int cFav = allDistinct.Count(g => g.Any(v => v.IsFavorite));

                    // Extract Custom Tags from DB
                    var tagsWithCounts = visibleDbFiles
                        .Where(v => !string.IsNullOrWhiteSpace(v.CustomTags))
                        .SelectMany(v => v.CustomTags!.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => new { Tag = t.Trim(), Poster = v.PosterUrl }))
                        .Where(x => !string.IsNullOrWhiteSpace(x.Tag))
                        .GroupBy(x => x.Tag)
                        .Select(g => new CustomTagItem
                        {
                            Name = g.Key,
                            Count = g.Count(),
                            PosterUrl = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.Poster))?.Poster
                        })
                        .OrderBy(t => t.Name)
                        .ToList();

                    var query = db.VideoFiles.AsQueryable();

                    // Filter hidden items
                    if (!ShowHiddenItems)
                    {
                        query = query.Where(v => !v.IsHidden);
                    }

                    // Search Filter
                    if (!string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        var searchTerms = SearchQuery.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (searchTerms.Length > 0)
                        {
                            foreach (var term in searchTerms)
                            {
                                string term1 = term.Replace("ی", "ي").Replace("ک", "ك");
                                string term2 = term.Replace("ي", "ی").Replace("ك", "ک");
                                
                                query = query.Where(v => 
                                    (v.FormattedTitle != null && (v.FormattedTitle.ToLower().Contains(term1) || v.FormattedTitle.ToLower().Contains(term2))) ||
                                    (v.CollectionName != null && (v.CollectionName.ToLower().Contains(term1) || v.CollectionName.ToLower().Contains(term2)))
                                );
                            }
                        }
                    }

                    // Category Tab Filter
                    if (SelectedCategoryTabIndex == 1) query = query.Where(v => v.MediaType == "Movie");
                    else if (SelectedCategoryTabIndex == 2) query = query.Where(v => v.MediaType == "Series");
                    else if (SelectedCategoryTabIndex == 3) query = query.Where(v => v.IsFavorite);
                    else if (SelectedCategoryTabIndex == 4 && !string.IsNullOrWhiteSpace(SelectedCustomTag))
                    {
                        query = query.Where(v => v.CustomTags != null && v.CustomTags.Contains(SelectedCustomTag));
                    }

                    // Watch Sub-Filter (0: All, 1: Watched, 2: Unwatched)
                    if (SelectedWatchTabIndex == 1) query = query.Where(v => v.IsWatched);
                    else if (SelectedWatchTabIndex == 2) query = query.Where(v => !v.IsWatched);

                    // Genre Dropdown Filter
                    string? selectedGenre = (SelectedGenreIndex > 0 && SelectedGenreIndex < Genres.Count) ? Genres[SelectedGenreIndex] : null;

                    var allFiles = query.ToList();

                    if (!string.IsNullOrWhiteSpace(selectedGenre))
                    {
                        allFiles = allFiles.Where(v => GenreTranslatorService.MatchesGenre(v.Genres, selectedGenre)).ToList();
                    }

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

                    var resultList = allFiles
                        .GroupBy(v => new { Title = (v.FormattedTitle ?? "ناشناس").ToLowerInvariant(), Type = v.MediaType })
                        .Select(g => 
                        {
                            var first = g.First();
                            if (g.Key.Type == "Series")
                            {
                                first.NumberOfEpisodes = g.Count();
                                first.NumberOfSeasons = g.Select(x => x.Season).Distinct().Count(s => s != null);
                                first.IsWatched = g.All(x => x.IsWatched);
                            }
                            first.IsFavorite = g.Any(x => x.IsFavorite);
                            first.IsWatchlist = g.Any(x => x.IsWatchlist);
                            first.IsHidden = g.Any(x => x.IsHidden);
                            return new GalleryItemViewModel(first, UpdateSelectionState, async (item) => await ToggleFavoriteAsync(item), (item) => OpenManageTags(item));
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

                    return (resultList.ToList(), cAll, cMov, cSer, cFav, tagsWithCounts);
                });

                AllCount = allCnt;
                MoviesCount = movCnt;
                SeriesCount = serCnt;
                FavoritesCount = favCnt;
                CategoriesCount = tagsList.Count;

                foreach (var t in tagsList)
                {
                    CustomTags.Add(t);
                }

                foreach (var m in grouped)
                {
                    Movies.Add(m);
                }
            }
            catch { }
            finally
            {
                HasNoMovies = (SelectedCategoryTabIndex == 4 && SelectedCustomTag == null) ? CustomTags.Count == 0 : Movies.Count == 0;
                IsLoading = false;
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                await Task.Delay(350, token);
                if (!token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _ = LoadMoviesAsync();
                    });
                }
            }, token).ContinueWith(t => { }, TaskContinuationOptions.OnlyOnCanceled);
        }

        private void UpdateSelectionState()
        {
            SelectedCount = Movies.Count(m => m.IsSelected);
            IsInSelectionMode = SelectedCount > 0;
        }

        [RelayCommand]
        public void CardClicked(GalleryItemViewModel item)
        {
            if (item == null) return;
            if (IsInSelectionMode)
            {
                item.IsSelected = !item.IsSelected;
                UpdateSelectionState();
            }
            else
            {
                OpenDetails(item);
            }
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
        public void ToggleSelection(GalleryItemViewModel item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
            UpdateSelectionState();
        }

        // ==========================================
        // MANAGE TAGS (برچسب‌ها / دسته‌بندی‌ها)
        // ==========================================
        [RelayCommand]
        public void OpenManageTags(GalleryItemViewModel item)
        {
            if (item == null) return;
            CurrentMediaForTags = item;
            TagsInputText = item.File.CustomTags ?? string.Empty;
            IsManageTagsDialogOpen = true;
        }

        [RelayCommand]
        public void OpenManageTagsForSelected()
        {
            var selected = Movies.Where(m => m.IsSelected).ToList();
            if (selected.Count == 0) return;
            CurrentMediaForTags = selected.First();
            TagsInputText = CurrentMediaForTags.File.CustomTags ?? string.Empty;
            IsManageTagsDialogOpen = true;
        }

        [RelayCommand]
        public void CloseTagsDialog()
        {
            IsManageTagsDialogOpen = false;
            CurrentMediaForTags = null;
        }

        [RelayCommand]
        public async Task SaveTagsAsync()
        {
            string tags = TagsInputText?.Trim() ?? string.Empty;
            var targets = Movies.Where(m => m.IsSelected).ToList();
            if (targets.Count == 0 && CurrentMediaForTags != null)
            {
                targets.Add(CurrentMediaForTags);
            }

            if (targets.Count > 0)
            {
                var titles = targets.Select(t => t.File.FormattedTitle).Distinct().ToList();
                var ids = targets.Select(t => t.File.Id).ToList();

                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var list = db.VideoFiles.Where(v => titles.Contains(v.FormattedTitle) || ids.Contains(v.Id)).ToList();
                    foreach (var item in list)
                    {
                        item.CustomTags = tags;
                    }
                    db.SaveChanges();
                });

                foreach (var t in targets)
                {
                    t.File.CustomTags = tags;
                    t.NotifyFileChanged();
                }
            }

            CloseTagsDialog();
            ExitSelectionMode();
            await LoadMoviesAsync();
        }

        // ==========================================
        // ITEM CONTEXT MENU ACTIONS (کلیک راست)
        // ==========================================
        [RelayCommand]
        public async Task ToggleFavoriteItemAsync(GalleryItemViewModel item)
        {
            if (item == null) return;
            await item.ToggleFavoriteAsync();
            await LoadMoviesAsync();
        }

        [RelayCommand]
        public async Task ToggleWatchedItemAsync(GalleryItemViewModel item)
        {
            if (item == null) return;
            await item.ToggleWatchedAsync();
            await LoadMoviesAsync();
        }

        [RelayCommand]
        public async Task ToggleHiddenItemAsync(GalleryItemViewModel item)
        {
            if (item == null) return;
            await item.ToggleHiddenAsync();
            await LoadMoviesAsync();
        }

        [RelayCommand]
        public async Task RefreshItemAsync(GalleryItemViewModel item)
        {
            if (item == null || item.File == null) return;
            try
            {
                item.IsUpdating = true;
                ToastService.Instance.ShowInfo($"در حال به‌روزرسانی «{item.File.FormattedTitle}» از TMDb...");
                var identifyService = new IdentifyMediaService();
                var updated = await identifyService.IdentifyMediaAsync(item.File);

                using (var db = new AppDbContext())
                {
                    var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle == item.File.FormattedTitle || v.Id == item.File.Id).ToList();
                    foreach (var dbFile in dbFiles)
                    {
                        dbFile.TmdbId = updated.TmdbId;
                        dbFile.PosterUrl = updated.PosterUrl;
                        dbFile.BackdropUrl = updated.BackdropUrl;
                        dbFile.Rating = updated.Rating;
                        dbFile.Overview = updated.Overview;
                        dbFile.Genres = updated.Genres;
                        dbFile.Actors = updated.Actors;
                        dbFile.Director = updated.Director;
                        dbFile.Year = updated.Year;
                        dbFile.FirstAirDate = updated.FirstAirDate;
                        dbFile.LastAirDate = updated.LastAirDate;
                        dbFile.NetworkName = updated.NetworkName;
                        dbFile.AirDay = updated.AirDay;
                        dbFile.AirTime = updated.AirTime;
                        dbFile.TotalSeasonsCount = updated.TotalSeasonsCount ?? updated.NumberOfSeasons;
                        dbFile.TotalEpisodesCount = updated.TotalEpisodesCount ?? updated.NumberOfEpisodes;
                        dbFile.NumberOfSeasons = updated.NumberOfSeasons ?? updated.TotalSeasonsCount;
                        dbFile.NumberOfEpisodes = updated.NumberOfEpisodes ?? updated.TotalEpisodesCount;
                        dbFile.NextEpisodeDate = updated.NextEpisodeDate;
                        dbFile.NextEpisodeSeason = updated.NextEpisodeSeason;
                        dbFile.NextEpisodeNumber = updated.NextEpisodeNumber;
                        dbFile.SeriesStatus = updated.SeriesStatus;
                        dbFile.CollectionName = updated.CollectionName;
                        dbFile.IsIdentified = true;
                    }
                    await db.SaveChangesAsync();

                    if (updated.MediaType == "Series" && updated.TmdbId.HasValue)
                    {
                        var (sList, eList) = await identifyService.FetchSeriesDetailsAsync(updated.TmdbId.Value);
                        if (sList.Count > 0)
                        {
                            var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == updated.TmdbId.Value).ToList();
                            var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == updated.TmdbId.Value).ToList();
                            db.TvSeasons.RemoveRange(oldS);
                            db.TvEpisodes.RemoveRange(oldE);
                            db.TvSeasons.AddRange(sList);
                            db.TvEpisodes.AddRange(eList);
                            await db.SaveChangesAsync();
                        }
                    }
                }

                item.NotifyFileChanged();
                ToastService.Instance.ShowSuccess($"«{item.File.FormattedTitle}» با موفقیت به‌روزرسانی شد.");
                await LoadMoviesAsync();
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در به‌روزرسانی: {ex.Message}");
            }
            finally
            {
                item.IsUpdating = false;
            }
        }

        [RelayCommand]
        public void PlayItem(GalleryItemViewModel item)
        {
            if (item == null || item.File == null) return;
            PlaybackService.PlayMedia(item.File);
        }

        [RelayCommand]
        public async Task DeleteItemAsync(GalleryItemViewModel item)
        {
            if (item == null) return;
            var result = MessageBox.Show($"آیا از حذف «{item.File.FormattedTitle}» از کتابخانه اطمینان دارید؟", "حذف از کتابخانه", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var list = db.VideoFiles.Where(v => v.FormattedTitle == item.File.FormattedTitle || v.Id == item.File.Id).ToList();
                    db.VideoFiles.RemoveRange(list);
                    db.SaveChanges();
                });
                await LoadMoviesAsync();
            }
        }

        private async Task ToggleFavoriteAsync(GalleryItemViewModel item)
        {
            // Handled inside item command
            await Task.CompletedTask;
        }

        // ==========================================
        // BULK SELECTION ACTIONS
        // ==========================================
        [RelayCommand]
        private void SelectAll()
        {
            bool allSelected = Movies.All(m => m.IsSelected);
            foreach (var m in Movies) m.IsSelected = !allSelected;
            UpdateSelectionState();
        }

        [RelayCommand]
        private void ExitSelectionMode()
        {
            foreach (var m in Movies) m.IsSelected = false;
            UpdateSelectionState();
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var selected = Movies.Where(m => m.IsSelected).ToList();
            if (selected.Count == 0) return;

            var result = MessageBox.Show($"آیا از حذف {selected.Count} مورد انتخاب شده اطمینان دارید؟", "حذف گروهی", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await RunBulkActionAsync("حذف", (db, f) => db.VideoFiles.Remove(f));
                ExitSelectionMode();
            }
        }

        [RelayCommand]
        private async Task ToggleFavoritesSelectedAsync()
        {
            await RunBulkActionAsync("تغییر علاقه‌مندی", (db, f) => f.IsFavorite = !f.IsFavorite);
        }

        [RelayCommand]
        private async Task ToggleWatchedSelectedAsync()
        {
            await RunBulkActionAsync("وضعیت مشاهده", (db, f) => f.IsWatched = !f.IsWatched);
        }

        [RelayCommand]
        private async Task ToggleHiddenSelectedAsync()
        {
            await RunBulkActionAsync("مخفی‌سازی", (db, f) => f.IsHidden = !f.IsHidden);
        }

        [RelayCommand]
        private async Task RefreshSelectedAsync()
        {
            var selected = Movies.Where(m => m.IsSelected).ToList();
            if (selected.Count == 0) return;

            int total = selected.Count;
            BulkActionTotalCount = total;
            BulkActionCurrentCount = 0;
            BulkActionProgress = 0;
            BulkActionPercentText = "0%";
            BulkActionCountText = $"0 از {total}";
            BulkActionText = "در حال شروع به‌روزرسانی...";
            IsBulkActionRunning = true;

            _bulkActionCts = new System.Threading.CancellationTokenSource();
            var token = _bulkActionCts.Token;

            try
            {
                int current = 0;
                var identifyService = new IdentifyMediaService();

                for (int i = 0; i < total; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var item = selected[i];
                    BulkActionCurrentCount = i + 1;
                    BulkActionProgress = ((double)(i + 1) / total) * 100;
                    BulkActionPercentText = $"{Math.Round(BulkActionProgress)}%";
                    BulkActionCountText = $"{i + 1} از {total}";
                    BulkActionText = $"در حال دریافت متادیتا برای «{item.File.FormattedTitle}»";

                    try
                    {
                        var updated = await identifyService.IdentifyMediaAsync(item.File);
                        using var db = new AppDbContext();
                        var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle == item.File.FormattedTitle || v.Id == item.File.Id).ToList();
                        foreach (var dbFile in dbFiles)
                        {
                            dbFile.TmdbId = updated.TmdbId;
                            dbFile.PosterUrl = updated.PosterUrl;
                            dbFile.BackdropUrl = updated.BackdropUrl;
                            dbFile.Rating = updated.Rating;
                            dbFile.Overview = updated.Overview;
                            dbFile.Genres = updated.Genres;
                            dbFile.Actors = updated.Actors;
                            dbFile.Director = updated.Director;
                            dbFile.Year = updated.Year;
                            dbFile.FirstAirDate = updated.FirstAirDate;
                            dbFile.LastAirDate = updated.LastAirDate;
                            dbFile.NetworkName = updated.NetworkName;
                            dbFile.AirDay = updated.AirDay;
                            dbFile.AirTime = updated.AirTime;
                            dbFile.TotalSeasonsCount = updated.TotalSeasonsCount ?? updated.NumberOfSeasons;
                            dbFile.TotalEpisodesCount = updated.TotalEpisodesCount ?? updated.NumberOfEpisodes;
                            dbFile.NumberOfSeasons = updated.NumberOfSeasons ?? updated.TotalSeasonsCount;
                            dbFile.NumberOfEpisodes = updated.NumberOfEpisodes ?? updated.TotalEpisodesCount;
                            dbFile.NextEpisodeDate = updated.NextEpisodeDate;
                            dbFile.NextEpisodeSeason = updated.NextEpisodeSeason;
                            dbFile.NextEpisodeNumber = updated.NextEpisodeNumber;
                            dbFile.SeriesStatus = updated.SeriesStatus;
                            dbFile.CollectionName = updated.CollectionName;
                            dbFile.IsIdentified = true;
                        }
                        await db.SaveChangesAsync();

                        if (updated.MediaType == "Series" && updated.TmdbId.HasValue)
                        {
                            var (sList, eList) = await identifyService.FetchSeriesDetailsAsync(updated.TmdbId.Value);
                            if (sList.Count > 0)
                            {
                                var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == updated.TmdbId.Value).ToList();
                                var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == updated.TmdbId.Value).ToList();
                                db.TvSeasons.RemoveRange(oldS);
                                db.TvEpisodes.RemoveRange(oldE);
                                db.TvSeasons.AddRange(sList);
                                db.TvEpisodes.AddRange(eList);
                                await db.SaveChangesAsync();
                            }
                        }
                    }
                    catch { }

                    current++;
                }

                if (!token.IsCancellationRequested)
                {
                    ToastService.Instance.ShowSuccess($"{current} عنوان با موفقیت از TMDb به‌روزرسانی شدند.");
                }
                else
                {
                    ToastService.Instance.ShowInfo($"عملیات به‌روزرسانی متوقف شد ({current} مورد انجام شد).");
                }
            }
            finally
            {
                IsBulkActionRunning = false;
                ExitSelectionMode();
                await LoadMoviesAsync();
            }
        }

        private async Task RunBulkActionAsync(string actionName, Action<AppDbContext, VideoFile> action)
        {
            var selected = Movies.Where(m => m.IsSelected).ToList();
            if (selected.Count == 0) return;

            IsBulkActionRunning = true;
            BulkActionProgress = 0;
            BulkActionText = $"در حال انجام {actionName}...";

            try
            {
                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var selectedIds = selected.Select(s => s.File.Id).ToList();
                    var dbFiles = db.VideoFiles.Where(v => selectedIds.Contains(v.Id)).ToList();

                    int total = dbFiles.Count;
                    for (int i = 0; i < total; i++)
                    {
                        action(db, dbFiles[i]);
                    }
                    db.SaveChanges();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در انجام عملیات گروهی: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBulkActionRunning = false;
                await LoadMoviesAsync();
            }
        }

        private void LoadSearchHistory()
        {
            // Search history loading
        }
    }
}
