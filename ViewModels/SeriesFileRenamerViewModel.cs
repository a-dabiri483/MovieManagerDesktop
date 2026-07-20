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
        private RenamerMode _selectedMode = RenamerMode.Auto;
        
        public bool IsAutoMode
        {
            get => SelectedMode == RenamerMode.Auto;
            set { if (value) { SelectedMode = RenamerMode.Auto; OnPropertyChanged(nameof(IsAutoMode)); OnPropertyChanged(nameof(IsCustomMode)); OnPropertyChanged(nameof(IsApiMode)); } }
        }
        
        public bool IsCustomMode
        {
            get => SelectedMode == RenamerMode.Custom;
            set { if (value) { SelectedMode = RenamerMode.Custom; OnPropertyChanged(nameof(IsAutoMode)); OnPropertyChanged(nameof(IsCustomMode)); OnPropertyChanged(nameof(IsApiMode)); } }
        }
        
        public bool IsApiMode
        {
            get => SelectedMode == RenamerMode.Api;
            set { if (value) { SelectedMode = RenamerMode.Api; OnPropertyChanged(nameof(IsAutoMode)); OnPropertyChanged(nameof(IsCustomMode)); OnPropertyChanged(nameof(IsApiMode)); } }
        }
        
        [ObservableProperty]
        private string _customBaseName = string.Empty;

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
                if (SelectedMode == RenamerMode.Auto && string.IsNullOrEmpty(CustomBaseName))
                {
                    CustomBaseName = new DirectoryInfo(dialog.FolderName).Name;
                }
                Items.Clear();
            }
        }
        
        [RelayCommand]
        private void SearchApi()
        {
            var searchDialogViewModel = new ApiSearchDialogViewModel(string.IsNullOrEmpty(CustomBaseName) ? (Path.GetFileName(SelectedFolderPath) ?? "") : CustomBaseName);
            var searchDialog = new MovieManagerDesktop.Views.Dialogs.ApiSearchDialog { DataContext = searchDialogViewModel };
            
            searchDialogViewModel.CloseAction = () => searchDialog.Close();
            searchDialogViewModel.SelectAction = async (result) => 
            {
                CustomBaseName = result.Title;
                // Remove invalid path chars
                CustomBaseName = string.Join("_", CustomBaseName.Split(Path.GetInvalidFileNameChars()));
                
                // Trigger scan if folder is already selected
                if (!string.IsNullOrEmpty(SelectedFolderPath))
                {
                    await ScanFolderAsync();
                }
            };
            
            searchDialog.Owner = System.Windows.Application.Current.MainWindow;
            searchDialog.ShowDialog();
        }

        [RelayCommand]
        private async Task ScanFolderAsync()
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
                var files = Directory.GetFiles(SelectedFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsVideoFile(f) || IsSubtitleFile(f))
                    .ToList();

                var tmdbCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var filePath in files)
                {
                    string originalFileName = Path.GetFileName(filePath);
                    bool isSubtitle = IsSubtitleFile(filePath);
                    
                    var parsedInfo = _regexParserService.ParseVideoFileName(originalFileName);
                    
                    string officialSeriesName = parsedInfo.CleanName; // Default

                    if (SelectedMode == RenamerMode.Custom || SelectedMode == RenamerMode.Api)
                    {
                        if (!string.IsNullOrWhiteSpace(CustomBaseName))
                        {
                            officialSeriesName = CustomBaseName;
                        }
                    }
                    else // Auto mode
                    {
                        // Use folder name as the fallback for Auto instead of fetching TMDB for each file silently
                        officialSeriesName = new DirectoryInfo(SelectedFolderPath).Name;
                    }

                    // Build new name: [Official Name] - S[Season]E[Episode] - [Quality] [Source] [[Extras]]
                    string newName = officialSeriesName;
                    
                    if (!string.IsNullOrEmpty(parsedInfo.SeasonEpisode))
                        newName += $" - {parsedInfo.SeasonEpisode}";
                        
                    if (!string.IsNullOrEmpty(parsedInfo.Quality))
                        newName += $" - {parsedInfo.Quality}";
                        
                    if (!string.IsNullOrEmpty(parsedInfo.Source))
                        newName += $" {parsedInfo.Source}";
                        
                    if (!string.IsNullOrEmpty(parsedInfo.Extras))
                        newName += $" [{parsedInfo.Extras}]";

                    // Cleanup multiple spaces
                    newName = string.Join(" ", newName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    
                    // Add Extension
                    newName += Path.GetExtension(filePath);

                    Items.Add(new RenamerItemModel
                    {
                        OriginalFilePath = filePath,
                        OriginalFileName = originalFileName,
                        NewFileName = newName,
                        Status = "آماده",
                        IsSubtitle = isSubtitle,
                        IsSelected = true
                    });
                }
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
