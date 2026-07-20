using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public partial class FolderIconToolViewModel : ObservableObject
    {
        private readonly FolderScannerService _folderScanner;
        private readonly IdentifyMediaService _identifyService;

        // ═══════════════════════════════════════════════════════════════
        // حالت دستی (Manual)
        // ═══════════════════════════════════════════════════════════════

        [ObservableProperty]
        private string _manualFolderPath = string.Empty;

        [ObservableProperty]
        private string _manualStatusMessage = "یک پوشه انتخاب کنید و روی «اسکن» کلیک کنید.";

        [ObservableProperty]
        private ObservableCollection<FolderInfo> _foldersWithIcons = new();

        [ObservableProperty]
        private ObservableCollection<FolderInfo> _foldersWithoutIcons = new();

        [ObservableProperty]
        private FolderInfo? _selectedFolderWithoutIcon;

        [ObservableProperty]
        private FolderInfo? _selectedFolderWithIcon;

        [ObservableProperty]
        private bool _isManualScanning;

        // ═══════════════════════════════════════════════════════════════
        // حالت اتوماتیک (Auto API)
        // ═══════════════════════════════════════════════════════════════

        [ObservableProperty]
        private string _autoFolderPath = string.Empty;

        public ObservableCollection<AutoIconItem> AutoFoldersWithoutIcon { get; } = new();
        public ObservableCollection<AutoIconItem> AutoFoldersWithIcon { get; } = new();

        private bool _isAllSelectedAuto = true;
        public bool IsAllSelectedAuto
        {
            get => _isAllSelectedAuto;
            set
            {
                if (SetProperty(ref _isAllSelectedAuto, value))
                {
                    foreach (var item in AutoFoldersWithoutIcon)
                    {
                        item.IsSelected = value;
                    }
                }
            }
        }

        [RelayCommand]
        private void CheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<AutoIconItem>())
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void UncheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<AutoIconItem>())
            {
                item.IsSelected = false;
            }
        }

        [ObservableProperty]
        private string _autoStatusMessage = "یک پوشه انتخاب کنید و روی «اسکن» کلیک کنید.";

        [ObservableProperty]
        private bool _isAutoScanning;

        [ObservableProperty]
        private bool _isAutoRunning;

        public bool IsAutoBusy => IsAutoScanning || IsAutoRunning;

        partial void OnIsAutoScanningChanged(bool value) => OnPropertyChanged(nameof(IsAutoBusy));
        partial void OnIsAutoRunningChanged(bool value) => OnPropertyChanged(nameof(IsAutoBusy));

        public ObservableCollection<string> Logs { get; } = new();

        public FolderIconToolViewModel()
        {
            _folderScanner = new FolderScannerService();
            _identifyService = new IdentifyMediaService();
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }

        // ==========================================
        // متدهای حالت دستی
        // ==========================================

        [RelayCommand]
        private void BrowseManualFolder()
        {
            var dialog = new OpenFolderDialog { Title = "انتخاب درایو/پوشه (حالت دستی)" };
            if (dialog.ShowDialog() == true)
            {
                ManualFolderPath = dialog.FolderName;
                FoldersWithIcons.Clear();
                FoldersWithoutIcons.Clear();
                ManualStatusMessage = $"📂 انتخاب شد: {ManualFolderPath}";
            }
        }

        [RelayCommand]
        private async Task ScanManualFoldersAsync()
        {
            if (string.IsNullOrWhiteSpace(ManualFolderPath) || !Directory.Exists(ManualFolderPath))
            {
                ManualStatusMessage = "❌ مسیر نامعتبر است.";
                return;
            }

            IsManualScanning = true;
            ManualStatusMessage = "🔍 در حال اسکن لایه اول...";
            FoldersWithIcons.Clear();
            FoldersWithoutIcons.Clear();

            try
            {
                var (withIcons, withoutIcons) = await _folderScanner.ScanFoldersAsync(ManualFolderPath);

                foreach (var folder in withIcons) FoldersWithIcons.Add(folder);
                foreach (var folder in withoutIcons) FoldersWithoutIcons.Add(folder);

                ManualStatusMessage = $"✅ {FoldersWithIcons.Count} با آیکون، {FoldersWithoutIcons.Count} بدون آیکون";
            }
            catch (Exception ex)
            {
                ManualStatusMessage = $"❌ خطا: {ex.Message}";
            }
            finally
            {
                IsManualScanning = false;
            }
        }

        [RelayCommand]
        private void SearchGoogle()
        {
            if (SelectedFolderWithoutIcon == null)
            {
                ManualStatusMessage = "⚠️ لطفاً یک پوشه بدون آیکون انتخاب کنید";
                return;
            }
            string query = $"{SelectedFolderWithoutIcon.Name} folder icon";
            string url = $"https://images.google.com/search?q={Uri.EscapeDataString(query)}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ManualStatusMessage = $"🌐 جستجو در گوگل برای: {query}";
        }

        [RelayCommand]
        private async Task ApplyCustomIconAsync(System.Collections.IList? selectedItems)
        {
            var itemsToProcess = selectedItems?.Cast<FolderInfo>().ToList() ?? new List<FolderInfo>();
            if (!itemsToProcess.Any() && SelectedFolderWithoutIcon != null)
            {
                itemsToProcess.Add(SelectedFolderWithoutIcon);
            }

            if (!itemsToProcess.Any())
            {
                ManualStatusMessage = "⚠️ لطفاً یک یا چند پوشه بدون آیکون انتخاب کنید";
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "انتخاب عکس برای آیکون",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.ico",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                ManualStatusMessage = $"⏳ در حال ساخت و اعمال آیکون روی {itemsToProcess.Count} پوشه...";
                try
                {
                    int successCount = 0;
                    foreach (var folder in itemsToProcess.ToList())
                    {
                        string iconFolderPath = Path.Combine(folder.Path, "ICON");
                        if (!Directory.Exists(iconFolderPath)) Directory.CreateDirectory(iconFolderPath);

                        string finalIconPath = Path.Combine(iconFolderPath, "icon.ico");
                        
                        if (dialog.FileName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                        {
                            if(File.Exists(finalIconPath)) File.Delete(finalIconPath);
                            File.Copy(dialog.FileName, finalIconPath, true);
                        }
                        else
                        {
                            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "folder_template.png");
                            if (File.Exists(templatePath))
                            {
                                IconConverterService.CreateTemplateIcon(dialog.FileName, templatePath, finalIconPath, null);
                            }
                            else
                            {
                                IconConverterService.ConvertToIcon(dialog.FileName, finalIconPath);
                            }
                        }

                        var result = await _folderScanner.ApplyIconToFolderAsync(folder.Path, finalIconPath);
                        if (result.Success)
                        {
                            folder.HasIcon = true;
                            folder.IconPath = finalIconPath;
                            FoldersWithIcons.Add(folder);
                            FoldersWithoutIcons.Remove(folder);
                            successCount++;
                        }
                        else
                        {
                            throw new Exception(result.ErrorMessage);
                        }
                    }
                    ManualStatusMessage = $"✅ آیکون با موفقیت روی {successCount} پوشه اعمال شد";
                }
                catch (Exception ex)
                {
                    ManualStatusMessage = $"❌ خطا: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task RemoveManualIconAsync(System.Collections.IList? selectedItems)
        {
            var itemsToProcess = selectedItems?.Cast<FolderInfo>().ToList() ?? new List<FolderInfo>();
            if (!itemsToProcess.Any() && SelectedFolderWithIcon != null)
            {
                itemsToProcess.Add(SelectedFolderWithIcon);
            }

            if (!itemsToProcess.Any()) return;

            ManualStatusMessage = $"⏳ در حال حذف آیکون از {itemsToProcess.Count} پوشه...";
            try
            {
                int successCount = 0;
                foreach (var folder in itemsToProcess.ToList())
                {
                    bool success = await _folderScanner.RemoveIconFromFolderAsync(folder.Path);
                    if (success)
                    {
                        folder.HasIcon = false;
                        folder.IconPath = string.Empty;
                        FoldersWithoutIcons.Add(folder);
                        FoldersWithIcons.Remove(folder);
                        successCount++;
                    }
                }
                ManualStatusMessage = $"✅ آیکون از {successCount} پوشه حذف شد";
            }
            catch (Exception ex)
            {
                ManualStatusMessage = $"❌ خطا: {ex.Message}";
            }
        }

        // ==========================================
        // متدهای حالت اتوماتیک
        // ==========================================

        [RelayCommand]
        private void BrowseAutoFolder()
        {
            var dialog = new OpenFolderDialog { Title = "انتخاب درایو/پوشه (حالت اتوماتیک)" };
            if (dialog.ShowDialog() == true)
            {
                AutoFolderPath = dialog.FolderName;
                AutoFoldersWithIcon.Clear();
                AutoFoldersWithoutIcon.Clear();
                AutoStatusMessage = $"📂 انتخاب شد: {AutoFolderPath}";
            }
        }

        [RelayCommand]
        private async Task ScanAutoFoldersAsync()
        {
            if (string.IsNullOrWhiteSpace(AutoFolderPath) || !Directory.Exists(AutoFolderPath))
            {
                AutoStatusMessage = "❌ مسیر نامعتبر است.";
                return;
            }

            IsAutoScanning = true;
            AutoStatusMessage = "🔍 در حال اسکن...";
            AutoFoldersWithoutIcon.Clear();
            AutoFoldersWithIcon.Clear();

            try
            {
                var (withIcons, withoutIcons) = await _folderScanner.ScanFoldersAsync(AutoFolderPath);

                foreach (var folder in withoutIcons)
                {
                    AutoFoldersWithoutIcon.Add(new AutoIconItem
                    {
                        FolderPath = folder.Path,
                        FolderName = folder.Name,
                        ExtractedName = ExtractName(folder.Name),
                        HasExistingIcon = false
                    });
                }

                foreach (var folder in withIcons)
                {
                    AutoFoldersWithIcon.Add(new AutoIconItem
                    {
                        FolderPath = folder.Path,
                        FolderName = folder.Name,
                        ExtractedName = ExtractName(folder.Name),
                        HasExistingIcon = true,
                        IconPath = folder.IconPath
                    });
                }

                AutoStatusMessage = $"✅ {withoutIcons.Count} بدون آیکون و {withIcons.Count} دارای آیکون.";
            }
            catch (Exception ex)
            {
                AutoStatusMessage = $"❌ خطا: {ex.Message}";
            }
            finally
            {
                IsAutoScanning = false;
            }
        }

        private string ExtractName(string folderName)
        {
            string name = folderName.Replace(".", " ");
            var match = System.Text.RegularExpressions.Regex.Match(name, @"(.*?)(?:s\d{2}|season|\d{4}|1080p|720p|480p|complete|bluray)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Trim();
            }
            return name.Trim();
        }

        [RelayCommand]
        private async Task SearchAutoIconsAsync(System.Collections.IList? selectedItems)
        {
            var itemsToSearch = selectedItems?.Cast<AutoIconItem>().ToList();
            if (itemsToSearch == null || !itemsToSearch.Any())
            {
                itemsToSearch = AutoFoldersWithoutIcon.Where(i => i.IsSelected).ToList();
            }

            if (!itemsToSearch.Any())
            {
                AutoStatusMessage = "⚠️ هیچ موردی برای جستجو انتخاب نشده است.";
                return;
            }

            IsAutoRunning = true;
            AutoStatusMessage = $"🔍 در حال جستجوی {itemsToSearch.Count} مورد در سرویس شناسایی...";

            int found = 0;
            int notFound = 0;

            foreach (var item in itemsToSearch)
            {
                item.Status = AutoIconStatus.Searching;
                try
                {
                    var results = await _identifyService.SearchMediaAsync(item.ExtractedName);
                    var bestResult = results?.FirstOrDefault();

                    if (bestResult != null && !string.IsNullOrWhiteSpace(bestResult.PosterUrl))
                    {
                        item.MediaTitle = bestResult.Title;
                        
                        // If it's a TMDB URL with w92, replace it to get high quality image for the folder icon
                        string highResUrl = bestResult.PosterUrl;
                        if (highResUrl.Contains("w92"))
                        {
                            highResUrl = highResUrl.Replace("w92", "w500");
                        }
                        
                        item.PosterUrl = highResUrl;
                        
                        Application.Current.Dispatcher.Invoke(() => {
                            try {
                                item.PosterPreview = new System.Windows.Media.Imaging.BitmapImage(new Uri(bestResult.PosterUrl));
                            } catch { }
                        });

                        item.Status = AutoIconStatus.Found;
                        found++;
                    }
                    else
                    {
                        item.Status = AutoIconStatus.NotFound;
                        item.ErrorMessage = "موردی پیدا نشد";
                        notFound++;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = AutoIconStatus.Error;
                    item.ErrorMessage = ex.Message;
                    notFound++;
                }
            }

            AutoStatusMessage = $"جستجو پایان یافت: {found} پیدا شد | {notFound} پیدا نشد.";
            IsAutoRunning = false;
        }

        [RelayCommand]
        private async Task ApplyAutoIconsAsync(System.Collections.IList? selectedItems)
        {
            var itemsToApply = selectedItems?.Cast<AutoIconItem>().Where(i => i.Status == AutoIconStatus.Found).ToList();
            if (itemsToApply == null || !itemsToApply.Any())
            {
                itemsToApply = AutoFoldersWithoutIcon.Where(i => i.IsSelected && i.Status == AutoIconStatus.Found).ToList();
            }

            if (!itemsToApply.Any())
            {
                AutoStatusMessage = "⚠️ هیچ پوشه‌ای با پوستر یافت شده برای اعمال انتخاب نشده است.";
                return;
            }

            IsAutoRunning = true;
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "folder_template.png");
            // If template doesn't exist, we just do direct conversion
            bool useTemplate = File.Exists(templatePath);

            int successCount = 0;
            int errorCount = 0;

            foreach (var item in itemsToApply)
            {
                item.Status = AutoIconStatus.Downloading;
                AutoStatusMessage = $"در حال پردازش: {item.MediaTitle}...";

                try
                {
                    string? downloadedImagePath = await _identifyService.DownloadImageAsync(item.PosterUrl, item.MediaTitle);
                    if (string.IsNullOrWhiteSpace(downloadedImagePath) || !File.Exists(downloadedImagePath))
                    {
                        throw new Exception("دانلود عکس با شکست مواجه شد");
                    }
                    
                    string iconFolderPath = Path.Combine(item.FolderPath, "ICON");
                    if (!Directory.Exists(iconFolderPath)) Directory.CreateDirectory(iconFolderPath);

                    string finalIconPath = Path.Combine(iconFolderPath, "icon.ico");

                    if (useTemplate)
                    {
                        IconConverterService.CreateTemplateIcon(downloadedImagePath, templatePath, finalIconPath, item.Rating);
                    }
                    else
                    {
                        IconConverterService.ConvertToIcon(downloadedImagePath, finalIconPath);
                    }

                    var result = await _folderScanner.ApplyIconToFolderAsync(item.FolderPath, finalIconPath);

                    if (result.Success)
                    {
                        item.Status = AutoIconStatus.Applied;
                        
                        Application.Current.Dispatcher.Invoke(() => {
                            item.HasExistingIcon = true;
                            item.IconPath = finalIconPath;
                            AutoFoldersWithoutIcon.Remove(item);
                            AutoFoldersWithIcon.Add(item);
                        });
                        
                        successCount++;
                    }
                    else
                    {
                        item.Status = AutoIconStatus.Error;
                        item.ErrorMessage = $"خطا: {result.ErrorMessage}";
                        errorCount++;
                    }

                    // Delete the downloaded image after making icon
                    try { File.Delete(downloadedImagePath); } catch { }
                }
                catch (Exception ex)
                {
                    item.Status = AutoIconStatus.Error;
                    item.ErrorMessage = ex.Message;
                    errorCount++;
                }
            }

            AutoStatusMessage = $"عملیات پایان یافت: {successCount} موفق | {errorCount} خطا.";
            IsAutoRunning = false;
        }

        [RelayCommand]
        private void SelectAllAutoItems()
        {
            foreach (var item in AutoFoldersWithoutIcon) item.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAllAutoItems()
        {
            foreach (var item in AutoFoldersWithoutIcon) item.IsSelected = false;
        }
    }
}
