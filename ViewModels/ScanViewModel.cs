using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
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

        public int GetRemainingFreeTierQuota()
        {
            if (LicenseManagerService.IsLicenseValid())
            {
                return int.MaxValue;
            }

            try
            {
                using var db = new AppDbContext();
                int currentTitleCount = db.VideoFiles.Select(v => v.FormattedTitle).Distinct().Count();
                return Math.Max(0, LicenseManagerService.FreeTierMediaLimit - currentTitleCount);
            }
            catch (Exception ex)
            {
                LoggerService.Warning($"[اسکنر] خطا در محاسبه سقف نسخه آزمایشی: {ex.Message}");
                return LicenseManagerService.FreeTierMediaLimit;
            }
        }

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (value && !LicenseManagerService.IsLicenseValid())
                {
                    int quota = GetRemainingFreeTierQuota();
                    int checkedCount = 0;
                    foreach (var group in _allGroups)
                    {
                        if (checkedCount < quota)
                        {
                            group.IsChecked = true;
                            checkedCount++;
                        }
                        else
                        {
                            group.IsChecked = false;
                        }
                    }
                    _isAllSelected = (_allGroups.Count <= quota && quota > 0);
                    OnPropertyChanged(nameof(IsAllSelected));
                    UpdateBulkToolbar();
                    if (_allGroups.Count > quota)
                    {
                        ToastService.Instance.ShowWarning($"در نسخه آزمایشی حداکثر {quota} عنوان مجاز به انتخاب هستند (سقف کل: {LicenseManagerService.FreeTierMediaLimit} عنوان). برای ثبت نامحدود لطفاً لایسنس را فعال کنید.");
                    }
                    return;
                }

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

        [RelayCommand]
        private void CheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var group in selectedItems.Cast<ScannedGroupViewModel>())
            {
                group.IsChecked = true;
            }
            UpdateBulkToolbar();
        }

        [RelayCommand]
        private void UncheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var group in selectedItems.Cast<ScannedGroupViewModel>())
            {
                group.IsChecked = false;
            }
            UpdateBulkToolbar();
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
            if (!LicenseManagerService.IsLicenseValid())
            {
                int quota = GetRemainingFreeTierQuota();
                int currentChecked = _allGroups.Count(g => g.IsChecked);
                if (currentChecked > quota)
                {
                    int allowed = quota;
                    foreach (var g in _allGroups)
                    {
                        if (g.IsChecked)
                        {
                            if (allowed > 0)
                            {
                                allowed--;
                            }
                            else
                            {
                                g.IsChecked = false;
                            }
                        }
                    }
                    ToastService.Instance.ShowWarning($"در نسخه آزمایشی حداکثر مجاز به انتخاب {quota} عنوان هستید (سقف کل: {LicenseManagerService.FreeTierMediaLimit} عنوان).");
                }
            }

            int count = _allGroups.Count(g => g.IsChecked);
            SelectedCountText = $"{count} مورد انتخاب شد";
            HasSelection = count > 0 && !IsScanning && !IsRegistering;
            BulkToolbarVisibility = HasSelection ? Visibility.Visible : Visibility.Collapsed;
            RegisterButtonVisibility = BulkToolbarVisibility; // Link to the new button they added

            _isAllSelected = _allGroups.Count > 0 && _allGroups.All(g => g.IsChecked);
            OnPropertyChanged(nameof(IsAllSelected));
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
            
            if (string.IsNullOrWhiteSpace(SelectedDirectory) || !Directory.Exists(SelectedDirectory))
            {
                StatusMessage = "مسیر نامعتبر است!";
                ToastService.Instance.ShowError("مسیر انتخاب شده برای اسکن نامعتبر است یا در دسترس نیست.");
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
                
                if (groupedFileLists.Count == 0)
                {
                    StatusMessage = "هیچ فایل ویدیویی یافت نشد.";
                    ToastService.Instance.ShowWarning("هیچ فایل ویدیویی پشتیبانی‌شده‌ای در این پوشه یافت نشد.");
                    return;
                }

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
                
                int remainingQuota = GetRemainingFreeTierQuota();
                int checkedCount = 0;

                foreach (var fileList in groupedFileLists)
                {
                    var vm = new ScannedGroupViewModel(fileList, existingSeriesCache);
                    if (checkedCount < remainingQuota)
                    {
                        vm.IsChecked = true;
                        checkedCount++;
                    }
                    else
                    {
                        vm.IsChecked = false;
                    }
                    _allGroups.Add(vm);
                }
                
                SearchQuery = string.Empty;
                IsFilterAll = true; // This will call ApplyFilters
                
                _isAllSelected = _allGroups.Count > 0 && _allGroups.All(g => g.IsChecked);
                OnPropertyChanged(nameof(IsAllSelected));
                UpdateBulkToolbar();
                
                ScanProgressText = $"{_allGroups.Count} گروه یافت شد";
                IsScanningIndeterminate = false;
                ScanProgressValue = 100;
                StatusMessage = $"اسکن با موفقیت انجام شد ({_allGroups.Count} عنوان یافت شد).";

                if (!LicenseManagerService.IsLicenseValid() && _allGroups.Count > remainingQuota)
                {
                    if (remainingQuota > 0)
                    {
                        ToastService.Instance.ShowInfo($"اسکن انجام شد. در نسخه آزمایشی {remainingQuota} عنوان اول علامت‌گذاری شدند (سقف کل: {LicenseManagerService.FreeTierMediaLimit} عنوان).");
                    }
                    else
                    {
                        ToastService.Instance.ShowWarning($"سقف نسخه آزمایشی ({LicenseManagerService.FreeTierMediaLimit} عنوان) پر شده است. برای ثبت عناوین جدید لطفاً لایسنس برنامه را فعال نمایید.");
                    }
                }
                else
                {
                    ToastService.Instance.ShowSuccess($"{_allGroups.Count} عنوان ویدیویی جهت بررسی و ثبت آماده شد.");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "اسکن توسط کاربر متوقف شد.";
                ToastService.Instance.ShowInfo("اسکن متوقف شد.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطای سیستمی: {ex.Message}";
                ToastService.Instance.ShowError($"خطا در اسکن پوشه: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                UpdateVisibility();
            }
        }

        [RelayCommand]
        private void OpenEditDialog(ScannedGroupViewModel group)
        {
            if (group == null) return;
            
            var vm = new EditScannedGroupViewModel(group, this);
            var view = new EditScannedGroupDialog
            {
                DataContext = vm
            };
            vm.CloseAction = () => view.Close();
            WindowHelper.SafeShowDialog(view);
        }

        [RelayCommand]
        private void OpenSelectExistingDialog(ScannedGroupViewModel group)
        {
            if (group == null) return;
            
            var targetGroups = new List<ScannedGroupViewModel>();
            if (group.IsChecked)
            {
                targetGroups = _allGroups.Where(g => g.IsChecked).ToList();
            }
            else
            {
                targetGroups.Add(group);
            }

            if (targetGroups.Count == 0) return;

            var vm = new SelectExistingMediaViewModel(targetGroups, this);
            var view = new SelectExistingMediaDialog
            {
                DataContext = vm
            };
            vm.CloseAction = () => view.Close();
            WindowHelper.SafeShowDialog(view);
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
                    ToastService.Instance.ShowWarning($"{mediaTypeDisplay} «{representative.FormattedTitle}» در سرور یافت نشد. می‌توانید با «جستجوی دستی» آن را انتخاب کنید.");
                    return;
                }
                
                using var db = new AppDbContext();
                
                // Merge with existing series/movie if same TmdbId or FormattedTitle is found in database
                VideoFile? existing = null;
                if (identified.TmdbId.HasValue && identified.TmdbId > 0)
                {
                    existing = db.VideoFiles.FirstOrDefault(v => v.TmdbId == identified.TmdbId && (v.MediaType == identified.MediaType || v.MediaType == representative.MediaType));
                }
                if (existing == null && !string.IsNullOrWhiteSpace(identified.FormattedTitle))
                {
                    string lowerTitle = identified.FormattedTitle.ToLower();
                    existing = db.VideoFiles.FirstOrDefault(v => v.FormattedTitle.ToLower() == lowerTitle);
                }

                if (existing != null && !string.IsNullOrWhiteSpace(existing.FormattedTitle))
                {
                    foreach (var file in group.Files)
                    {
                        file.FormattedTitle = existing.FormattedTitle;
                        if (!string.IsNullOrWhiteSpace(existing.MediaType))
                        {
                            file.MediaType = existing.MediaType;
                        }
                    }
                }
                else
                {
                    // Check free tier limit if adding a brand new title
                    if (!LicenseManagerService.IsLicenseValid())
                    {
                        int currentTitleCount = db.VideoFiles.Select(v => v.FormattedTitle).Distinct().Count();
                        if (currentTitleCount >= LicenseManagerService.FreeTierMediaLimit)
                        {
                            group.Status = "نیاز به لایسنس";
                            group.IsError = true;
                            group.IsChecked = false;
                            ApplyFilters();
                            ToastService.Instance.ShowWarning($"سقف نسخه آزمایشی ({LicenseManagerService.FreeTierMediaLimit} عنوان) تکمیل شده است. برای اسکن نامحدود و ثبت آرشیو کامل، لطفاً لایسنس برنامه را فعال نمایید.");
                            var win = new LicenseActivationWindow();
                            WindowHelper.SafeShowDialog(win);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                    {
                        foreach (var file in group.Files)
                        {
                            file.FormattedTitle = identified.FormattedTitle;
                        }
                    }
                }

                var groupPaths = group.Files.Select(v => v.FilePath).ToList();
                var alreadyExistingPaths = db.VideoFiles.Where(v => groupPaths.Contains(v.FilePath)).Select(v => v.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int newlyAddedCount = 0;

                foreach (var item in group.Files)
                {
                    if (alreadyExistingPaths.Contains(item.FilePath))
                    {
                        continue;
                    }

                    item.TmdbId = identified.TmdbId ?? existing?.TmdbId;
                    item.PosterUrl = !string.IsNullOrWhiteSpace(identified.PosterUrl) ? identified.PosterUrl : existing?.PosterUrl;
                    item.Rating = identified.Rating ?? existing?.Rating;
                    item.Overview = !string.IsNullOrWhiteSpace(identified.Overview) ? identified.Overview : existing?.Overview;
                    item.BackdropUrl = !string.IsNullOrWhiteSpace(identified.BackdropUrl) ? identified.BackdropUrl : existing?.BackdropUrl;
                    item.Genres = !string.IsNullOrWhiteSpace(identified.Genres) ? identified.Genres : existing?.Genres;
                    item.Actors = !string.IsNullOrWhiteSpace(identified.Actors) ? identified.Actors : existing?.Actors;
                    item.Director = !string.IsNullOrWhiteSpace(identified.Director) ? identified.Director : existing?.Director;
                    
                    if (!string.IsNullOrWhiteSpace(identified.Year)) item.Year = identified.Year;
                    else if (identified.FirstAirDate.HasValue) item.Year = identified.FirstAirDate.Value.Year.ToString();
                    else if (!string.IsNullOrWhiteSpace(existing?.Year)) item.Year = existing.Year;

                    item.FirstAirDate = identified.FirstAirDate ?? existing?.FirstAirDate;
                    item.LastAirDate = identified.LastAirDate ?? existing?.LastAirDate;
                    item.NetworkName = identified.NetworkName ?? existing?.NetworkName;
                    item.AirDay = identified.AirDay ?? existing?.AirDay;
                    item.AirTime = identified.AirTime ?? existing?.AirTime;
                    item.TotalSeasonsCount = identified.TotalSeasonsCount ?? identified.NumberOfSeasons ?? existing?.TotalSeasonsCount;
                    item.TotalEpisodesCount = identified.TotalEpisodesCount ?? identified.NumberOfEpisodes ?? existing?.TotalEpisodesCount;
                    item.NumberOfSeasons = identified.NumberOfSeasons ?? identified.TotalSeasonsCount ?? existing?.NumberOfSeasons;
                    item.NumberOfEpisodes = identified.NumberOfEpisodes ?? identified.TotalEpisodesCount ?? existing?.NumberOfEpisodes;
                    item.SeriesStatus = identified.SeriesStatus ?? existing?.SeriesStatus;
                    item.CollectionName = identified.CollectionName ?? existing?.CollectionName;
                    item.MediaType = existing?.MediaType ?? identified.MediaType ?? item.MediaType ?? "Series";
                    item.IsIdentified = true;
                    
                    db.VideoFiles.Add(item);
                    newlyAddedCount++;
                }

                if (newlyAddedCount > 0)
                {
                    await db.SaveChangesAsync();
                }

                group.TitleOverride = group.Files.First().FormattedTitle;
                group.Representative.FormattedTitle = group.TitleOverride;
                group.Status = newlyAddedCount > 0 ? "ثبت شد" : "قبلاً ثبت شده";
                group.IsRegistered = true;
                group.IsError = false;
                group.IsChecked = false;
                ApplyFilters();
                
                if (newlyAddedCount > 0)
                {
                    ToastService.Instance.ShowSuccess($"«{group.TitleOverride}» ({newlyAddedCount} قسمت/فایل) با موفقیت در دیتابیس ثبت شد.");
                }
                else
                {
                    ToastService.Instance.ShowInfo($"فایل‌های «{group.TitleOverride}» قبلاً در دیتابیس ثبت شده بودند.");
                }

                // Notify other ViewModels (Home, Movies, Series Tracker) to refresh
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                group.Status = "مسیر تکراری";
                group.IsError = true;
                ApplyFilters();
                string mediaTypeDisplay = group.Representative.MediaType == "Series" ? "سریال" : "فیلم";
                ToastService.Instance.ShowWarning($"{mediaTypeDisplay} مورد نظر در مسیر دیگری موجود است یا تکراری می‌باشد.");
            }
            catch (Exception ex)
            {
                group.Status = ex is InvalidOperationException ? ex.Message : $"خطا: {ex.Message}";
                group.IsError = true;
                ApplyFilters();
                
                string errMessage = ex is InvalidOperationException ? ex.Message :
                    (ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم ارتباط با سرور. لطفاً وضعیت اینترنت یا قندشکن خود را بررسی کنید." 
                    : $"خطای سیستمی: {ex.Message}");
                    
                ToastService.Instance.ShowError(errMessage);
            }
        }

        [RelayCommand]
        private async Task RegisterSelectedAsync()
        {
            if (IsRegistering || IsScanning) return;
            
            var selectedGroups = _allGroups.Where(x => x.IsChecked && !x.IsRegistered).ToList();
            if (!selectedGroups.Any())
            {
                ToastService.Instance.ShowWarning("لطفاً حداقل یک عنوان را جهت ثبت انتخاب کنید.");
                return;
            }

            // Check free tier limit before starting bulk registration
            if (!LicenseManagerService.IsLicenseValid())
            {
                int quota = GetRemainingFreeTierQuota();
                if (quota <= 0)
                {
                    ToastService.Instance.ShowWarning($"سقف ثبت در نسخه آزمایشی ({LicenseManagerService.FreeTierMediaLimit} عنوان) تکمیل شده است. برای اسکن نامحدود و ثبت آرشیو کامل، لطفاً لایسنس برنامه را فعال نمایید.");
                    var win = new LicenseActivationWindow();
                    WindowHelper.SafeShowDialog(win);
                    return;
                }

                if (selectedGroups.Count > quota)
                {
                    selectedGroups = selectedGroups.Take(quota).ToList();
                }
            }
            
            LoggerService.Info($"[اسکنر] شروع ثبت گروهی برای {selectedGroups.Count} گروه...");

            IsRegistering = true;
            IsScanningIndeterminate = false;
            ScanProgressValue = 0;
            UpdateVisibility();
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                await Task.Run(async () =>
                {
                    int successCount = 0;
                    int failedCount = 0;
                    int processedGroups = 0;
                    int totalGroups = selectedGroups.Count;
                    
                    var fetchSemaphore = new SemaphoreSlim(2); // Halved from 5 to 2 concurrent fetches for network & proxy stability
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
                            
                            _ = Application.Current.Dispatcher.InvokeAsync(() => group.Status = "در حال ارتباط با سرور...");
                            
                            var identified = await _identifyService.IdentifyMediaAsync(representative);
                            
                            bool hasData = (identified.TmdbId.HasValue && identified.TmdbId > 0) || 
                                           !string.IsNullOrWhiteSpace(identified.PosterUrl) || 
                                           !string.IsNullOrWhiteSpace(identified.Overview);
                            
                            if (!hasData)
                            {
                                Interlocked.Increment(ref failedCount);
                                int doneCount = Interlocked.Increment(ref processedGroups);
                                LoggerService.Warning($"[اسکنر] دیتایی برای '{representative.FormattedTitle}' یافت نشد.");
                                _ = Application.Current.Dispatcher.InvokeAsync(() => {
                                    group.Status = "خطا در پیدا کردن";
                                    group.IsError = true;
                                    group.IsChecked = false;
                                    ScanProgressValue = ((double)doneCount / totalGroups) * 100;
                                    ScanProgressText = $"در حال ثبت... {((double)doneCount / totalGroups) * 100:0}% ({doneCount} از {totalGroups})";
                                });
                                return;
                            }
                            
                            int newlyAddedCount = 0;
                            // Ensure safe sequential database access
                            await dbSemaphore.WaitAsync();
                            try
                            {
                                using var db = new AppDbContext();
                                
                                VideoFile? existing = null;
                                if (identified.TmdbId.HasValue && identified.TmdbId > 0)
                                {
                                    existing = db.VideoFiles.FirstOrDefault(v => v.TmdbId == identified.TmdbId && (v.MediaType == identified.MediaType || v.MediaType == representative.MediaType));
                                }
                                if (existing == null && !string.IsNullOrWhiteSpace(identified.FormattedTitle))
                                {
                                    string lowerTitle = identified.FormattedTitle.ToLower();
                                    existing = db.VideoFiles.FirstOrDefault(v => v.FormattedTitle.ToLower() == lowerTitle);
                                }

                                if (existing != null && !string.IsNullOrWhiteSpace(existing.FormattedTitle))
                                {
                                    foreach (var file in group.Files)
                                    {
                                        file.FormattedTitle = existing.FormattedTitle;
                                        if (!string.IsNullOrWhiteSpace(existing.MediaType))
                                        {
                                            file.MediaType = existing.MediaType;
                                        }
                                    }
                                }
                                else
                                {
                                    // In-flight limit check during bulk registration
                                    if (!LicenseManagerService.IsLicenseValid())
                                    {
                                        int currentTitleCount = db.VideoFiles.Select(v => v.FormattedTitle).Distinct().Count();
                                        if (currentTitleCount >= LicenseManagerService.FreeTierMediaLimit)
                                        {
                                            _cancellationTokenSource?.Cancel();
                                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                            {
                                                group.Status = "نیاز به لایسنس";
                                                group.IsError = true;
                                                group.IsChecked = false;
                                                ToastService.Instance.ShowWarning($"سقف نسخه آزمایشی ({LicenseManagerService.FreeTierMediaLimit} عنوان) تکمیل شد. برای ادامه اسکن و ثبت نامحدود، لطفاً لایسنس برنامه را فعال نمایید.");
                                                var win = new LicenseActivationWindow();
                                                WindowHelper.SafeShowDialog(win);
                                            });
                                            return;
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(identified.FormattedTitle))
                                    {
                                        foreach (var file in group.Files)
                                        {
                                            file.FormattedTitle = identified.FormattedTitle;
                                        }
                                    }
                                }

                                var groupPaths = group.Files.Select(v => v.FilePath).ToList();
                                var alreadyExistingPaths = db.VideoFiles.Where(v => groupPaths.Contains(v.FilePath)).Select(v => v.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

                                foreach (var file in group.Files)
                                {
                                    if (alreadyExistingPaths.Contains(file.FilePath))
                                    {
                                        continue;
                                    }

                                    file.TmdbId = identified.TmdbId ?? existing?.TmdbId;
                                    file.PosterUrl = !string.IsNullOrWhiteSpace(identified.PosterUrl) ? identified.PosterUrl : existing?.PosterUrl;
                                    file.Rating = identified.Rating ?? existing?.Rating;
                                    file.Overview = !string.IsNullOrWhiteSpace(identified.Overview) ? identified.Overview : existing?.Overview;
                                    file.BackdropUrl = !string.IsNullOrWhiteSpace(identified.BackdropUrl) ? identified.BackdropUrl : existing?.BackdropUrl;
                                    file.Genres = !string.IsNullOrWhiteSpace(identified.Genres) ? identified.Genres : existing?.Genres;
                                    file.Actors = !string.IsNullOrWhiteSpace(identified.Actors) ? identified.Actors : existing?.Actors;
                                    file.Director = !string.IsNullOrWhiteSpace(identified.Director) ? identified.Director : existing?.Director;
                                    
                                    if (!string.IsNullOrWhiteSpace(identified.Year)) file.Year = identified.Year;
                                    else if (identified.FirstAirDate.HasValue) file.Year = identified.FirstAirDate.Value.Year.ToString();
                                    else if (!string.IsNullOrWhiteSpace(existing?.Year)) file.Year = existing.Year;

                                    file.FirstAirDate = identified.FirstAirDate ?? existing?.FirstAirDate;
                                    file.LastAirDate = identified.LastAirDate ?? existing?.LastAirDate;
                                    file.NetworkName = identified.NetworkName ?? existing?.NetworkName;
                                    file.AirDay = identified.AirDay ?? existing?.AirDay;
                                    file.AirTime = identified.AirTime ?? existing?.AirTime;
                                    file.TotalSeasonsCount = identified.TotalSeasonsCount ?? identified.NumberOfSeasons ?? existing?.TotalSeasonsCount;
                                    file.TotalEpisodesCount = identified.TotalEpisodesCount ?? identified.NumberOfEpisodes ?? existing?.TotalEpisodesCount;
                                    file.NumberOfSeasons = identified.NumberOfSeasons ?? identified.TotalSeasonsCount ?? existing?.NumberOfSeasons;
                                    file.NumberOfEpisodes = identified.NumberOfEpisodes ?? identified.TotalEpisodesCount ?? existing?.NumberOfEpisodes;
                                    file.SeriesStatus = identified.SeriesStatus ?? existing?.SeriesStatus;
                                    file.CollectionName = identified.CollectionName ?? existing?.CollectionName;
                                    file.MediaType = existing?.MediaType ?? identified.MediaType ?? file.MediaType ?? "Series";
                                    file.IsIdentified = true;
                                    
                                    db.VideoFiles.Add(file);
                                    newlyAddedCount++;
                                }

                                if (newlyAddedCount > 0)
                                {
                                    await db.SaveChangesAsync();
                                }
                            }
                            finally
                            {
                                dbSemaphore.Release();
                            }

                            Interlocked.Add(ref successCount, newlyAddedCount > 0 ? newlyAddedCount : group.Files.Count);
                            int done = Interlocked.Increment(ref processedGroups);

                            _ = Application.Current.Dispatcher.InvokeAsync(() => {
                                group.TitleOverride = group.Files.First().FormattedTitle;
                                group.Representative.FormattedTitle = group.TitleOverride;
                                group.Status = newlyAddedCount > 0 ? "ثبت شد" : "قبلاً ثبت شده";
                                group.IsRegistered = true;
                                group.IsError = false;
                                group.IsChecked = false;
                                
                                LoggerService.Info($"[اسکنر] '{group.TitleOverride}' با موفقیت در دیتابیس ثبت شد.");

                                ScanProgressValue = ((double)done / totalGroups) * 100;
                                ScanProgressText = $"در حال ثبت... {((double)done / totalGroups) * 100:0}% ({done} از {totalGroups})";
                            });
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failedCount);
                            int done = Interlocked.Increment(ref processedGroups);
                            LoggerService.Error($"[اسکنر] خطا در پردازش '{group.Representative.FormattedTitle}': {ex.Message}", ex);
                            _ = Application.Current.Dispatcher.InvokeAsync(() => {
                                group.Status = ex is InvalidOperationException ? ex.Message : "خطای سیستمی";
                                group.IsError = true;
                                ScanProgressValue = ((double)done / totalGroups) * 100;
                                ScanProgressText = $"در حال ثبت... {((double)done / totalGroups) * 100:0}% ({done} از {totalGroups})";
                            });
                        }
                        finally
                        {
                            fetchSemaphore.Release();
                        }
                    }).ToList();
                    
                    await Task.WhenAll(tasks);
                    
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        LoggerService.Info($"[اسکنر] عملیات ثبت گروهی پایان یافت. کل فایل‌های اضافه شده: {successCount}, ناموفق: {failedCount}");
                        StatusMessage = $"ثبت پایان یافت ({successCount} فایل با موفقیت ثبت شد).";

                        if (successCount > 0)
                        {
                            ToastService.Instance.ShowSuccess($"عملیات ثبت پایان یافت: {successCount} فایل با موفقیت در برنامه ثبت شد.");
                        }

                        if (failedCount > 0)
                        {
                            ToastService.Instance.ShowWarning($"{failedCount} عنوان ثبت نشد. می‌توانید با فیلتر «خطا در ثبت» آن‌ها را به صورت دستی جستجو و ثبت کنید.");
                        }
                    }
                    else
                    {
                        StatusMessage = "ثبت توسط کاربر لغو شد.";
                        ToastService.Instance.ShowInfo("عملیات ثبت لغو شد.");
                    }
                    
                    _ = Application.Current.Dispatcher.InvokeAsync(() => ApplyFilters());
                    WeakReferenceMessenger.Default.Send(new MovieManagerDesktop.Messages.MediaUpdatedMessage());
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در ثبت: {ex.Message}";
                string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم ارتباط با سرور در حین ثبت گروهی. لطفاً اینترنت یا قندشکن خود را بررسی کنید." 
                    : $"خطا در ثبت گروهی: {ex.Message}";
                ToastService.Instance.ShowError(errMessage);
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
