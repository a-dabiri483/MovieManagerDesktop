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
    public partial class SettingsViewModel : ObservableObject
    {
        public string SelectedDataSource
        {
            get => _selectedDataSource;
            set
            {
                SetProperty(ref _selectedDataSource, value);
                OnPropertyChanged(nameof(IsFmDbSelected));
                OnPropertyChanged(nameof(IsTmdbSelected));
                OnPropertyChanged(nameof(IsOmdbSelected));
            }
        }
        private string _selectedDataSource = "FM_DB";

        public bool IsFmDbSelected
        {
            get => SelectedDataSource == "FM_DB";
            set { if (value) SelectedDataSource = "FM_DB"; }
        }

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

        [ObservableProperty]
        private string _tmdbApiKey;

        [ObservableProperty]
        private string _omdbApiKey;

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

        [ObservableProperty]
        private string _localAutoBackupPath = string.Empty;

        [ObservableProperty]
        private bool _isGoogleDriveAutoBackupEnabled;

        [ObservableProperty]
        private int _backupFrequencyIndex;

        [RelayCommand]
        private void BrowseBackupPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "پوشه پشتیبان‌گیری را انتخاب کنید"
            };

            if (dialog.ShowDialog() == true)
            {
                LocalAutoBackupPath = dialog.FolderName;
            }
        }

        public SettingsViewModel()
        {
            var settings = SettingsManager.LoadSettings();
            SelectedDataSource = settings.SelectedDataSource ?? "FM_DB";
            TmdbApiKey = settings.TmdbApiKey;
            OmdbApiKey = settings.OmdbApiKey;
            TmdbLanguage = settings.TmdbLanguage ?? "fa-IR";
            _isDarkTheme = settings.IsDarkTheme;
            SelectedTheme = settings.Theme ?? "Cyan"; // This calls ApplyTheme
            
            _isLocalAutoBackupEnabled = settings.IsLocalAutoBackupEnabled;
            _localAutoBackupPath = settings.LocalAutoBackupPath;
            _isGoogleDriveAutoBackupEnabled = settings.IsGoogleDriveAutoBackupEnabled;
            _backupFrequencyIndex = settings.BackupFrequencyIndex;

            CheckGoogleDriveConnection();
        }

        private void CheckGoogleDriveConnection()
        {
            IsGoogleDriveConnected = MovieManagerDesktop.Services.BackupManager.IsConnectedToGoogleDrive();
            if (IsGoogleDriveConnected)
            {
                _ = LoadCloudBackupsAsync();
            }
        }

        [RelayCommand]
        private async Task ConnectToGoogleDrive()
        {
            try
            {
                await MovieManagerDesktop.Services.BackupManager.ConnectToGoogleDriveAsync();
                CheckGoogleDriveConnection();
                ToastService.Instance.ShowSuccess("اتصال به حساب گوگل با موفقیت انجام شد.");
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
                MovieManagerDesktop.Services.LoggerService.Error("Error loading cloud backups", ex);
                ToastService.Instance.ShowError($"خطا در دریافت لیست بکاپ‌ها: {ex.Message}");
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
        private void SaveSettings()
        {
            var settings = SettingsManager.LoadSettings();
            
            settings.SelectedDataSource = SelectedDataSource;
            settings.TmdbApiKey = TmdbApiKey;
            settings.OmdbApiKey = OmdbApiKey;
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
        private async Task TestGoogleDrive()
        {
            try
            {
                IsUploadingBackup = true;
                BackupProgressValue = 0;
                BackupProgressText = "شروع عملیات پشتیبان‌گیری...";

                var progress = new Progress<double>(percent => 
                {
                    BackupProgressValue = percent;
                });
                
                var textProgress = new Progress<string>(text => 
                {
                    BackupProgressText = text;
                });

                await MovieManagerDesktop.Services.BackupManager.ForceGoogleDriveBackupAsync(progress, textProgress);
                
                ToastService.Instance.ShowSuccess("فایل بکاپ با موفقیت در گوگل درایو آپلود شد.");
                
                // Refresh list if connected
                if (IsGoogleDriveConnected)
                {
                    await LoadCloudBackupsAsync();
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error testing Google Drive", ex);
                ToastService.Instance.ShowError($"خطا در آپلود بکاپ: {ex.Message}");
            }
            finally
            {
                // Wait a bit before hiding the progress bar so user can see it reached 100%
                await Task.Delay(2000);
                IsUploadingBackup = false;
            }
        }

        [RelayCommand]
        private async Task ImportJson()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = "json"
                };

                if (dialog.ShowDialog() == true)
                {
                    await ImportJsonFileAsync(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error importing json dialog", ex);
                ToastService.Instance.ShowError($"خطا در انتخاب فایل: {ex.Message}");
            }
        }

        private async Task ImportJsonFileAsync(string filePath)
        {
            try
            {
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
                    var confirmDialog = new ConfirmDialog($"تعداد {videoFiles.Count} فیلم/سریال در این فایل وجود دارد. آیا مایل به ادغام تمامی اطلاعات بک‌آپ با دیتابیس فعلی هستید؟");
                    var result = await DialogHost.Show(confirmDialog, "RootDialog");

                    if (result is bool res && res)
                    {
                        using var db = new AppDbContext();
                        
                        // Import VideoFiles
                        var existingVideoIds = db.VideoFiles.Select(v => v.Id).ToList();
                        var newVideos = videoFiles.Where(i => !existingVideoIds.Contains(i.Id)).ToList();
                        db.VideoFiles.AddRange(newVideos);

                        // Import TvSeasons
                        var existingSeasonIds = db.TvSeasons.Select(s => s.Id).ToList();
                        var newSeasons = tvSeasons.Where(i => !existingSeasonIds.Contains(i.Id)).ToList();
                        db.TvSeasons.AddRange(newSeasons);

                        // Import TvEpisodes
                        var existingEpisodeIds = db.TvEpisodes.Select(e => e.Id).ToList();
                        var newEpisodes = tvEpisodes.Where(i => !existingEpisodeIds.Contains(i.Id)).ToList();
                        db.TvEpisodes.AddRange(newEpisodes);
                        
                        await db.SaveChangesAsync();

                        // Restore Settings if present
                        if (importedSettings != null)
                        {
                            SettingsManager.SaveSettings(importedSettings);
                            // The settings will take effect on next restart, or we can apply theme immediately
                            SelectedDataSource = importedSettings.SelectedDataSource ?? "FM_DB";
                            TmdbApiKey = importedSettings.TmdbApiKey;
                            OmdbApiKey = importedSettings.OmdbApiKey;
                            TmdbLanguage = importedSettings.TmdbLanguage ?? "fa-IR";
                            IsDarkTheme = importedSettings.IsDarkTheme;
                            SelectedTheme = importedSettings.Theme ?? "Cyan";
                        }
                        
                        ToastService.Instance.ShowSuccess("اطلاعات پشتیبان با موفقیت بازیابی شد.");
                        
                        // Send message to refresh lists
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
                MovieManagerDesktop.Services.LoggerService.Error("Error importing json content", ex);
                ToastService.Instance.ShowError($"خطا در بازیابی اطلاعات: {ex.Message}");
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
