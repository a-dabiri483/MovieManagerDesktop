using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.Controls;
using Microsoft.EntityFrameworkCore;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows; // For Clipboard

namespace MovieManagerDesktop.ViewModels
{
    public enum SettingsSubView
    {
        Main,
        DataSources,
        Backup,
        Proxy,
        Personalization,
        Player,
        Education,
        About
    }

    public class EducationTopicItem : ObservableObject
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string IconKind { get; set; } = "School";
        public string IconColor { get; set; } = "#EB3B5A";

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
    }

    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private SettingsSubView _currentSubView = SettingsSubView.Main;

        public bool IsMainView => CurrentSubView == SettingsSubView.Main;
        public bool IsDataSourcesView => CurrentSubView == SettingsSubView.DataSources;
        public bool IsBackupView => CurrentSubView == SettingsSubView.Backup;
        public bool IsProxyView => CurrentSubView == SettingsSubView.Proxy;
        public bool IsPersonalizationView => CurrentSubView == SettingsSubView.Personalization;
        public bool IsPlayerView => CurrentSubView == SettingsSubView.Player;
        public bool IsEducationView => CurrentSubView == SettingsSubView.Education;
        public bool IsAboutView => CurrentSubView == SettingsSubView.About;

        partial void OnCurrentSubViewChanged(SettingsSubView value)
        {
            OnPropertyChanged(nameof(IsMainView));
            OnPropertyChanged(nameof(IsDataSourcesView));
            OnPropertyChanged(nameof(IsBackupView));
            OnPropertyChanged(nameof(IsProxyView));
            OnPropertyChanged(nameof(IsPersonalizationView));
            OnPropertyChanged(nameof(IsPlayerView));
            OnPropertyChanged(nameof(IsEducationView));
            OnPropertyChanged(nameof(IsAboutView));
        }

        [ObservableProperty]
        private string _educationSearchQuery = string.Empty;

        partial void OnEducationSearchQueryChanged(string value)
        {
            string q = (value ?? string.Empty).Trim().ToLower();
            foreach (var topic in EducationTopics)
            {
                if (string.IsNullOrEmpty(q))
                {
                    topic.IsVisible = true;
                }
                else
                {
                    topic.IsVisible = topic.Title.ToLower().Contains(q) ||
                                      topic.Description.ToLower().Contains(q) ||
                                      topic.Content.ToLower().Contains(q);
                }
            }
        }

        public ObservableCollection<EducationTopicItem> EducationTopics { get; } = new();

        // Personalization properties matching Android Photo 4 & 5
        [ObservableProperty]
        private bool _showActorImages = true;

        [ObservableProperty]
        private bool _hideAdultContent = false;

        [ObservableProperty]
        private bool _showBoxOfficeOnHome = true;

        [ObservableProperty]
        private string _boxOfficeCurrency = "usd";

        [ObservableProperty]
        private string _defaultBoxOfficeFilter = "all";

        [ObservableProperty]
        private bool _autoSyncUpcomingToCalendar = true;

        [ObservableProperty]
        private string _genreLanguageOverride = "fa";

        [ObservableProperty]
        private string _translateToLanguage = "fa";

        [ObservableProperty]
        private string _fetchInfoLanguage = "fa-IR";

        [ObservableProperty]
        private string _dateFormatOverride = "jalali";

        public bool IsJalaliCalendar
        {
            get => string.Equals(DateFormatOverride, "jalali", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(DateFormatOverride) || DateFormatOverride == "auto";
            set
            {
                if (value) DateFormatOverride = "jalali";
            }
        }

        public bool IsGregorianCalendar
        {
            get => string.Equals(DateFormatOverride, "gregorian", StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value) DateFormatOverride = "gregorian";
            }
        }

        private void SavePersonalizationSettings()
        {
            var settings = SettingsManager.LoadSettings();
            settings.DateFormatOverride = DateFormatOverride;
            settings.GenreLanguageOverride = GenreLanguageOverride;
            settings.TranslateToLanguage = TranslateToLanguage;
            settings.FetchInfoLanguage = FetchInfoLanguage;
            settings.ShowActorImages = ShowActorImages;
            settings.HideAdultContent = HideAdultContent;
            SettingsManager.SaveSettings(settings);

            WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
        }

        partial void OnDateFormatOverrideChanged(string value)
        {
            SavePersonalizationSettings();
            OnPropertyChanged(nameof(IsJalaliCalendar));
            OnPropertyChanged(nameof(IsGregorianCalendar));
        }

        partial void OnGenreLanguageOverrideChanged(string value)
        {
            SavePersonalizationSettings();
        }

        partial void OnTranslateToLanguageChanged(string value)
        {
            SavePersonalizationSettings();
        }

        partial void OnFetchInfoLanguageChanged(string value)
        {
            SavePersonalizationSettings();
        }

        partial void OnShowActorImagesChanged(bool value)
        {
            SavePersonalizationSettings();
        }

        // Video Player Settings Properties
        [ObservableProperty]
        private bool _useInternalPlayer = true;

        [ObservableProperty]
        private string _externalPlayerType = "SystemDefault"; // SystemDefault, PotPlayer, VLC, Custom

        [ObservableProperty]
        private string _customExternalPlayerPath = string.Empty;

        public bool IsInternalPlayerSelected
        {
            get => UseInternalPlayer;
            set
            {
                if (value)
                {
                    UseInternalPlayer = true;
                    SavePlayerSettings();
                }
            }
        }

        public bool IsExternalPlayerSelected
        {
            get => !UseInternalPlayer;
            set
            {
                if (value)
                {
                    UseInternalPlayer = false;
                    SavePlayerSettings();
                }
            }
        }

        public bool IsSystemDefaultPlayerSelected
        {
            get => ExternalPlayerType == "SystemDefault";
            set { if (value) { ExternalPlayerType = "SystemDefault"; SavePlayerSettings(); } }
        }

        public bool IsPotPlayerSelected
        {
            get => ExternalPlayerType == "PotPlayer";
            set { if (value) { ExternalPlayerType = "PotPlayer"; SavePlayerSettings(); } }
        }

        public bool IsVlcSelected
        {
            get => ExternalPlayerType == "VLC";
            set { if (value) { ExternalPlayerType = "VLC"; SavePlayerSettings(); } }
        }

        public bool IsCustomPlayerSelected
        {
            get => ExternalPlayerType == "Custom";
            set { if (value) { ExternalPlayerType = "Custom"; SavePlayerSettings(); } }
        }

        [RelayCommand]
        private void OpenDataSources() => CurrentSubView = SettingsSubView.DataSources;

        [RelayCommand]
        private void OpenProxy() => CurrentSubView = SettingsSubView.Proxy;

        [RelayCommand]
        private void OpenBackup()
        {
            CurrentSubView = SettingsSubView.Backup;
            CheckGoogleDriveConnection();
        }

        [RelayCommand]
        private void OpenPersonalization() => CurrentSubView = SettingsSubView.Personalization;

        [RelayCommand]
        private void OpenPlayer()
        {
            LoadPlayerSettings();
            CurrentSubView = SettingsSubView.Player;
        }

        [RelayCommand]
        private void OpenEducation() => CurrentSubView = SettingsSubView.Education;

        [RelayCommand]
        private void OpenAbout() => CurrentSubView = SettingsSubView.About;

        [RelayCommand]
        private void BrowseCustomExternalPlayer()
        {
            var dialog = new OpenFileDialog
            {
                Title = "انتخاب فایل اجرایی پلیر ویدیویی",
                Filter = "فایل‌های اجرایی (*.exe)|*.exe|همه فایل‌ها (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                CustomExternalPlayerPath = dialog.FileName;
                ExternalPlayerType = "Custom";
                SavePlayerSettings();
                ToastService.Instance.ShowSuccess("پلیر سفارشی با موفقیت انتخاب شد.");
            }
        }

        private void SavePlayerSettings()
        {
            var settings = SettingsManager.LoadSettings();
            settings.UseInternalPlayer = UseInternalPlayer;
            settings.ExternalPlayerType = ExternalPlayerType;
            settings.CustomExternalPlayerPath = CustomExternalPlayerPath;
            SettingsManager.SaveSettings(settings);

            OnPropertyChanged(nameof(IsInternalPlayerSelected));
            OnPropertyChanged(nameof(IsExternalPlayerSelected));
            OnPropertyChanged(nameof(IsSystemDefaultPlayerSelected));
            OnPropertyChanged(nameof(IsPotPlayerSelected));
            OnPropertyChanged(nameof(IsVlcSelected));
            OnPropertyChanged(nameof(IsCustomPlayerSelected));
        }

        private void LoadPlayerSettings()
        {
            var settings = SettingsManager.LoadSettings();
            UseInternalPlayer = settings.UseInternalPlayer;
            ExternalPlayerType = settings.ExternalPlayerType ?? "SystemDefault";
            CustomExternalPlayerPath = settings.CustomExternalPlayerPath ?? string.Empty;

            OnPropertyChanged(nameof(IsInternalPlayerSelected));
            OnPropertyChanged(nameof(IsExternalPlayerSelected));
            OnPropertyChanged(nameof(IsSystemDefaultPlayerSelected));
            OnPropertyChanged(nameof(IsPotPlayerSelected));
            OnPropertyChanged(nameof(IsVlcSelected));
            OnPropertyChanged(nameof(IsCustomPlayerSelected));
        }

        public string SelectedDataSource
        {
            get => _selectedDataSource;
            set
            {
                SetProperty(ref _selectedDataSource, value);
                OnPropertyChanged(nameof(IsTmdbSelected));
                OnPropertyChanged(nameof(IsOmdbSelected));
            }
        }
        private string _selectedDataSource = "TMDB_ONLY";

        public bool IsTmdbSelected
        {
            get => SelectedDataSource == "TMDB_ONLY";
            set { if (value) SelectedDataSource = "TMDB_ONLY"; }
        }

        public bool IsOmdbSelected
        {
            get => SelectedDataSource == "OMDB_ONLY";
            set { if (value) SelectedDataSource = "OMDB_ONLY"; }
        }

        public string TmdbLanguage
        {
            get => _tmdbLanguage;
            set
            {
                SetProperty(ref _tmdbLanguage, value);
                OnPropertyChanged(nameof(IsPersianLanguage));
                OnPropertyChanged(nameof(IsEnglishLanguage));
            }
        }
        private string _tmdbLanguage = "fa-IR";

        public bool IsPersianLanguage
        {
            get => TmdbLanguage == "fa-IR";
            set { if (value) TmdbLanguage = "fa-IR"; }
        }

        public bool IsEnglishLanguage
        {
            get => TmdbLanguage == "en-US";
            set { if (value) TmdbLanguage = "en-US"; }
        }

        public ObservableCollection<MovieManagerDesktop.Models.ApiKeyItem> TmdbApiKeys { get; } = new();

        public ObservableCollection<MovieManagerDesktop.Models.ApiKeyItem> OmdbApiKeys { get; } = new();

        public ObservableCollection<MovieManagerDesktop.Models.ApiKeyItem> ApiProxyUrls { get; } = new();

        [ObservableProperty]
        private bool _isApiProxyEnabled;

        [ObservableProperty]
        private string _databaseSizeText = "17 MB";

        [ObservableProperty]
        private string _newTmdbKey = string.Empty;

        [ObservableProperty]
        private string _newOmdbKey = string.Empty;

        [ObservableProperty]
        private string _newProxyUrl = string.Empty;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotGoogleDriveConnected))]
        private bool _isGoogleDriveConnected;

        public bool IsNotGoogleDriveConnected => !IsGoogleDriveConnected;

        [ObservableProperty]
        private bool _isLoadingBackups;

        [ObservableProperty]
        private bool _isUploadingBackup;

        [ObservableProperty]
        private string _backupProgressText;

        [ObservableProperty]
        private double _backupProgressValue;

        public ObservableCollection<MovieManagerDesktop.Services.CloudBackupModel> CloudBackups { get; } = new();

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                SetProperty(ref _selectedTheme, value);
                ApplyTheme(value, IsDarkTheme);
                AutoSaveTheme();
                OnPropertyChanged(nameof(IsCyan));
                OnPropertyChanged(nameof(IsMidnightBlue));
                OnPropertyChanged(nameof(IsOLEDBlack));
            }
        }
        private string _selectedTheme = "Cyan";

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                SetProperty(ref _isDarkTheme, value);
                ApplyTheme(SelectedTheme, value);
                AutoSaveTheme();
                OnPropertyChanged(nameof(IsLightTheme));
            }
        }
        private bool _isDarkTheme = true;

        public bool IsLightTheme
        {
            get => !IsDarkTheme;
            set { IsDarkTheme = !value; }
        }

        public bool IsCyan
        {
            get => SelectedTheme == "Cyan";
            set { if (value) SelectedTheme = "Cyan"; }
        }

        public bool IsMidnightBlue
        {
            get => SelectedTheme == "MidnightBlue";
            set { if (value) SelectedTheme = "MidnightBlue"; }
        }

        public bool IsOLEDBlack
        {
            get => SelectedTheme == "OLEDBlack";
            set { if (value) SelectedTheme = "OLEDBlack"; }
        }

        [ObservableProperty]
        private bool _isLocalAutoBackupEnabled;

        partial void OnIsLocalAutoBackupEnabledChanged(bool value)
        {
            var settings = SettingsManager.LoadSettings();
            settings.IsLocalAutoBackupEnabled = value;
            SettingsManager.SaveSettings(settings);
            LoggerService.Info($"[Backup] Auto local backup: {(value ? "Enabled" : "Disabled")}");
        }

        [ObservableProperty]
        private string _localAutoBackupPath = string.Empty;

        partial void OnLocalAutoBackupPathChanged(string value)
        {
            var settings = SettingsManager.LoadSettings();
            settings.LocalAutoBackupPath = value;
            SettingsManager.SaveSettings(settings);
        }

        [ObservableProperty]
        private bool _isGoogleDriveAutoBackupEnabled;

        partial void OnIsGoogleDriveAutoBackupEnabledChanged(bool value)
        {
            var settings = SettingsManager.LoadSettings();
            settings.IsGoogleDriveAutoBackupEnabled = value;
            SettingsManager.SaveSettings(settings);
            LoggerService.Info($"[Backup] Auto cloud backup: {(value ? "Enabled" : "Disabled")}");
        }

        [ObservableProperty]
        private int _backupFrequencyIndex;

        partial void OnBackupFrequencyIndexChanged(int value)
        {
            var settings = SettingsManager.LoadSettings();
            settings.BackupFrequencyIndex = value;
            SettingsManager.SaveSettings(settings);
        }

        [RelayCommand]
        private void BrowseBackupPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "پوشه پشتیبان‌گیری را انتخاب کنید"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                LocalAutoBackupPath = dialog.FolderName;
                ToastService.Instance.ShowSuccess("مسیر پشتیبان‌گیری خودکار ذخیره شد.");
            }
        }

        public SettingsViewModel()
        {
            var settings = SettingsManager.LoadSettings();
            string loadedSource = settings.SelectedDataSource ?? "TMDB_ONLY";
            if (loadedSource == "FM_DB") loadedSource = "TMDB_ONLY";
            SelectedDataSource = loadedSource;
            
            var defaultTmdb = SettingsManager.DefaultTmdbKeys;
            var savedTmdb = (settings.TmdbApiKey ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim());
            var tmdbKeys = defaultTmdb.Union(savedTmdb).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
            foreach (var key in tmdbKeys) TmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(key));
            
            var defaultOmdb = SettingsManager.DefaultOmdbKeys;
            var savedOmdb = (settings.OmdbApiKey ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim());
            var omdbKeys = defaultOmdb.Union(savedOmdb).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
            foreach (var key in omdbKeys) OmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(key));
            
            var proxyUrls = (settings.ApiProxyUrl ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in proxyUrls) ApiProxyUrls.Add(new MovieManagerDesktop.Models.ApiKeyItem(url.Trim()));
            
            IsApiProxyEnabled = settings.IsApiProxyEnabled && ApiProxyUrls.Count > 0;
            TmdbLanguage = settings.TmdbLanguage ?? "fa-IR";
            _isDarkTheme = settings.IsDarkTheme;
            SelectedTheme = settings.Theme ?? "Cyan"; // This calls ApplyTheme
            
            _isLocalAutoBackupEnabled = settings.IsLocalAutoBackupEnabled;
            _localAutoBackupPath = settings.LocalAutoBackupPath;
            _isGoogleDriveAutoBackupEnabled = settings.IsGoogleDriveAutoBackupEnabled;
            _backupFrequencyIndex = settings.BackupFrequencyIndex;

            _dateFormatOverride = settings.DateFormatOverride ?? "jalali";
            _genreLanguageOverride = settings.GenreLanguageOverride ?? "fa";
            _translateToLanguage = settings.TranslateToLanguage ?? "fa";
            _fetchInfoLanguage = settings.FetchInfoLanguage ?? "fa-IR";
            _showActorImages = settings.ShowActorImages;
            _hideAdultContent = settings.HideAdultContent;
            
            CalculateDatabaseSize();
            CheckGoogleDriveConnection();
            LoadEducationTopics();
        }

        private void CheckGoogleDriveConnection()
        {
            IsGoogleDriveConnected = MovieManagerDesktop.Services.BackupManager.IsConnectedToGoogleDrive();
            if (IsGoogleDriveConnected)
            {
                _ = LoadCloudBackupsInternalAsync(isInitialLoad: true);
            }
        }

        [RelayCommand]
        private async Task ConnectToGoogleDrive()
        {
            try
            {
                LoggerService.Info("[Cloud] 🔑 Starting Google Drive OAuth connection...");
                ToastService.Instance.ShowInfo("در حال باز کردن مرورگر جهت ورود و اتصال به حساب گوگل...");
                await MovieManagerDesktop.Services.BackupManager.ConnectToGoogleDriveAsync();
                CheckGoogleDriveConnection();
                if (IsGoogleDriveConnected)
                {
                    ToastService.Instance.ShowSuccess("اتصال به حساب گوگل با موفقیت انجام شد.");
                    await LoadCloudBackupsAsync();
                }
                else
                {
                    ToastService.Instance.ShowWarning("اتصال به گوگل درایو تایید نشد. لطفاً دسترسی را در مرورگر تایید کنید.");
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error connecting to Google Drive", ex);
                ToastService.Instance.ShowError($"خطا در اتصال به گوگل: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DisconnectGoogleDrive()
        {
            var dialog = new ConfirmDialog("آیا از خروج از حساب گوگل و قطع دسترسی اطمینان دارید؟");
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is bool res && res)
            {
                await MovieManagerDesktop.Services.BackupManager.DisconnectGoogleDriveAsync();
                IsGoogleDriveConnected = false;
                CloudBackups.Clear();
                ToastService.Instance.ShowSuccess("دسترسی به حساب گوگل قطع شد.");
            }
        }

        [RelayCommand]
        private async Task LoadCloudBackupsAsync()
        {
            await LoadCloudBackupsInternalAsync(isInitialLoad: false);
        }

        private async Task LoadCloudBackupsInternalAsync(bool isInitialLoad)
        {
            if (!IsGoogleDriveConnected) return;

            IsLoadingBackups = true;
            try
            {
                var backups = await MovieManagerDesktop.Services.BackupManager.GetDriveBackupsAsync();
                CloudBackups.Clear();
                foreach (var backup in backups)
                {
                    CloudBackups.Add(backup);
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Warning($"Failed to load cloud backups: {ex.Message}");
                if (!isInitialLoad)
                {
                    ToastService.Instance.ShowError($"خطا در دریافت لیست بکاپ‌ها: {ex.Message}");
                }
            }
            finally
            {
                IsLoadingBackups = false;
            }
        }

        [RelayCommand]
        private async Task DownloadAndRestoreCloudBackup(MovieManagerDesktop.Services.CloudBackupModel backup)
        {
            if (backup == null) return;

            var confirmDialog = new ConfirmDialog($"آیا از دانلود و اعمال بکاپ '{backup.Name}' روی دیتابیس فعلی اطمینان دارید؟");
            var result = await DialogHost.Show(confirmDialog, "RootDialog");

            if (result is bool res && res)
            {
                try
                {
                    IsUploadingBackup = true;
                    BackupProgressValue = 0;
                    BackupProgressText = "در حال آماده‌سازی برای دانلود...";

                    var progress = new Progress<double>(percent => 
                    {
                        BackupProgressValue = percent;
                    });
                    
                    var textProgress = new Progress<string>(text => 
                    {
                        BackupProgressText = text;
                    });

                    string tempFile = Path.GetTempFileName();
                    await MovieManagerDesktop.Services.BackupManager.DownloadDriveBackupAsync(backup.Id, tempFile, progress, textProgress, backup.SizeInBytes);
                    
                    BackupProgressText = "دانلود تکمیل شد. در حال ادغام با دیتابیس فعلی...";
                    await ImportJsonFileAsync(tempFile);
                    
                    System.IO.File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.LoggerService.Error("Error restoring cloud backup", ex);
                    ToastService.Instance.ShowError($"خطا در اعمال بکاپ: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(1500);
                    IsUploadingBackup = false;
                }
            }
        }

        [RelayCommand]
        private async Task DeleteCloudBackup(MovieManagerDesktop.Services.CloudBackupModel backup)
        {
            if (backup == null) return;

            var confirmDialog = new ConfirmDialog($"آیا از حذف بکاپ '{backup.Name}' از گوگل درایو اطمینان دارید؟");
            var result = await DialogHost.Show(confirmDialog, "RootDialog");

            if (result is bool res && res)
            {
                try
                {
                    await MovieManagerDesktop.Services.BackupManager.DeleteDriveBackupAsync(backup.Id);
                    CloudBackups.Remove(backup);
                    ToastService.Instance.ShowSuccess("بکاپ با موفقیت از گوگل درایو حذف شد.");
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.LoggerService.Error("Error deleting cloud backup", ex);
                    ToastService.Instance.ShowError($"خطا در حذف بکاپ: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task ShareCloudBackup(MovieManagerDesktop.Services.CloudBackupModel backup)
        {
            if (backup == null) return;

            try
            {
                ToastService.Instance.ShowInfo("در حال ایجاد لینک اشتراک‌گذاری...");
                string link = await MovieManagerDesktop.Services.BackupManager.ShareDriveBackupAsync(backup.Id);
                System.Windows.Clipboard.SetText(link);
                ToastService.Instance.ShowSuccess("لینک دانلود فایل در کلیپ‌بورد کپی شد.");
                
                // Update link in UI if empty
                if (string.IsNullOrEmpty(backup.WebViewLink))
                {
                    backup.WebViewLink = link;
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error sharing cloud backup", ex);
                ToastService.Instance.ShowError($"خطا در ایجاد لینک اشتراک‌گذاری: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddTmdbKey()
        {
            if (string.IsNullOrWhiteSpace(NewTmdbKey)) { ToastService.Instance.ShowError("لطفاً کلید TMDB را وارد کنید."); return; }
            TmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(NewTmdbKey.Trim()));
            NewTmdbKey = string.Empty;
            SaveSettings();
            ToastService.Instance.ShowSuccess("کلید TMDB با موفقیت اضافه شد.");
        }
        
        [RelayCommand]
        private void DeleteTmdbKey(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item != null)
            {
                TmdbApiKeys.Remove(item);
                SaveSettings();
                ToastService.Instance.ShowSuccess("کلید TMDB حذف شد.");
            }
        }

        [RelayCommand]
        private async Task TestTmdbKeyAsync(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key)) { ToastService.Instance.ShowError("کلید خالی است."); return; }
            try
            {
                ToastService.Instance.ShowInfo("در حال تست کلید TMDB...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var client = new System.Net.Http.HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
                client.Timeout = TimeSpan.FromSeconds(15);
                var response = await client.GetAsync($"https://api.themoviedb.org/3/movie/550?api_key={item.Key.Trim()}");
                sw.Stop();
                if (response.IsSuccessStatusCode)
                    ToastService.Instance.ShowSuccess($"کلید TMDB معتبر است ✅ (تاخیر: {sw.ElapsedMilliseconds} میلی‌ثانیه)");
                else
                    ToastService.Instance.ShowError($"کلید TMDB نامعتبر است. کد خطا: {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در تست کلید: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddOmdbKey()
        {
            if (string.IsNullOrWhiteSpace(NewOmdbKey)) { ToastService.Instance.ShowError("لطفاً کلید OMDB را وارد کنید."); return; }
            OmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(NewOmdbKey.Trim()));
            NewOmdbKey = string.Empty;
            SaveSettings();
            ToastService.Instance.ShowSuccess("کلید OMDB با موفقیت اضافه شد.");
        }
        
        [RelayCommand]
        private void DeleteOmdbKey(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item != null)
            {
                OmdbApiKeys.Remove(item);
                SaveSettings();
                ToastService.Instance.ShowSuccess("کلید OMDB حذف شد.");
            }
        }

        [RelayCommand]
        private async Task TestOmdbKeyAsync(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key)) { ToastService.Instance.ShowError("کلید خالی است."); return; }
            try
            {
                ToastService.Instance.ShowInfo("در حال تست کلید OMDB...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var client = new System.Net.Http.HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
                client.Timeout = TimeSpan.FromSeconds(15);
                var response = await client.GetAsync($"https://www.omdbapi.com/?apikey={item.Key.Trim()}&t=inception");
                sw.Stop();
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (content.Contains("True", StringComparison.OrdinalIgnoreCase))
                        ToastService.Instance.ShowSuccess($"کلید OMDB معتبر است ✅ (تاخیر: {sw.ElapsedMilliseconds} میلی‌ثانیه)");
                    else
                        ToastService.Instance.ShowError("کلید OMDB نامعتبر است یا پاسخ صحیحی دریافت نشد.");
                }
                else
                    ToastService.Instance.ShowError($"کلید OMDB نامعتبر است. کد خطا: {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در تست کلید: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddProxyUrl()
        {
            if (string.IsNullOrWhiteSpace(NewProxyUrl)) { ToastService.Instance.ShowError("لطفاً آدرس پروکسی را وارد کنید."); return; }
            ApiProxyUrls.Add(new MovieManagerDesktop.Models.ApiKeyItem(NewProxyUrl.Trim()));
            NewProxyUrl = string.Empty;
            SaveSettings();
            ToastService.Instance.ShowSuccess("آدرس پروکسی جدید با موفقیت اضافه شد.");
        }
        
        [RelayCommand]
        private void DeleteProxyUrl(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item != null)
            {
                ApiProxyUrls.Remove(item);
                SaveSettings();
                ToastService.Instance.ShowSuccess("آدرس پروکسی حذف شد.");
            }
        }

        [RelayCommand]
        private async Task TestProxyUrlAsync(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
            {
                ToastService.Instance.ShowError("آدرس پروکسی خالی است.");
                return;
            }

            try
            {
                ToastService.Instance.ShowInfo("در حال تست اتصال به سرور ورکر...");
                string targetUrl = item.Key.Trim();
                if (!targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    targetUrl = "https://" + targetUrl;

                string testEndpoint;
                if (targetUrl.Contains("?"))
                    testEndpoint = $"{targetUrl}&url=" + Uri.EscapeDataString("https://api.themoviedb.org/3/configuration");
                else
                    testEndpoint = $"{targetUrl.TrimEnd('/')}/?url=" + Uri.EscapeDataString("https://api.themoviedb.org/3/configuration");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                var response = await client.GetAsync(testEndpoint);
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    ToastService.Instance.ShowSuccess($"اتصال با موفقیت برقرار شد ✅ (تاخیر: {sw.ElapsedMilliseconds} میلی‌ثانیه)");
                }
                else
                {
                    ToastService.Instance.ShowWarning($"پاسخ از سرور دریافت شد اما وضعیت ناموفق بود: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در برقراری اتصال به پروکسی: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SyncProxiesFromCloudAsync(bool showToast = true)
        {
            if (showToast) ToastService.Instance.ShowInfo("در حال دریافت سرورهای ضدتحریم از منبع ابری...");
            var (success, count, message) = await SettingsManager.SyncEncryptedProxiesAsync(force: true);
            
            if (showToast)
            {
                if (success)
                {
                    if (count > 0)
                    {
                        ToastService.Instance.ShowSuccess(message);
                    }
                    else
                    {
                        ToastService.Instance.ShowWarning(message);
                    }
                }
                else
                {
                    ToastService.Instance.ShowError(message);
                }
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var settings = SettingsManager.LoadSettings();
            
            settings.SelectedDataSource = SelectedDataSource;
            settings.TmdbApiKey = string.Join(",", TmdbApiKeys.Select(k => k.Key).Where(k => !string.IsNullOrWhiteSpace(k)));
            settings.OmdbApiKey = string.Join(",", OmdbApiKeys.Select(k => k.Key).Where(k => !string.IsNullOrWhiteSpace(k)));
            settings.ApiProxyUrl = string.Join(",", ApiProxyUrls.Select(k => k.Key).Where(k => !string.IsNullOrWhiteSpace(k)));
            settings.IsApiProxyEnabled = IsApiProxyEnabled;
            settings.TmdbLanguage = TmdbLanguage;
            settings.Theme = SelectedTheme;
            settings.IsDarkTheme = IsDarkTheme;
            
            settings.IsLocalAutoBackupEnabled = IsLocalAutoBackupEnabled;
            settings.LocalAutoBackupPath = LocalAutoBackupPath;
            settings.IsGoogleDriveAutoBackupEnabled = IsGoogleDriveAutoBackupEnabled;
            settings.BackupFrequencyIndex = BackupFrequencyIndex;
            
            SettingsManager.SaveSettings(settings);
            StatusMessage = "تنظیمات با موفقیت ذخیره شد.";
            // clear after 3 seconds
            Task.Delay(3000).ContinueWith(_ => StatusMessage = string.Empty, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void AutoSaveTheme()
        {
            var settings = SettingsManager.LoadSettings();
            settings.IsDarkTheme = IsDarkTheme;
            settings.Theme = SelectedTheme;
            SettingsManager.SaveSettings(settings);
        }

        private void ApplyTheme(string themeName, bool isDark)
        {
            var paletteHelper = new PaletteHelper();
            MaterialDesignThemes.Wpf.Theme theme;
            
            var baseTheme = isDark ? BaseTheme.Dark : BaseTheme.Light;
            System.Windows.Media.Color primaryColor;
            System.Windows.Media.Color secondaryColor;
            
            if (themeName == "Cyan")
            {
                primaryColor = System.Windows.Media.Color.FromRgb(0, 180, 216); // Cyan #00B4D8
                secondaryColor = System.Windows.Media.Color.FromRgb(58, 134, 255); // Blue #3A86FF
                theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
            }
            else if (themeName == "MidnightBlue")
            {
                primaryColor = System.Windows.Media.Color.FromRgb(25, 118, 210);
                secondaryColor = System.Windows.Media.Color.FromRgb(3, 169, 244);
                theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
            }
            else // OLEDBlack
            {
                primaryColor = System.Windows.Media.Color.FromRgb(33, 33, 33);
                secondaryColor = System.Windows.Media.Color.FromRgb(158, 158, 158);
                theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
            }

            paletteHelper.SetTheme(theme);
            
            // Swap our custom DesignSystem light/dark resource
            var appDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            var existingLightDict = appDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("DesignSystem.Light.xaml"));
            
            if (isDark)
            {
                if (existingLightDict != null)
                {
                    appDictionaries.Remove(existingLightDict);
                }
            }
            else
            {
                if (existingLightDict == null)
                {
                    appDictionaries.Add(new System.Windows.ResourceDictionary { Source = new System.Uri("pack://application:,,,/MovieManagerDesktop;component/Themes/DesignSystem.Light.xaml") });
                }
            }
        }

        [RelayCommand]
        private async Task ClearDatabase()
        {
            var dialog = new ConfirmDialog("آیا از حذف کامل دیتابیس اطمینان دارید؟ تمام فیلم‌ها پاک خواهند شد!");
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is bool res && res)
            {
                try
                {
                    // Force garbage collection to release any orphaned DbContexts
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    // Force SQLite to close all open file handles in the connection pool
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    
                    using var db = new AppDbContext();
                    
                    // Completely drop the database file and recreate it.
                    // ClearAllPools and GC above ensure the file is not locked.
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                    
                    ToastService.Instance.ShowSuccess("دیتابیس با موفقیت خالی شد.");
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.LoggerService.Error("Error clearing database", ex);
                    ToastService.Instance.ShowError($"خطا در پاک‌سازی: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task ExportJson()
        {
            try
            {
                using var db = new AppDbContext();
                var data = db.VideoFiles.ToList();
                
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = "json",
                    FileName = $"CineTrack_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var backupModel = new MovieManagerDesktop.Services.BackupManager.FullBackupModel
                    {
                        VideoFiles = db.VideoFiles.ToList(),
                        TvSeasons = db.TvSeasons.ToList(),
                        TvEpisodes = db.TvEpisodes.ToList(),
                        Settings = SettingsManager.LoadSettings()
                    };
                    var json = JsonSerializer.Serialize(backupModel, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(dialog.FileName, json);
                    ToastService.Instance.ShowSuccess("نسخه پشتیبان جامع با موفقیت صادر شد.");
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error exporting json", ex);
                ToastService.Instance.ShowError($"خطا در خروجی گرفتن: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task BackupDatabase()
        {
            try
            {
                LoggerService.Info("[Backup] 💾 Initializing local database backup export...");
                string? selectedPath = null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "JSON Backup File (*.json)|*.json",
                        FileName = $"MovieManager_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                        Title = "ذخیره فایل پشتیبان محلی دیتابیس"
                    };

                    if (saveDialog.ShowDialog(Application.Current.MainWindow) == true)
                    {
                        selectedPath = saveDialog.FileName;
                    }
                });

                if (string.IsNullOrEmpty(selectedPath))
                {
                    LoggerService.Info("[Backup] Backup cancelled by user.");
                    return;
                }

                ToastService.Instance.ShowInfo("در حال تهیه نسخه پشتیبان...");
                var json = await MovieManagerDesktop.Services.BackupManager.GenerateBackupJsonAsync();
                await File.WriteAllTextAsync(selectedPath, json);

                long fileLength = new FileInfo(selectedPath).Length;
                string formattedSize = fileLength > 1024 * 1024 
                    ? $"{(fileLength / 1024f / 1024f):F1} MB" 
                    : $"{(fileLength / 1024f):F1} KB";

                LoggerService.Info($"[Backup] 💾 Local backup saved successfully: {selectedPath} ({formattedSize})");
                ToastService.Instance.ShowSuccess($"فایل پشتیبان با موفقیت ایجاد شد ({formattedSize}).");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error generating local backup", ex);
                ToastService.Instance.ShowError($"خطا در ایجاد پشتیبان: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RestoreDatabase()
        {
            try
            {
                LoggerService.Info("[Backup] 📥 Initializing local database restore...");
                string? selectedPath = null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var openDialog = new OpenFileDialog
                    {
                        Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                        DefaultExt = "json",
                        Title = "انتخاب فایل پشتیبان JSON برای بازیابی"
                    };

                    if (openDialog.ShowDialog(Application.Current.MainWindow) == true)
                    {
                        selectedPath = openDialog.FileName;
                    }
                });

                if (string.IsNullOrEmpty(selectedPath))
                {
                    LoggerService.Info("[Backup] Restore cancelled by user.");
                    return;
                }

                await ImportJsonFileAsync(selectedPath);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error opening restore dialog", ex);
                ToastService.Instance.ShowError($"خطا در باز کردن فایل: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task UploadToGoogleDrive()
        {
            try
            {
                if (!IsGoogleDriveConnected)
                {
                    LoggerService.Info("[Cloud] Connecting to Google Drive before upload...");
                    await ConnectToGoogleDrive();
                    if (!IsGoogleDriveConnected)
                    {
                        ToastService.Instance.ShowWarning("جهت آپلود نسخه پشتیبان در گوگل درایو، ابتدا اتصال به حساب را تایید کنید.");
                        return;
                    }
                }

                IsUploadingBackup = true;
                BackupProgressValue = 0;
                BackupProgressText = "شروع عملیات پشتیبان‌گیری ابری...";

                var progress = new Progress<double>(percent => 
                {
                    BackupProgressValue = percent;
                });
                
                var textProgress = new Progress<string>(text => 
                {
                    BackupProgressText = text;
                });

                await MovieManagerDesktop.Services.BackupManager.ForceGoogleDriveBackupAsync(progress, textProgress);
                
                ToastService.Instance.ShowSuccess("فایل پشتیبان با موفقیت در گوگل درایو بارگذاری شد.");
                
                await LoadCloudBackupsAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error uploading backup to Google Drive", ex);
                ToastService.Instance.ShowError($"خطا در آپلود بکاپ: {ex.Message}");
            }
            finally
            {
                await Task.Delay(1500);
                IsUploadingBackup = false;
            }
        }

        [RelayCommand]
        private async Task RestoreFromGoogleDrive(MovieManagerDesktop.Services.CloudBackupModel backup)
        {
            await DownloadAndRestoreCloudBackup(backup);
        }

        [RelayCommand]
        private async Task ImportJson()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = "json",
                    Title = "انتخاب فایل پشتیبان JSON"
                };

                if (dialog.ShowDialog() == true)
                {
                    await ImportJsonFileAsync(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error selecting json backup file", ex);
                ToastService.Instance.ShowError($"خطا در انتخاب فایل: {ex.Message}");
            }
        }

        private async Task ImportJsonFileAsync(string filePath)
        {
            try
            {
                LoggerService.Info($"[Backup] 📥 Reading backup file: {filePath}");
                var json = await File.ReadAllTextAsync(filePath);
                
                System.Collections.Generic.List<Models.VideoFile> videoFiles = new();
                System.Collections.Generic.List<Models.TvSeason> tvSeasons = new();
                System.Collections.Generic.List<Models.TvEpisode> tvEpisodes = new();
                SettingsModel importedSettings = null;

                if (json.TrimStart().StartsWith("["))
                {
                    // Old format (just VideoFiles list)
                    var oldData = JsonSerializer.Deserialize<System.Collections.Generic.List<Models.VideoFile>>(json);
                    if (oldData != null) videoFiles = oldData;
                }
                else
                {
                    // New format (FullBackupModel)
                    var fullData = JsonSerializer.Deserialize<MovieManagerDesktop.Services.BackupManager.FullBackupModel>(json);
                    if (fullData != null)
                    {
                        if (fullData.VideoFiles != null) videoFiles = fullData.VideoFiles;
                        if (fullData.TvSeasons != null) tvSeasons = fullData.TvSeasons;
                        if (fullData.TvEpisodes != null) tvEpisodes = fullData.TvEpisodes;
                        importedSettings = fullData.Settings;
                    }
                }
                
                if (videoFiles.Any())
                {
                    var confirmDialog = new ConfirmDialog($"تعداد {videoFiles.Count} فیلم/سریال در این فایل وجود دارد. آیا مایل به ادغام و بروزرسانی تمامی اطلاعات، وضعیت‌های تماشا و ادامه پخش با دیتابیس فعلی هستید؟");
                    var result = await DialogHost.Show(confirmDialog, "RootDialog");

                    if (result is bool res && res)
                    {
                        ToastService.Instance.ShowInfo("در حال ادغام و بروزرسانی اطلاعات دیتابیس...");
                        using var db = new AppDbContext();
                        
                        // Import VideoFiles with Full Merge (Update existing + Insert new)
                        var existingVideos = await db.VideoFiles.ToListAsync();
                        var existingMap = existingVideos.ToDictionary(v => v.Id);
                        int insertedCount = 0;
                        int updatedCount = 0;

                        foreach (var incoming in videoFiles)
                        {
                            if (existingMap.TryGetValue(incoming.Id, out var existing))
                            {
                                // Merge watch state, continue watching, ratings, etc.
                                existing.IsWatched = incoming.IsWatched;
                                existing.WatchProgressSeconds = incoming.WatchProgressSeconds;
                                existing.WatchProgressPercent = incoming.WatchProgressPercent;
                                existing.TotalDurationSeconds = incoming.TotalDurationSeconds;
                                existing.LastPlayedEpisode = incoming.LastPlayedEpisode;
                                existing.LastPlayedAt = incoming.LastPlayedAt;
                                existing.IsFavorite = incoming.IsFavorite;
                                existing.IsWatchlist = incoming.IsWatchlist;
                                existing.IsTracked = incoming.IsTracked;
                                existing.CustomTags = incoming.CustomTags;
                                existing.CollectionName = incoming.CollectionName;
                                if (!string.IsNullOrEmpty(incoming.PosterUrl)) existing.PosterUrl = incoming.PosterUrl;
                                if (!string.IsNullOrEmpty(incoming.BackdropUrl)) existing.BackdropUrl = incoming.BackdropUrl;
                                updatedCount++;
                            }
                            else
                            {
                                db.VideoFiles.Add(incoming);
                                insertedCount++;
                            }
                        }

                        // Import TvSeasons
                        var existingSeasonIds = db.TvSeasons.Select(s => s.Id).ToHashSet();
                        foreach (var season in tvSeasons)
                        {
                            if (!existingSeasonIds.Contains(season.Id))
                            {
                                db.TvSeasons.Add(season);
                            }
                        }

                        // Import TvEpisodes with watch state merge
                        var existingEpisodes = await db.TvEpisodes.ToListAsync();
                        var episodeMap = existingEpisodes.ToDictionary(e => e.Id);
                        foreach (var incomingEp in tvEpisodes)
                        {
                            if (episodeMap.TryGetValue(incomingEp.Id, out var existingEp))
                            {
                                existingEp.IsWatched = incomingEp.IsWatched;
                            }
                            else
                            {
                                db.TvEpisodes.Add(incomingEp);
                            }
                        }
                        
                        await db.SaveChangesAsync();

                        LoggerService.Info($"[Backup] ✔ Database restore completed: {insertedCount} new items, {updatedCount} updated items.");

                        // Restore Settings if present
                        if (importedSettings != null)
                        {
                            SettingsManager.SaveSettings(importedSettings);
                            SelectedDataSource = importedSettings.SelectedDataSource ?? "TMDB_ONLY";
                            
                            TmdbApiKeys.Clear();
                            var tmdbKeys = string.IsNullOrWhiteSpace(importedSettings.TmdbApiKey) 
                                ? SettingsManager.DefaultTmdbKeys 
                                : importedSettings.TmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim());
                            foreach (var key in tmdbKeys) TmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(key));
                            
                            OmdbApiKeys.Clear();
                            var omdbKeys = string.IsNullOrWhiteSpace(importedSettings.OmdbApiKey) 
                                ? SettingsManager.DefaultOmdbKeys 
                                : importedSettings.OmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim());
                            foreach (var key in omdbKeys) OmdbApiKeys.Add(new MovieManagerDesktop.Models.ApiKeyItem(key));
                            
                            ApiProxyUrls.Clear();
                            var proxyUrls = (importedSettings.ApiProxyUrl ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var url in proxyUrls) ApiProxyUrls.Add(new MovieManagerDesktop.Models.ApiKeyItem(url.Trim()));
                            
                            TmdbLanguage = importedSettings.TmdbLanguage ?? "fa-IR";
                            IsDarkTheme = importedSettings.IsDarkTheme;
                            SelectedTheme = importedSettings.Theme ?? "Cyan";
                            LoggerService.Info("[Backup] ✔ Application settings restored.");
                        }
                        
                        ToastService.Instance.ShowSuccess($"اطلاعات پشتیبان با موفقیت بازیابی شد ({insertedCount} جدید، {updatedCount} بروزرسانی).");
                        
                        // Send message to refresh lists and continue watching across all views
                        WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                    }
                }
                else
                {
                    ToastService.Instance.ShowError("هیچ اطلاعات معتبری در فایل یافت نشد.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error importing json content", ex);
                ToastService.Instance.ShowError($"خطا در بازیابی اطلاعات: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task TestProxyAsync(MovieManagerDesktop.Models.ApiKeyItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
            {
                ToastService.Instance.ShowError("ابتدا آدرس ورکر را وارد کنید.");
                return;
            }

            try
            {
                ToastService.Instance.ShowInfo("در حال بررسی اتصال به پروکسی...");
                
                string proxy = item.Key.Trim().TrimEnd('/');
                if (proxy.Contains("?"))
                {
                    if (!proxy.EndsWith("url=")) proxy += "&url=";
                }
                else
                {
                    proxy += "/?url=";
                }
                
                // We use Google's 204 endpoint for a fast, reliable test
                string testUrl = proxy + Uri.EscapeDataString("https://www.google.com/generate_204");
                
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "MovieManagerDesktop");
                client.Timeout = TimeSpan.FromSeconds(15);
                
                var response = await client.GetAsync(testUrl);
                
                // 204 No Content is exactly what google generate_204 should return
                if (response.IsSuccessStatusCode)
                {
                    ToastService.Instance.ShowSuccess("اتصال موفق! پروکسی به درستی کار می‌کند.");
                }
                else
                {
                    ToastService.Instance.ShowError($"خطا: پروکسی متصل شد اما پاسخ نامعتبر بود. کد خطا: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error testing proxy", ex);
                ToastService.Instance.ShowError($"اتصال ناموفق بود: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyWorkerCode()
        {
            string workerCode = @"export default {
    async fetch(request, env, ctx) {
      if (request.method === ""OPTIONS"") {
        return new Response(null, {
          headers: {
            ""Access-Control-Allow-Origin"": ""*"",
            ""Access-Control-Allow-Methods"": ""GET, POST, PUT, DELETE, OPTIONS"",
            ""Access-Control-Allow-Headers"": request.headers.get(""Access-Control-Request-Headers"") || ""*"",
          },
        });
      }
      const url = new URL(request.url);
      const targetUrl = url.searchParams.get(""url"");
      if (!targetUrl) return new Response(""Missing 'url' query parameter."", { status: 400 });
      try {
        const headers = new Headers(request.headers);
        headers.delete(""Host""); headers.delete(""Origin""); headers.delete(""Referer"");
        const response = await fetch(new Request(targetUrl, {
          method: request.method, headers: headers, body: request.body, redirect: ""follow"",
        }));
        const modifiedResponse = new Response(response.body, response);
        modifiedResponse.headers.set(""Access-Control-Allow-Origin"", ""*"");
        modifiedResponse.headers.delete(""X-Frame-Options"");
        modifiedResponse.headers.delete(""Content-Security-Policy"");
        return modifiedResponse;
      } catch (error) { return new Response(`Proxy Error: ${error.message}`, { status: 500 }); }
    },
};";
            try
            {
                System.Windows.Clipboard.SetText(workerCode);
                ToastService.Instance.ShowSuccess("کد ورکر در کلیپ‌بورد کپی شد.");
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error copying worker code", ex);
                ToastService.Instance.ShowError("خطا در کپی کردن کد.");
            }
        }



        [RelayCommand]
        private void BackToMain()
        {
            if (CurrentSubView != SettingsSubView.Main)
            {
                CurrentSubView = SettingsSubView.Main;
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
            }
        }

        [RelayCommand]
        private void ToggleTopicExpanded(EducationTopicItem item)
        {
            if (item != null)
            {
                item.IsExpanded = !item.IsExpanded;
            }
        }

        [RelayCommand]
        private async Task ClearAllDatabaseDataAsync()
        {
            var dialog = new ConfirmDialog("آیا از حذف تمام اطلاعات فیلم‌ها و سریال‌های دیتابیس اطمینان دارید؟ این عملیات تمام اطلاعات اسکن‌شده را پاک می‌کند.");
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is bool res && res)
            {
                try
                {
                    using var db = new AppDbContext();
                    db.VideoFiles.RemoveRange(db.VideoFiles);
                    db.TvEpisodes.RemoveRange(db.TvEpisodes);
                    db.TvSeasons.RemoveRange(db.TvSeasons);
                    await db.SaveChangesAsync();

                    CalculateDatabaseSize();
                    ToastService.Instance.ShowSuccess("اطلاعات دیتابیس با موفقیت پاکسازی شد.");
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.LoggerService.Error("Error clearing database", ex);
                    ToastService.Instance.ShowError($"خطا در پاکسازی دیتابیس: {ex.Message}");
                }
            }
        }

        private void LoadEducationTopics()
        {
            EducationTopics.Clear();
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "📅 تقویم و ثبت یادآوری پخش",
                Description = "آموزش جستجو و تنظیم یادآوری برای فیلم‌ها و سریال‌ها",
                Content = "۱. از منوی اصلی وارد بخش «تقویم» شوید.\n۲. روی آیکون زنگوله (افزودن یادآوری) کلیک کنید.\n۳. فیلم یا سریال مورد نظر خود را جستجو کنید.\n۴. تاریخ و ساعت یادآوری را تنظیم کرده و دکمه ذخیره را بزنید.",
                IconKind = "CalendarMonth",
                IconColor = "#EB3B5A"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "📱 اسکن درایوها و شناسایی فیلم‌ها",
                Description = "مراحل کامل اسکن هارد، شناسایی عنوان‌ها و اضافه کردن به کتابخانه",
                Content = "۱. روی آیکون ذره‌بین یا در صفحه اصلی روی گزینه «اسکن» کلیک کنید.\n۲. گزینه اسکن کل درایو یا اسکن یک پوشه خاص را انتخاب کنید. پس از اسکن، نام فیلم‌ها و سریال‌های موجود شناسایی شده و نمایش داده می‌شوند.\n۳. در مرحله شناسایی، روی دکمه «شناسایی» کلیک کرده یا موارد را انتخاب و تایید کنید. برای ویرایش یا جستجوی دستی می‌توانید روی آیکون مداد بزنید.\n۴. پس از ثبت آیتم‌های مورد نظر وارد مرحله بعد شده و روی «سازماندهی» کلیک کنید. پس از اتمام، فیلم‌ها و سریال‌ها در کتابخانه شما قرار می‌گیرند.",
                IconKind = "FolderZip",
                IconColor = "#8854D0"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🔄 بروزرسانی اطلاعات کتابخانه",
                Description = "نحوه دریافت پوسترها، خلاصه‌ها و امتیازات جدید برای فیلم‌ها و سریال‌ها",
                Content = "۱. وارد بخش «کتابخانه» شوید.\n۲. برای بروزرسانی یک آیتم مشخص، وارد صفحه جزئیات آن شده و گزینه «بروزرسانی اطلاعات» را بزنید.\n۳. جهت بروزرسانی همزمان آیتم‌ها، روی آیکون بروزرسانی در بالای صفحه کلیک کنید.",
                IconKind = "Refresh",
                IconColor = "#26DE81"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🏷️ انجام عملیات و دسته‌بندی گروهی",
                Description = "نحوه انتخاب چندتایی پوسترها و اعمال تغییرات همزمان",
                Content = "۱. در صفحه کتابخانه، روی یک پوستر کلیک طولانی (یا دکمه انتخاب گروهی) کنید تا حالت انتخاب چندتایی فعال شود.\n۲. فیلم‌ها و سریال‌های مورد نظر خود را علامت بزنید.\n۳. از نوار ابزار پایین، عملیات دلخواه (مانند افزودن به دسته، تغییر وضعیت تماشا، حذف و...) را انتخاب کنید.",
                IconKind = "TagMultipleOutline",
                IconColor = "#FFB800"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🗑️ حذف فیلم‌ها و پاکسازی کش برنامه",
                Description = "حذف آیتم‌ها از کتابخانه یا فایل‌های اصلی حافظه و مدیریت فضای ذخیره‌سازی",
                Content = "۱. برای حذف یک فیلم از کتابخانه، وارد صفحه جزئیات شده و گزینه «حذف» را انتخاب کنید.\n۲. در پنجره تایید، می‌توانید انتخاب کنید که فقط اطلاعات از دیتابیس برنامه پاک شود یا فایل اصلی ویدیو نیز از حافظه حذف گردد.\n۳. جهت پاکسازی تصویرهای کش شده و آزادسازی حافظه، از بخش ابزارها دکمه «پاکسازی کش تصاویر» را بزنید.",
                IconKind = "DeleteSweepOutline",
                IconColor = "#EB3B5A"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🔑 تنظیمات کلیدهای اختصاصی API و منبع اطلاعات",
                Description = "افزایش سرعت دریافت اطلاعات با وارد کردن کلیدهای اختصاصی TMDb/OMDb",
                Content = "۱. از منوی تنظیمات وارد بخش «منابع داده و API» شوید.\n۲. برنامه به صورت پیش‌فرض دارای کلیدهای استاندارد است، اما برای سرعت اختصاصی و عدم برخورد به محدودیت، می‌توانید کلید اختصاصی خود را وارد کنید.\n۳. با زدن دکمه (؟) راهنمای دریافت کلید رایگان TMDb و OMDb را مشاهده کنید.\n۴. همچنین می‌توانید منبع اصلی دریافت اطلاعات آنلاین را بین TMDb و OMDb تغییر دهید.",
                IconKind = "KeyOutline",
                IconColor = "#8854D0"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "📌 ردیاب دستی سریال‌ها (بدون نیاز به فایل)",
                Description = "پیگیری وضعیت مشاهده فصل‌ها و قسمت‌های سریال‌های در حال پخش",
                Content = "۱. وارد بخش «ردیاب سریال» از منوی اصلی شوید.\n۲. با زدن دکمه (+)، سریال مورد علاقه خود را به صورت آنلاین جستجو کرده و به ردیاب اضافه کنید (نیازی نیست فایل آن را روی سیستم داشته باشید).\n۳. با کلیک روی هر فصل، قسمت‌های تماشا شده را علامت بزنید.\n۴. برنامه درصد پیشرفت تماشای شما و تعداد قسمت‌های باقی‌مانده را محاسبه کرده و زمان پخش قسمت بعدی را نمایش می‌دهد.",
                IconKind = "BookmarkOutline",
                IconColor = "#EB3B5A"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "☁️ پشتیبان‌گیری و بازیابی ابری (Google Drive)",
                Description = "ذخیره‌سازی امن تمام اطلاعات کتابخانه، برچسب‌ها و وضعیت‌های تماشا در ابر",
                Content = "۱. وارد بخش «تنظیمات -> پشتیبان‌گیری و بازیابی» شوید.\n۲. جهت ذخیره در حافظه سیستم، گزینه «ایجاد فایل پشتیبان» را بزنید تا فایل JSON ساخته شود.\n۳. جهت ذخیره در ابر، با حساب گوگل خود وارد شده و دکمه «ایجاد نسخه پشتیبان ابری جدید» را انتخاب کنید.\n۴. در هنگام نصب مجدد برنامه، تنها با زدن دکمه «بازیابی»، کل اطلاعات کتابخانه شما به حالت اول بازمی‌گردد.",
                IconKind = "CloudUploadOutline",
                IconColor = "#3867D6"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🌍 تنظیمات زبان و ترجمه آنلاین داستان",
                Description = "دریافت خلاصه‌ها به زبان دلخواه و ترجمه فوری با موتور گوگل",
                Content = "۱. در بخش تنظیمات، زبان دریافت اطلاعات را روی «فارسی»، «انگلیسی» یا «خودکار» قرار دهید.\n۲. اگر داستان فیلمی به زبان انگلیسی بود، در صفحه جزئیات فیلم روی دکمه «ترجمه داستان» کلیک کنید.\n۳. برنامه با استفاده از سرویس ترجمه پرسرعت گوگل، خلاصه داستان را در کسر از ثانیه به فارسی روان ترجمه کرده و نمایش می‌دهد.",
                IconKind = "Translate",
                IconColor = "#8854D0"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🪄 ترمیم هوشمند مسیر",
                Description = "تعمیر خودکار آدرس فایل‌هایی که جابه‌جا شده‌اند",
                Content = "اگر فایل‌های ویدئویی خود را جابه‌جا کنید یا نام درایو شما تغییر کند، با رفتن به بخش «ابزارها» و انتخاب «ترمیم هوشمند مسیر»، برنامه به صورت اتوماتیک مکان جدید فایل‌ها را پیدا کرده و پایگاه داده را بروزرسانی می‌کند؛ بدون اینکه سابقه تماشا (Watched) یا پوسترهای شما حذف شوند!",
                IconKind = "AutoFix",
                IconColor = "#EB3B5A"
            });
            EducationTopics.Add(new EducationTopicItem
            {
                Title = "🔞 مدیریت و فیلتر محتوای +18 و حریم خصوصی",
                Description = "آموزش نمایش یا مخفی‌سازی فیلم‌ها و سریال‌های بزرگسال در لیست آرشیو",
                Content = "۱. از منوی تنظیمات وارد بخش «شخصی‌سازی» شوید.\n۲. گزینه «نمایش محتوای +18 در لیست» را مشاهده می‌کنید.\n۳. با غیرفعال کردن این گزینه، تمام عناوین بزرگسال (+18) از لیست کتابخانه، صفحه اصلی و جستجو مخفی می‌شوند.\n۴. فیلم‌ها و سریال‌های استاندارد (مانند عناوینی با رده سنی R یا TV-MA) به عنوان بزرگسال تلقی نمی‌شوند و فقط محتوای اختصاصی +18 مشمول این فیلتر خواهد بود.",
                IconKind = "EyeOutline",
                IconColor = "#EB3B5A"
            });
        }

        private void CalculateDatabaseSize()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "moviemanager.db");
                if (File.Exists(dbPath))
                {
                    var info = new FileInfo(dbPath);
                    double mb = (double)info.Length / (1024 * 1024);
                    DatabaseSizeText = $"{Math.Max(1, Math.Round(mb, 1))} MB";
                }
                else
                {
                    DatabaseSizeText = "17 MB";
                }
            }
            catch
            {
                DatabaseSizeText = "17 MB";
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentSubView != SettingsSubView.Main)
            {
                CurrentSubView = SettingsSubView.Main;
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
            }
        }
    }
}
