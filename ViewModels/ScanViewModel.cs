using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ScanViewModel : ObservableObject
    {
        private readonly ScannerService _scannerService;
        private readonly IdentifyMediaService _identifyService;
        private CancellationTokenSource? _cancellationTokenSource;
        private List<ScannedGroupViewModel> _allGroups = new();

        [ObservableProperty]
        private string _selectedDirectory = string.Empty;
        
        [ObservableProperty]
        private bool _isScanning;
        
        [ObservableProperty]
        private bool _isRegistering;
        
        [ObservableProperty]
        private Visibility _startButtonVisibility = Visibility.Visible;
        
        [ObservableProperty]
        private Visibility _stopButtonVisibility = Visibility.Collapsed;
        
        [ObservableProperty]
        private Visibility _registerButtonVisibility = Visibility.Collapsed;
        
        [ObservableProperty]
        private string _statusMessage = "آماده برای اسکن...";
        
        // Progress
        [ObservableProperty]
        private string _scanProgressText = "";
        
        [ObservableProperty]
        private double _scanProgressValue = 0;
        
        [ObservableProperty]
        private bool _isScanningIndeterminate = true;
        
        // Bulk Toolbar
        [ObservableProperty]
        private Visibility _bulkToolbarVisibility = Visibility.Collapsed;
        
        [ObservableProperty]
        private bool _hasSelection = false;
        
        [ObservableProperty]
        private string _selectedCountText = "0 مورد انتخاب شد";
        
        // Search
        [ObservableProperty]
        private string _searchQuery = string.Empty;
        
        // Filters
        private string _selectedFilter = "همه";

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    foreach (var group in _allGroups)
                    {
                        group.IsChecked = value;
                    }
                    UpdateBulkToolbar();
                }
            }
        }

        public bool IsFilterAll
        {
            get => _selectedFilter == "همه";
            set { if (value) { _selectedFilter = "همه"; OnPropertyChanged(nameof(IsFilterAll)); ApplyFilters(); } }
        }
        public bool IsFilterRegistered
        {
            get => _selectedFilter == "ثبت شد";
            set { if (value) { _selectedFilter = "ثبت شد"; OnPropertyChanged(nameof(IsFilterRegistered)); ApplyFilters(); } }
        }
        public bool IsFilterError
        {
            get => _selectedFilter == "خطا در ثبت";
            set { if (value) { _selectedFilter = "خطا در ثبت"; OnPropertyChanged(nameof(IsFilterError)); ApplyFilters(); } }
        }
        public bool IsFilterPending
        {
            get => _selectedFilter == "آماده بررسی";
            set { if (value) { _selectedFilter = "آماده بررسی"; OnPropertyChanged(nameof(IsFilterPending)); ApplyFilters(); } }
        }

        public ObservableCollection<ScannedGroupViewModel> ScannedFilesView { get; } = new();

        public ScanViewModel()
        {
            _scannerService = new ScannerService();
            _identifyService = new IdentifyMediaService();
            
            if (Directory.Exists("D:\\"))
                SelectedDirectory = "D:\\";
            else
                SelectedDirectory = "C:\\";
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilters();

        public void UpdateBulkToolbar()
        {
            int count = _allGroups.Count(g => g.IsChecked);
            SelectedCountText = $"{count} مورد انتخاب شد";
            HasSelection = count > 0 && !IsScanning && !IsRegistering;
            BulkToolbarVisibility = HasSelection ? Visibility.Visible : Visibility.Collapsed;
            RegisterButtonVisibility = BulkToolbarVisibility; // Link to the new button they added
        }

        private void ApplyFilters()
        {
            var filtered = _allGroups.AsEnumerable();
            
            if (_selectedFilter == "ثبت شد")
                filtered = filtered.Where(g => g.IsRegistered);
            else if (_selectedFilter == "خطا در ثبت")
                filtered = filtered.Where(g => g.IsError);
            else if (_selectedFilter == "آماده بررسی")
                filtered = filtered.Where(g => !g.IsRegistered && !g.IsError);
            
            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLowerInvariant();
                filtered = filtered.Where(g => 
                    g.DisplayTitle.ToLowerInvariant().Contains(q) || 
                    (g.YearOverride?.Contains(q) ?? false));
            }
            
            ScannedFilesView.Clear();
            foreach (var g in filtered)
            {
                ScannedFilesView.Add(g);
            }
            
            UpdateBulkToolbar();
        }

        private void UpdateVisibility()
        {
            StartButtonVisibility = IsScanning || IsRegistering ? Visibility.Collapsed : Visibility.Visible;
            StopButtonVisibility = IsScanning || IsRegistering ? Visibility.Visible : Visibility.Collapsed;
            UpdateBulkToolbar();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "انتخاب پوشه یا درایو"
            };
            
            if (dialog.ShowDialog() == true)
            {
                SelectedDirectory = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (IsScanning || IsRegistering) return;
            
            if (!Directory.Exists(SelectedDirectory))
            {
                StatusMessage = "مسیر نامعتبر است!";
                return;
            }
            
            IsScanning = true;
            IsScanningIndeterminate = true;
            ScanProgressText = "در حال یافتن فایل‌ها...";
            _allGroups.Clear();
            ScannedFilesView.Clear();
            UpdateVisibility();
            
            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<string>(message =>
            {
                StatusMessage = message;
            });
            
            try
            {
                var files = await _scannerService.ScanDirectoryAsync(SelectedDirectory, progress, _cancellationTokenSource.Token);
                
                var groupedFileLists = MovieManagerDesktop.Services.SmartGroupingService.SmartGroupFiles(files);
                
                // Load existing series
                List<string> existingSeriesCache;
                using (var db = new AppDbContext())
                {
                    existingSeriesCache = db.VideoFiles
                        .Where(v => v.MediaType == "Series")
                        .Select(v => v.FormattedTitle)
                        .Distinct()
                        .ToList();
                }
                
                foreach(var fileList in groupedFileLists)
                {
                    var vm = new ScannedGroupViewModel(fileList, existingSeriesCache);
                    _allGroups.Add(vm);
                }
                
                SearchQuery = string.Empty;
                IsFilterAll = true; // This will call ApplyFilters
                
                // تمام آیتم‌ها به صورت پیش‌فرض تیک خورده هستند، پس تیکِ هدر هم باید فعال باشد
                _isAllSelected = true;
                OnPropertyChanged(nameof(IsAllSelected));
                
                ScanProgressText = $"{_allGroups.Count} گروه یافت شد";
                IsScanningIndeterminate = false;
                ScanProgressValue = 100;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "اسکن متوقف شد.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطای سیستمی: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                UpdateVisibility();
            }
        }

        [RelayCommand]
        private async Task OpenEditDialogAsync(ScannedGroupViewModel group)
        {
            if (group == null) return;
            
            var vm = new EditScannedGroupViewModel(group, this);
            var view = new EditScannedGroupDialog
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };
            vm.CloseAction = () => view.Close();
            view.ShowDialog();
        }

        [RelayCommand]
        private async Task OpenSelectExistingDialogAsync(ScannedGroupViewModel group)
        {
            if (group == null) return;
            
            var vm = new SelectExistingMediaViewModel(group, this);
            var view = new SelectExistingMediaDialog
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };
            vm.CloseAction = () => view.Close();
            view.ShowDialog();
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            var selected = _allGroups.Where(g => g.IsChecked).ToList();
            foreach (var s in selected)
            {
                _allGroups.Remove(s);
            }
            ApplyFilters();
        }

        [RelayCommand]
        private async Task RetryGroupAsync(ScannedGroupViewModel group)
        {
            if (IsRegistering || IsScanning) return;
            
            group.Status = "در حال جستجو...";
            group.IsError = false;
            
            var representative = group.Representative;
            representative.FormattedTitle = string.IsNullOrWhiteSpace(group.TitleOverride) ? representative.FormattedTitle : group.TitleOverride;
            representative.Year = string.IsNullOrWhiteSpace(group.YearOverride) ? null : group.YearOverride;
            
            string overrideId = group.IdOverride?.Trim() ?? "";
            if (!string.IsNullOrEmpty(overrideId))
            {
                if (overrideId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    int? tmdbId = await _identifyService.GetTmdbIdFromImdbIdAsync(overrideId);
                    if (tmdbId.HasValue) representative.TmdbId = tmdbId;
                    else representative.TmdbId = 0;
                }
                else if (int.TryParse(overrideId, out int tmdb))
                {
                    representative.TmdbId = tmdb;
                }
            }
            else
            {
                representative.TmdbId = null;
            }
            
            try
            {
                var identified = await _identifyService.IdentifyMediaAsync(representative);
                
                bool hasData = (identified.TmdbId.HasValue && identified.TmdbId > 0) || 
                               !string.IsNullOrWhiteSpace(identified.PosterUrl) || 
                               !string.IsNullOrWhiteSpace(identified.Overview);
                
                if (!hasData)
                {
                    group.Status = "خطا در پیدا کردن";
                    group.IsError = true;
                    group.IsChecked = false;
                    ApplyFilters();
                    string mediaTypeDisplay = representative.MediaType == "Series" ? "سریال" : "فیلم";
                    Application.Current.Dispatcher.Invoke(() => {
                        _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.AlertDialog($"{mediaTypeDisplay} مورد نظر پیدا نشد.\n\nنام جستجو شده: {representative.FormattedTitle}"), "RootDialog");
                    });
                    return;
                }
                
                using var db = new AppDbContext();
                
                // Merge with existing series/movie if same TmdbId is found in database
                if (identified.TmdbId.HasValue && identified.TmdbId > 0)
                {
                    var existing = db.VideoFiles.FirstOrDefault(v => v.TmdbId == identified.TmdbId && v.MediaType == identified.MediaType);
                    if (existing != null)
                    {
                        if (identified.MediaType == "Movie")
                        {
                            throw new InvalidOperationException("این فیلم قبلاً در دیتابیس ثبت شده است.");
                        }
                        else if (!string.IsNullOrWhiteSpace(existing.FormattedTitle))
                        {
                            foreach (var file in group.Files)
                            {
                                file.FormattedTitle = existing.FormattedTitle;
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                    {
                        foreach (var file in group.Files)
                        {
                            file.FormattedTitle = identified.FormattedTitle;
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                {
                    string lowerTitle = identified.FormattedTitle.ToLower();
                    var existing = db.VideoFiles.FirstOrDefault(v => v.FormattedTitle.ToLower() == lowerTitle && v.MediaType == identified.MediaType);
                    if (existing != null)
                    {
                        if (identified.MediaType == "Movie")
                        {
                            throw new InvalidOperationException("این فیلم قبلاً در دیتابیس ثبت شده است.");
                        }
                        else if (!string.IsNullOrWhiteSpace(existing.FormattedTitle))
                        {
                            foreach (var file in group.Files)
                            {
                                file.FormattedTitle = existing.FormattedTitle;
                            }
                        }
                    }
                    else
                    {
                        foreach (var file in group.Files)
                        {
                            file.FormattedTitle = identified.FormattedTitle;
                        }
                    }
                }

                foreach (var item in group.Files)
                {
                    item.TmdbId = identified.TmdbId;
                    item.PosterUrl = identified.PosterUrl;
                    item.Rating = identified.Rating;
                    item.Overview = identified.Overview;
                    item.BackdropUrl = identified.BackdropUrl;
                    item.Genres = identified.Genres;
                    item.Actors = identified.Actors;
                    item.Director = identified.Director;
                    if (!string.IsNullOrWhiteSpace(identified.Year)) item.Year = identified.Year;
                    item.IsIdentified = true;
                    db.VideoFiles.Add(item);
                }
                await db.SaveChangesAsync();
                
                group.TitleOverride = group.Files.First().FormattedTitle;
                group.Representative.FormattedTitle = group.TitleOverride;
                group.Status = "ثبت شد";
                group.IsRegistered = true;
                group.IsError = false;
                group.IsChecked = false;
                ApplyFilters();
                
                // Notify other ViewModels (Home, Movies, Series Tracker) to refresh
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                group.Status = "مسیر تکراری";
                group.IsError = true;
                ApplyFilters();
                string mediaTypeDisplay = group.Representative.MediaType == "Series" ? "سریال" : "فیلم";
                Application.Current.Dispatcher.Invoke(() => {
                    _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.ConfirmDialog($"{mediaTypeDisplay} مورد نظر در مسیر دیگری موجود است یا تکراری می‌باشد."), "RootDialog");
                });
            }
            catch (Exception ex)
            {
                group.Status = ex is InvalidOperationException ? ex.Message : $"خطا: {ex.Message}";
                group.IsError = true;
                ApplyFilters();
                
                string errMessage = ex is InvalidOperationException ? ex.Message :
                    (ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم ارتباط با سرور. لطفاً وضعیت اینترنت یا قندشکن خود را بررسی کنید." 
                    : $"خطای سیستمی:\n{ex.Message}");
                    
                Application.Current.Dispatcher.Invoke(() => {
                    _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.AlertDialog(errMessage), "RootDialog");
                });
            }
        }

        [RelayCommand]
        private async Task RegisterSelectedAsync()
        {
            if (IsRegistering || IsScanning) return;
            
            var selectedGroups = _allGroups.Where(x => x.IsChecked && !x.IsRegistered).ToList();
            if(!selectedGroups.Any()) return;
            
            IsRegistering = true;
            IsScanningIndeterminate = false;
            ScanProgressValue = 0;
            UpdateVisibility();
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();
                    int successCount = 0;
                    int processedGroups = 0;
                    int totalGroups = selectedGroups.Count;
                    
                    var fetchSemaphore = new SemaphoreSlim(5); // 5 concurrent fetches
                    var dbSemaphore = new SemaphoreSlim(1); // 1 concurrent db write for SQLite
                    
                    var tasks = selectedGroups.Select(async group =>
                    {
                        await fetchSemaphore.WaitAsync();
                        try
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested) return;
                            
                            var representative = group.Representative;
                            representative.FormattedTitle = string.IsNullOrWhiteSpace(group.TitleOverride) ? representative.FormattedTitle : group.TitleOverride;
                            representative.Year = string.IsNullOrWhiteSpace(group.YearOverride) ? null : group.YearOverride;
                            
                            Application.Current.Dispatcher.Invoke(() => group.Status = "در حال ارتباط با سرور...");
                            
                            var identified = await _identifyService.IdentifyMediaAsync(representative);
                            
                            bool hasData = (identified.TmdbId.HasValue && identified.TmdbId > 0) || 
                                           !string.IsNullOrWhiteSpace(identified.PosterUrl) || 
                                           !string.IsNullOrWhiteSpace(identified.Overview);
                            
                            if (!hasData)
                            {
                                Application.Current.Dispatcher.Invoke(() => {
                                    group.Status = "خطا در پیدا کردن";
                                    group.IsError = true;
                                    group.IsChecked = false;
                                    
                                    // Show dialog if it's the only item being processed or we want to notify
                                    if (totalGroups == 1)
                                    {
                                        string mediaTypeDisplay = representative.MediaType == "Series" ? "سریال" : "فیلم";
                                        _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.AlertDialog($"{mediaTypeDisplay} مورد نظر پیدا نشد.\n\nنام: {representative.FormattedTitle}"), "RootDialog");
                                    }
                                });
                                return;
                            }
                            
                            // Ensure safe sequential database access
                            await dbSemaphore.WaitAsync();
                            try
                            {
                                using var db = new AppDbContext();
                                
                                if (identified.TmdbId.HasValue && identified.TmdbId > 0)
                                {
                                    var existing = db.VideoFiles.FirstOrDefault(v => v.TmdbId == identified.TmdbId && v.MediaType == identified.MediaType);
                                    if (existing != null)
                                    {
                                        if (identified.MediaType == "Movie")
                                        {
                                            throw new InvalidOperationException("این فیلم قبلاً در دیتابیس ثبت شده است.");
                                        }
                                        else if (!string.IsNullOrWhiteSpace(existing.FormattedTitle))
                                        {
                                            foreach (var file in group.Files)
                                                file.FormattedTitle = existing.FormattedTitle;
                                        }
                                    }
                                    else if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                                    {
                                        foreach (var file in group.Files)
                                            file.FormattedTitle = identified.FormattedTitle;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                                {
                                    string lowerTitle = identified.FormattedTitle.ToLower();
                                    var existing = db.VideoFiles.FirstOrDefault(v => v.FormattedTitle.ToLower() == lowerTitle && v.MediaType == identified.MediaType);
                                    if (existing != null)
                                    {
                                        if (identified.MediaType == "Movie")
                                        {
                                            throw new InvalidOperationException("این فیلم قبلاً در دیتابیس ثبت شده است.");
                                        }
                                        else if (!string.IsNullOrWhiteSpace(existing.FormattedTitle))
                                        {
                                            foreach (var file in group.Files)
                                                file.FormattedTitle = existing.FormattedTitle;
                                        }
                                    }
                                    else
                                    {
                                        foreach (var file in group.Files)
                                            file.FormattedTitle = identified.FormattedTitle;
                                    }
                                }

                                foreach (var file in group.Files)
                                {
                                    file.TmdbId = identified.TmdbId;
                                    file.PosterUrl = identified.PosterUrl;
                                    file.Rating = identified.Rating;
                                    file.Overview = identified.Overview;
                                    file.BackdropUrl = identified.BackdropUrl;
                                    file.Genres = identified.Genres;
                                    file.Actors = identified.Actors;
                                    file.Director = identified.Director;
                                    if (!string.IsNullOrWhiteSpace(identified.Year)) file.Year = identified.Year;
                                    file.IsIdentified = true;
                                    db.VideoFiles.Add(file);
                                }
                                await db.SaveChangesAsync();
                            }
                            finally
                            {
                                dbSemaphore.Release();
                            }
                            
                            successCount += group.Files.Count;
                            
                            Application.Current.Dispatcher.Invoke(() => {
                                group.TitleOverride = group.Files.First().FormattedTitle;
                                group.Representative.FormattedTitle = group.TitleOverride;
                                group.Status = "ثبت شد";
                                group.IsRegistered = true;
                                group.IsError = false;
                                group.IsChecked = false;
                                
                                processedGroups++;
                                ScanProgressValue = ((double)processedGroups / totalGroups) * 100;
                                ScanProgressText = $"در حال ثبت... {((double)processedGroups / totalGroups) * 100:0}% ({processedGroups} از {totalGroups})";
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => {
                                group.Status = ex is InvalidOperationException ? ex.Message : "خطای سیستمی";
                                group.IsError = true;
                                if (ex is InvalidOperationException && totalGroups == 1)
                                {
                                    _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.AlertDialog(ex.Message), "RootDialog");
                                }
                            });
                        }
                        finally
                        {
                            fetchSemaphore.Release();
                        }
                    });
                    
                    await Task.WhenAll(tasks);
                    
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        StatusMessage = $"ثبت با موفقیت تمام شد. {successCount} فایل ثبت شد.";
                    }
                    
                    
                    Application.Current.Dispatcher.Invoke(() => ApplyFilters());
                    WeakReferenceMessenger.Default.Send(new MovieManagerDesktop.Messages.MediaUpdatedMessage());
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در ثبت: {ex.Message}";
                Application.Current.Dispatcher.Invoke(() => {
                    string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                        ? "عدم ارتباط با سرور در حین ثبت گروهی. لطفاً اینترنت خود را بررسی کنید." 
                        : $"خطا در ثبت گروهی:\n{ex.Message}";
                    _ = MaterialDesignThemes.Wpf.DialogHost.Show(new MovieManagerDesktop.Controls.ConfirmDialog(errMessage), "RootDialog");
                });
            }
            finally
            {
                IsRegistering = false;
                ScanProgressValue = 100;
                UpdateVisibility();
            }
        }

        [RelayCommand]
        private void StopScan()
        {
            _cancellationTokenSource?.Cancel();
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
