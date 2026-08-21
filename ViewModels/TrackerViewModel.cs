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
    public partial class TrackerItemViewModel : ObservableObject
    {
        public VideoFile Media { get; }

        public int Id => Media.Id;
        public string Title => string.IsNullOrWhiteSpace(Media.FormattedTitle) ? (Media.FileName ?? "بدون عنوان") : Media.FormattedTitle;
        public string? PosterUrl => Media.PosterUrl;
        public string? BackdropUrl => Media.BackdropUrl;
        public string FormattedYear
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Media.Year))
                    return DateTimeFormatterService.FormatYear(Media.Year);
                if (Media.FirstAirDate.HasValue)
                    return DateTimeFormatterService.FormatYear(Media.FirstAirDate.Value.Year.ToString());
                return string.Empty;
            }
        }
        public string FormattedGenres => GenreTranslatorService.TranslateList(Media.Genres);
        public string YearAndGenres
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FormattedYear)) parts.Add(FormattedYear);
                if (!string.IsNullOrWhiteSpace(FormattedGenres)) parts.Add(FormattedGenres);
                return parts.Count > 0 ? string.Join(" • ", parts) : "نامشخص";
            }
        }
        public string MediaType => Media.MediaType ?? "Movie";
        public bool IsSeries => MediaType.Equals("Series", StringComparison.OrdinalIgnoreCase);
        public string MediaTypeDisplay => IsSeries ? "سریال" : "فیلم سینمایی";

        [ObservableProperty]
        private bool _isSelected;

        public Action? OnSelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            OnSelectionChanged?.Invoke();
        }

        public string FormattedStatus
        {
            get
            {
                if (string.IsNullOrEmpty(Media.SeriesStatus)) return IsSeries ? "سریال" : "فیلم";
                string s = Media.SeriesStatus.ToLower();
                if (s.Contains("returning") || s.Contains("airing")) return "در حال پخش";
                if (s.Contains("ended") || s.Contains("finished")) return "تمام شده";
                if (s.Contains("cancel")) return "کنسل شده";
                if (s.Contains("planned")) return "برنامه‌ریزی شده";
                return Media.SeriesStatus;
            }
        }

        public string StatusColor
        {
            get
            {
                string s = (Media.SeriesStatus ?? "").ToLower();
                if (s.Contains("returning") || s.Contains("airing")) return "#2ED573";
                if (s.Contains("ended") || s.Contains("finished")) return "#A4B0BE";
                if (s.Contains("cancel")) return "#FF4757";
                return "#00D2D3";
            }
        }

        public string NextEpisodeInfo
        {
            get
            {
                if (string.IsNullOrEmpty(Media.NextEpisodeDate)) return "";
                string formattedDate = DateTimeFormatterService.FormatDate(Media.NextEpisodeDate);
                if (Media.NextEpisodeNumber.HasValue)
                    return $"قسمت {Media.NextEpisodeNumber} - {formattedDate}";
                return $"پخش بعدی: {formattedDate}";
            }
        }

        public string SeasonsEpisodesCount
        {
            get
            {
                int s = Media.TotalSeasonsCount ?? Media.NumberOfSeasons ?? 0;
                int e = Media.TotalEpisodesCount ?? Media.NumberOfEpisodes ?? 0;
                if (s > 0 && e > 0) return $"{s} فصل • {e} قسمت";
                if (s > 0) return $"{s} فصل";
                return "";
            }
        }

        public TrackerViewModel? ParentViewModel { get; set; }

        public TrackerItemViewModel(VideoFile media, TrackerViewModel? parent = null)
        {
            Media = media;
            ParentViewModel = parent;
        }

        [RelayCommand]
        public void ToggleSelection()
        {
            IsSelected = !IsSelected;
        }

        [RelayCommand]
        public void HandleCardClick()
        {
            if (ParentViewModel != null && ParentViewModel.IsSelectionMode)
            {
                IsSelected = !IsSelected;
            }
            else
            {
                OpenDetails();
            }
        }

        [RelayCommand]
        public void HandleCardRightClick()
        {
            IsSelected = !IsSelected;
        }

        [RelayCommand]
        public void OpenDetails()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new TrackedDetailViewModel(Media)));
        }
    }

    public partial class TrackerViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        // 0: همه, 1: سریال‌ها, 2: فیلم‌ها, 3: در حال پخش, 4: پایان‌یافته
        [ObservableProperty]
        private int _selectedFilterIndex = 0;

        [ObservableProperty]
        private int _trackedCount = 0;

        // Selection properties
        [ObservableProperty]
        private bool _isSelectionMode;

        [ObservableProperty]
        private int _selectedCount;

        private List<TrackerItemViewModel> _allTrackedItems = new();

        public ObservableCollection<TrackerItemViewModel> FilteredItems { get; } = new();

        public TrackerViewModel()
        {
            _ = LoadTrackerDataAsync();
        }

        partial void OnSelectedFilterIndexChanged(int value) => ApplyFilters();
        partial void OnSearchQueryChanged(string value) => ApplyFilters();

        [RelayCommand]
        public async Task LoadTrackerDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var trackedList = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    
                    // ONLY load items that have been explicitly added to tracker
                    var dbTracked = db.VideoFiles
                        .Where(v => v.IsTracked || v.FilePath == "[Manual Tracker]")
                        .ToList();

                    // Group by Title and Type to avoid duplicates
                    var distinct = dbTracked
                        .GroupBy(v => new { Title = (v.FormattedTitle ?? v.FileName ?? "").Trim().ToLowerInvariant(), Type = v.MediaType })
                        .Select(g => g.First())
                        .OrderByDescending(v => v.DateAdded)
                        .Select(v => new TrackerItemViewModel(v))
                        .ToList();

                    return distinct;
                });

                _allTrackedItems = trackedList;
                foreach (var item in _allTrackedItems)
                {
                    item.ParentViewModel = this;
                    item.OnSelectionChanged = UpdateSelectionState;
                }

                TrackedCount = _allTrackedItems.Count;
                UpdateSelectionState();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error loading tracked media", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateSelectionState()
        {
            SelectedCount = FilteredItems.Count(x => x.IsSelected);
            IsSelectionMode = SelectedCount > 0;
        }

        private void ApplyFilters()
        {
            FilteredItems.Clear();
            var filtered = _allTrackedItems.AsEnumerable();

            // Category Filter
            if (SelectedFilterIndex == 1) // سریال‌ها
                filtered = filtered.Where(x => x.IsSeries);
            else if (SelectedFilterIndex == 2) // فیلم‌ها
                filtered = filtered.Where(x => !x.IsSeries);
            else if (SelectedFilterIndex == 3) // در حال پخش
                filtered = filtered.Where(x => x.FormattedStatus == "در حال پخش");
            else if (SelectedFilterIndex == 4) // پایان یافته
                filtered = filtered.Where(x => x.FormattedStatus == "تمام شده");

            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.Trim().ToLowerInvariant();
                filtered = filtered.Where(x => x.Title.ToLowerInvariant().Contains(q) || x.FormattedGenres.ToLowerInvariant().Contains(q));
            }

            foreach (var item in filtered)
            {
                FilteredItems.Add(item);
            }

            UpdateSelectionState();
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = true;
            }
            UpdateSelectionState();
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var item in _allTrackedItems)
            {
                item.IsSelected = false;
            }
            UpdateSelectionState();
        }

        [RelayCommand]
        public async Task DeleteSelectedAsync()
        {
            var selectedItems = FilteredItems.Where(x => x.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            var result = System.Windows.MessageBox.Show(
                $"آیا مطمئن هستید که می‌خواهید {selectedItems.Count} عنوان انتخاب‌شده را از ردیاب حذف کنید؟",
                "تأیید حذف گروهی از ردیاب",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();
                    var ids = selectedItems.Select(x => x.Id).ToList();
                    var dbItems = db.VideoFiles.Where(v => ids.Contains(v.Id)).ToList();

                    foreach (var dbItem in dbItems)
                    {
                        if (dbItem.FilePath == "[Manual Tracker]" || string.IsNullOrEmpty(dbItem.FilePath))
                        {
                            db.VideoFiles.Remove(dbItem);
                        }
                        else
                        {
                            dbItem.IsTracked = false;
                            dbItem.IsWatchlist = false;
                        }
                    }
                    await db.SaveChangesAsync();
                });

                ToastService.Instance.ShowSuccess($"{selectedItems.Count} عنوان از ردیاب حذف شد.");
                await LoadTrackerDataAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error bulk deleting tracked items", ex);
                ToastService.Instance.ShowError("خطا در حذف گروهی از ردیاب.");
            }
        }

        [RelayCommand]
        public async Task RefreshSelectedFromTmdbAsync()
        {
            var selectedItems = FilteredItems.Where(x => x.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            ToastService.Instance.ShowInfo($"در حال بروزرسانی {selectedItems.Count} عنوان از TMDb...");
            try
            {
                var identifySvc = new IdentifyMediaService();
                await Task.Run(async () =>
                {
                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            await identifySvc.UpdateSeriesStatusAsync(item.Media);
                            if (item.Media.TmdbId.HasValue && item.IsSeries)
                            {
                                var (sList, eList) = await identifySvc.FetchSeriesDetailsAsync(item.Media.TmdbId.Value);
                                if (sList.Count > 0)
                                {
                                    using var db = new AppDbContext();
                                    var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == item.Media.TmdbId.Value).ToList();
                                    var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == item.Media.TmdbId.Value).ToList();
                                    db.TvSeasons.RemoveRange(oldS);
                                    db.TvEpisodes.RemoveRange(oldE);
                                    db.TvSeasons.AddRange(sList);
                                    db.TvEpisodes.AddRange(eList);
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                        catch { }
                    }
                });

                ToastService.Instance.ShowSuccess("عناوین انتخاب‌شده با موفقیت از TMDb بروزرسانی شدند.");
                await LoadTrackerDataAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error refreshing selected items from TMDb", ex);
                ToastService.Instance.ShowError("خطا در بروزرسانی از TMDb.");
            }
        }

        [RelayCommand]
        public async Task RemoveItemAsync(TrackerItemViewModel item)
        {
            if (item == null) return;
            var result = System.Windows.MessageBox.Show(
                $"آیا می‌خواهید «{item.Title}» را از ردیاب حذف کنید؟",
                "حذف از ردیاب",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();
                    var dbItem = db.VideoFiles.FirstOrDefault(v => v.Id == item.Id);
                    if (dbItem != null)
                    {
                        if (dbItem.FilePath == "[Manual Tracker]" || string.IsNullOrEmpty(dbItem.FilePath))
                        {
                            db.VideoFiles.Remove(dbItem);
                        }
                        else
                        {
                            dbItem.IsTracked = false;
                            dbItem.IsWatchlist = false;
                        }
                        await db.SaveChangesAsync();
                    }
                });

                ToastService.Instance.ShowSuccess($"«{item.Title}» از ردیاب حذف شد.");
                await LoadTrackerDataAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error removing item", ex);
                ToastService.Instance.ShowError("خطا در حذف از ردیاب.");
            }
        }

        [RelayCommand]
        public async Task RefreshItemFromTmdbAsync(TrackerItemViewModel item)
        {
            if (item == null) return;
            ToastService.Instance.ShowInfo($"در حال بروزرسانی «{item.Title}» از TMDb...");

            try
            {
                var identifySvc = new IdentifyMediaService();
                await identifySvc.UpdateSeriesStatusAsync(item.Media);
                if (item.Media.TmdbId.HasValue && item.IsSeries)
                {
                    var (sList, eList) = await identifySvc.FetchSeriesDetailsAsync(item.Media.TmdbId.Value);
                    if (sList.Count > 0)
                    {
                        using var db = new AppDbContext();
                        var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == item.Media.TmdbId.Value).ToList();
                        var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == item.Media.TmdbId.Value).ToList();
                        db.TvSeasons.RemoveRange(oldS);
                        db.TvEpisodes.RemoveRange(oldE);
                        db.TvSeasons.AddRange(sList);
                        db.TvEpisodes.AddRange(eList);
                        await db.SaveChangesAsync();
                    }
                }
                ToastService.Instance.ShowSuccess($"«{item.Title}» با موفقیت بروزرسانی شد.");
                await LoadTrackerDataAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error refreshing single item", ex);
                ToastService.Instance.ShowError("خطا در بروزرسانی از TMDb.");
            }
        }

        [RelayCommand]
        private void GoToManualSearch()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ManualSearchViewModel("", returnToTracker: true)));
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
