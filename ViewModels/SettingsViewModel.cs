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
using System.Threading.Tasks;

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

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                SetProperty(ref _selectedTheme, value);
                ApplyTheme(value, IsDarkTheme);
                AutoSaveTheme();
                OnPropertyChanged(nameof(IsDeepPurple));
                OnPropertyChanged(nameof(IsMidnightBlue));
                OnPropertyChanged(nameof(IsOLEDBlack));
            }
        }
        private string _selectedTheme = "DeepPurple";

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

        public bool IsDeepPurple
        {
            get => SelectedTheme == "DeepPurple";
            set { if (value) SelectedTheme = "DeepPurple"; }
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

        public SettingsViewModel()
        {
            var settings = SettingsManager.LoadSettings();
            SelectedDataSource = settings.SelectedDataSource ?? "FM_DB";
            TmdbApiKey = settings.TmdbApiKey;
            OmdbApiKey = settings.OmdbApiKey;
            TmdbLanguage = settings.TmdbLanguage ?? "fa-IR";
            _isDarkTheme = settings.IsDarkTheme;
            SelectedTheme = settings.Theme ?? "DeepPurple"; // This calls ApplyTheme
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
            
            if (themeName == "DeepPurple")
            {
                primaryColor = System.Windows.Media.Color.FromRgb(103, 58, 183);
                secondaryColor = System.Windows.Media.Color.FromRgb(156, 39, 176);
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
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(dialog.FileName, json);
                    ToastService.Instance.ShowSuccess("نسخه پشتیبان با موفقیت صادر شد.");
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error exporting json", ex);
                ToastService.Instance.ShowError($"خطا در خروجی گرفتن: {ex.Message}");
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
                    var json = await File.ReadAllTextAsync(dialog.FileName);
                    var importedData = JsonSerializer.Deserialize<System.Collections.Generic.List<Models.VideoFile>>(json);
                    
                    if (importedData != null && importedData.Any())
                    {
                        var confirmDialog = new ConfirmDialog($"تعداد {importedData.Count} فیلم در این فایل وجود دارد. آیا مایل به ادغام آن‌ها با دیتابیس فعلی هستید؟");
                        var result = await DialogHost.Show(confirmDialog, "RootDialog");

                        if (result is bool res && res)
                        {
                            using var db = new AppDbContext();
                            var existingIds = db.VideoFiles.Select(v => v.Id).ToList();
                            var newItems = importedData.Where(i => !existingIds.Contains(i.Id)).ToList();
                            
                            db.VideoFiles.AddRange(newItems);
                            await db.SaveChangesAsync();
                            
                            ToastService.Instance.ShowSuccess($"{newItems.Count} فیلم با موفقیت به دیتابیس اضافه شد.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error importing json", ex);
                ToastService.Instance.ShowError($"خطا در بازیابی پشتیبان: {ex.Message}");
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
