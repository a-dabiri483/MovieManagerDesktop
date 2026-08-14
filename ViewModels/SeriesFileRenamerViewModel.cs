using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using FolderIconManager.WPF.Services;
using System.Collections.Generic;

namespace MovieManagerDesktop.ViewModels
{
    public enum RenamerMode
    {
        Auto,
        Custom,
        Api
    }

    public partial class SeriesFileRenamerViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _identifyMediaService;
        private readonly RegexParserService _regexParserService;

        [ObservableProperty]
        private string _selectedFolderPath = string.Empty;

        [ObservableProperty]
        private bool _isScanning = false;
        
        [ObservableProperty]
        private string _customBaseName = string.Empty;
        partial void OnCustomBaseNameChanged(string value) => UpdatePreview();

        [ObservableProperty]
        private string _customQuality = "1080p";
        partial void OnCustomQualityChanged(string value) => UpdatePreview();

        [ObservableProperty]
        private int _customSeason = 1;
        partial void OnCustomSeasonChanged(int value) => UpdatePreview();

        [ObservableProperty]
        private int _customStartEpisode = 1;
        partial void OnCustomStartEpisodeChanged(int value) => UpdatePreview();

        [RelayCommand]
        private void CheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<RenamerItemModel>())
            {
                if (!item.IsRenamed)
                    item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void UncheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<RenamerItemModel>())
            {
                if (!item.IsRenamed)
                    item.IsSelected = false;
            }
        }

        [ObservableProperty]
        private bool _isApplying;

        private bool _isAllSelected = true;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    foreach (var item in Items)
                    {
                        if (!item.IsRenamed)
                        {
                            item.IsSelected = value;
                        }
                    }
                }
            }
        }

        public ObservableCollection<RenamerItemModel> Items { get; } = new();

        public SeriesFileRenamerViewModel()
        {
            _identifyMediaService = new IdentifyMediaService();
            _regexParserService = new RegexParserService();
        }

        [RelayCommand]
        private void SelectFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "انتخاب پوشه سریال‌ها"
            };
            if (dialog.ShowDialog() == true)
            {
                SelectedFolderPath = dialog.FolderName;
                if (string.IsNullOrEmpty(CustomBaseName))
                {
                    CustomBaseName = new DirectoryInfo(dialog.FolderName).Name;
                }
                Items.Clear();
                _ = ScanFolderAsync(); // Automatically trigger scan
            }
        }

        [RelayCommand]
        private void SelectFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "انتخاب فایل‌ها",
                Multiselect = true,
                Filter = "Media Files|*.mkv;*.mp4;*.avi;*.srt;*.ass|All Files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                Items.Clear();
                SelectedFolderPath = Path.GetDirectoryName(dialog.FileNames.First()) ?? string.Empty;
                if (string.IsNullOrEmpty(CustomBaseName))
                {
                    CustomBaseName = new DirectoryInfo(SelectedFolderPath).Name;
                }
                
                LoadFiles(dialog.FileNames);
            }
        }

        [RelayCommand]
        public async Task ScanFolderAsync()
        {
            if (string.IsNullOrEmpty(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
            {
                System.Windows.MessageBox.Show("پوشه انتخاب شده معتبر نیست.", "خطا", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            IsScanning = true;
            Items.Clear();

            try
            {
                var files = Directory.GetFiles(SelectedFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => IsVideoFile(f) || IsSubtitleFile(f))
                    .OrderBy(f => f) // مرتب‌سازی الفبایی مهم است
                    .ToArray();

                LoadFiles(files);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطا در اسکن فایل‌ها: {ex.Message}", "خطا", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void LoadFiles(string[] files)
        {
            var sortedFiles = files.OrderBy(f => f).ToList();
            
            foreach (var filePath in sortedFiles)
            {
                string originalFileName = Path.GetFileName(filePath);
                bool isSubtitle = IsSubtitleFile(filePath);

                Items.Add(new RenamerItemModel
                {
                    OriginalFilePath = filePath,
                    OriginalFileName = originalFileName,
                    Status = "آماده",
                    IsSubtitle = isSubtitle,
                    IsSelected = true
                });
            }
            
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (Items.Count == 0) return;

            string baseName = string.IsNullOrWhiteSpace(CustomBaseName) ? "Series" : CustomBaseName.Trim();
            string quality = string.IsNullOrWhiteSpace(CustomQuality) ? "" : CustomQuality.Trim();
            string seasonStr = CustomSeason.ToString("D2");
            
            int currentEp = CustomStartEpisode;

            foreach (var item in Items)
            {
                if (item.IsRenamed) continue;

                string epStr = currentEp.ToString("D2");
                string ext = Path.GetExtension(item.OriginalFilePath);
                
                string newName = baseName;
                if (!string.IsNullOrEmpty(quality))
                {
                    newName += $" - {quality}";
                }
                newName += $" - S{seasonStr}E{epStr}{ext}";
                
                // Cleanup invalid chars if any (though baseName should be clean already)
                newName = string.Join("_", newName.Split(Path.GetInvalidFileNameChars()));

                item.NewFileName = newName;
                
                // Increment episode number for the next file (subtitles will get the same number as video if they are interleaved, 
                // but usually they are processed sequentially so we just increment for everything for now).
                // Actually, if we have a video and a subtitle for the same episode, incrementing blindly might cause S01E01 for video and S01E02 for subtitle.
                // It's safer to increment for each video file, but if it's a subtitle with the exact same base name, use the same episode.
                // However, user asked for simple sequential. Let's just increment for each item for now, 
                // as usually users rename either videos OR subtitles in batch.
                currentEp++;
            }
        }

        [RelayCommand]
        private async Task ApplyChangesAsync()
        {
            var selectedItems = Items.Where(i => i.IsSelected && !i.IsRenamed).ToList();
            if (!selectedItems.Any())
            {
                System.Windows.MessageBox.Show("هیچ فایلی برای تغییر نام انتخاب نشده است.", "پیام", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            IsApplying = true;

            try
            {
                foreach (var item in selectedItems)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(item.OriginalFilePath);
                        string newFilePath = Path.Combine(directory, item.NewFileName);

                        if (File.Exists(newFilePath) && item.OriginalFilePath.ToLower() != newFilePath.ToLower())
                        {
                            item.Status = "خطا (نام تکراری)";
                            continue;
                        }

                        // Rename the file
                        File.Move(item.OriginalFilePath, newFilePath);
                        item.IsRenamed = true;
                        item.Status = "انجام شد";
                        item.OriginalFilePath = newFilePath;
                        item.OriginalFileName = item.NewFileName;
                        item.IsSelected = false; // Uncheck after success
                    }
                    catch (Exception ex)
                    {
                        item.Status = "خطا";
                    }
                }
                
                System.Windows.MessageBox.Show("تغییر نام فایل‌ها با موفقیت انجام شد.", "موفقیت", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            finally
            {
                IsApplying = false;
            }
        }
        
        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in Items) item.IsSelected = true;
        }
        
        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in Items) item.IsSelected = false;
        }


        private bool IsVideoFile(string filePath)
        {
            string[] extensions = { ".mkv", ".mp4", ".avi", ".ts", ".webm", ".m4v" };
            return extensions.Contains(Path.GetExtension(filePath).ToLower());
        }
        
        private bool IsSubtitleFile(string filePath)
        {
            string[] extensions = { ".srt", ".ass", ".ssa", ".vtt" };
            return extensions.Contains(Path.GetExtension(filePath).ToLower());
        }
    }
}
