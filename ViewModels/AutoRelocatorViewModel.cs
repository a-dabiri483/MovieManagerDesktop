using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class BrokenItemModel : ObservableObject
    {
        public int DbId { get; set; }
        public string MediaType { get; set; } = string.Empty; // "Movie" or "Episode"
        public string Title { get; set; } = string.Empty;
        public string OldFilePath { get; set; } = string.Empty;
        public long OldFileSizeBytes { get; set; }
        public int? TmdbId { get; set; }

        [ObservableProperty]
        private string _newFilePath = string.Empty;

        [ObservableProperty]
        private string _status = "گمشده ❌";

        [ObservableProperty]
        private bool _isResolved = false;
    }

    public partial class AutoRelocatorViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly IdentifyMediaService _identifyMediaService;

        public ObservableCollection<BrokenItemModel> BrokenItems { get; } = new();

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private string _targetDirectory = string.Empty;

        [ObservableProperty]
        private int _totalBroken = 0;

        [ObservableProperty]
        private int _totalResolved = 0;

        public AutoRelocatorViewModel()
        {
            _dbContext = new AppDbContext();
            _identifyMediaService = new IdentifyMediaService();
            _ = LoadBrokenLinksAsync();
        }

        [RelayCommand]
        private async Task LoadBrokenLinksAsync()
        {
            IsScanning = true;
            BrokenItems.Clear();

            try
            {
                await Task.Run(() =>
                {
                    // 1. Find all broken files (Movies & Episodes)
                    var allVideoFiles = _dbContext.VideoFiles.ToList();
                    foreach (var file in allVideoFiles)
                    {
                        if (!string.IsNullOrEmpty(file.FilePath) && !File.Exists(file.FilePath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                BrokenItems.Add(new BrokenItemModel
                                {
                                    DbId = file.Id,
                                    MediaType = string.IsNullOrEmpty(file.MediaType) ? "Unknown" : file.MediaType,
                                    Title = string.IsNullOrEmpty(file.FormattedTitle) ? file.FileName : file.FormattedTitle,
                                    OldFilePath = file.FilePath,
                                    OldFileSizeBytes = file.FileSizeBytes,
                                    TmdbId = file.TmdbId
                                });
                            });
                        }
                    }
                });

                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری لینک‌های شکسته: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private void SelectTargetDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "انتخاب درایو یا پوشه جدید"
            };
            if (dialog.ShowDialog() == true)
            {
                TargetDirectory = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task ScanAndRelocateAsync()
        {
            if (string.IsNullOrEmpty(TargetDirectory) || !Directory.Exists(TargetDirectory))
            {
                MessageBox.Show("پوشه مقصد معتبر نیست.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsScanning = true;

            try
            {
                await Task.Run(async () =>
                {
                    // Get all files in target directory safely
                    var allFiles = new System.Collections.Generic.List<string>();
                    SafeGetVideoFiles(TargetDirectory, allFiles);

                    // PHASE 1: Fast Match (By Name or Size)
                    foreach (var item in BrokenItems.Where(i => !i.IsResolved))
                    {
                        string oldFileName = Path.GetFileName(item.OldFilePath);
                        
                        var match = allFiles.FirstOrDefault(f => 
                            Path.GetFileName(f).Equals(oldFileName, StringComparison.OrdinalIgnoreCase) || 
                            (item.OldFileSizeBytes > 0 && new FileInfo(f).Length == item.OldFileSizeBytes));

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "پیدا شد (سریع) ⚡";
                                item.IsResolved = true;
                            });
                            allFiles.Remove(match);
                        }
                    }

                    // PHASE 2: Deep Match (TMDB API) for remaining broken movies
                    var remainingMovies = BrokenItems.Where(i => !i.IsResolved && i.MediaType == "Movie" && i.TmdbId.HasValue).ToList();
                    
                    if (remainingMovies.Count > 0 && allFiles.Count > 0)
                    {
                        foreach (var file in allFiles.ToList())
                        {
                            var tempFile = new VideoFile { FilePath = file, FileName = Path.GetFileName(file) };
                            
                            if (!remainingMovies.Any(m => !m.IsResolved)) break;

                            tempFile = await _identifyMediaService.IdentifyMediaAsync(tempFile);
                            
                            if (tempFile.TmdbId.HasValue)
                            {
                                var matchedBroken = remainingMovies.FirstOrDefault(m => !m.IsResolved && m.TmdbId == tempFile.TmdbId);
                                if (matchedBroken != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        matchedBroken.NewFilePath = file;
                                        matchedBroken.Status = "پیدا شد (عمیق) 🔍";
                                        matchedBroken.IsResolved = true;
                                    });
                                    allFiles.Remove(file);
                                }
                            }
                        }
                    }
                });

                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در اسکن: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task ApplyFixesAsync()
        {
            var resolvedItems = BrokenItems.Where(i => i.IsResolved && !string.IsNullOrEmpty(i.NewFilePath) && !i.Status.Contains("ذخیره شد")).ToList();
            if (resolvedItems.Count == 0) return;

            IsScanning = true;

            try
            {
                int savedCount = 0;
                foreach (var item in resolvedItems)
                {
                    var file = await _dbContext.VideoFiles.FindAsync(item.DbId);
                    if (file != null)
                    {
                        file.FilePath = item.NewFilePath;
                    }

                    item.Status = "ذخیره شد ✅";
                    savedCount++;
                }

                await _dbContext.SaveChangesAsync();
                MessageBox.Show($"{savedCount} مسیر با موفقیت در دیتابیس جایگزین و ذخیره شد!", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                
                await LoadBrokenLinksAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در ذخیره اطلاعات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void UpdateStats()
        {
            TotalBroken = BrokenItems.Count;
            TotalResolved = BrokenItems.Count(i => i.IsResolved);
        }

        private void SafeGetVideoFiles(string path, System.Collections.Generic.List<string> files)
        {
            try
            {
                var directoryFiles = Directory.GetFiles(path).Where(f => IsVideoFile(f));
                files.AddRange(directoryFiles);
                
                var directories = Directory.GetDirectories(path);
                foreach (var d in directories)
                {
                    SafeGetVideoFiles(d, files);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories that we don't have access to (like System Volume Information)
            }
            catch (Exception)
            {
                // Ignore other read errors
            }
        }

        private bool IsVideoFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".wmv" || ext == ".mov" || ext == ".flv" || ext == ".webm" || ext == ".m4v";
        }
    }
}
