using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class BrokenItemModel : ObservableObject
    {
        public int DbId { get; set; }
        public string MediaType { get; set; } = string.Empty; // "Movie" or "Series"
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OldFilePath { get; set; } = string.Empty;
        public long OldFileSizeBytes { get; set; }
        public string FileSizeFormatted => FormatFileSize(OldFileSizeBytes);
        public int? TmdbId { get; set; }
        public string? PosterUrl { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string SeasonEpisodeInfo => (Season.HasValue && Episode.HasValue) ? $"فصل {Season} • قسمت {Episode}" : "";

        [ObservableProperty]
        private string _newFilePath = string.Empty;

        [ObservableProperty]
        private string _status = "گمشده ❌";

        [ObservableProperty]
        private bool _isResolved = false;

        [ObservableProperty]
        private bool _isSelected = false;

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "حجم نامشخص";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public partial class AutoRelocatorViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly IdentifyMediaService _identifyMediaService;

        public ObservableCollection<BrokenItemModel> BrokenItems { get; } = new();
        public ObservableCollection<BrokenItemModel> TrashedItems { get; } = new();

        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: Broken, 1: Trash

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private string _scanProgressText = string.Empty;

        [ObservableProperty]
        private string _targetDirectory = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _totalBroken = 0;

        [ObservableProperty]
        private int _totalResolved = 0;

        [ObservableProperty]
        private int _totalTrashed = 0;

        [ObservableProperty]
        private bool _isAllSelected = false;

        public AutoRelocatorViewModel()
        {
            _dbContext = new AppDbContext();
            _identifyMediaService = new IdentifyMediaService();
            _ = LoadBrokenLinksAsync();
        }

        partial void OnSearchQueryChanged(string value)
        {
            // Trigger refresh if needed
        }

        partial void OnIsAllSelectedChanged(bool value)
        {
            var targetCollection = SelectedTabIndex == 0 ? BrokenItems : TrashedItems;
            foreach (var item in targetCollection)
            {
                item.IsSelected = value;
            }
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            IsAllSelected = false;
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        [RelayCommand]
        public async Task LoadBrokenLinksAsync()
        {
            IsScanning = true;
            ScanProgressText = "در حال بررسی دیتابیس و فایل‌های سیستم...";
            BrokenItems.Clear();
            TrashedItems.Clear();

            try
            {
                var settings = SettingsManager.LoadSettings();
                var trashedIds = new HashSet<int>(settings.TrashedBrokenDbIds ?? new List<int>());

                await Task.Run(() =>
                {
                    var allVideoFiles = _dbContext.VideoFiles.ToList();
                    var brokenList = new List<BrokenItemModel>();
                    var trashedList = new List<BrokenItemModel>();

                    foreach (var file in allVideoFiles)
                    {
                        if (!string.IsNullOrEmpty(file.FilePath) && !File.Exists(file.FilePath))
                        {
                            var item = new BrokenItemModel
                            {
                                DbId = file.Id,
                                MediaType = string.IsNullOrEmpty(file.MediaType) ? "Movie" : file.MediaType,
                                Title = string.IsNullOrEmpty(file.FormattedTitle) ? file.FileName : file.FormattedTitle,
                                FileName = string.IsNullOrEmpty(file.FileName) ? Path.GetFileName(file.FilePath) : file.FileName,
                                OldFilePath = file.FilePath,
                                OldFileSizeBytes = file.FileSizeBytes,
                                TmdbId = file.TmdbId,
                                PosterUrl = file.PosterUrl,
                                Season = file.Season,
                                Episode = file.Episode
                            };

                            if (trashedIds.Contains(file.Id))
                            {
                                item.Status = "سطل زباله 🗑️";
                                trashedList.Add(item);
                            }
                            else
                            {
                                brokenList.Add(item);
                            }
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in brokenList) BrokenItems.Add(item);
                        foreach (var item in trashedList) TrashedItems.Add(item);
                    });
                });

                UpdateStats();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error loading broken links", ex);
                ToastService.Instance.ShowError($"خطا در بارگذاری فایل‌های گمشده: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                ScanProgressText = string.Empty;
            }
        }

        [RelayCommand]
        private void SelectTargetDirectory()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "انتخاب پوشه یا درایو مقصد"
            };
            if (dialog.ShowDialog() == true)
            {
                TargetDirectory = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void ManualRelocate(BrokenItemModel item)
        {
            if (item == null) return;

            var dialog = new OpenFileDialog
            {
                Title = $"انتخاب فایل جایگزین برای {item.Title}",
                Filter = "فایل‌های ویدیویی|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.flv;*.webm;*.m4v;*.ts|همه فایل‌ها|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedPath = dialog.FileName;
                if (File.Exists(selectedPath))
                {
                    item.NewFilePath = selectedPath;
                    item.IsResolved = true;
                    item.Status = "انتخاب دستی 📁";
                    UpdateStats();
                    ToastService.Instance.ShowSuccess($"مسیر جدید برای {item.Title} انتخاب شد.");
                }
            }
        }

        [RelayCommand]
        private async Task ScanAndRelocateAsync()
        {
            if (string.IsNullOrEmpty(TargetDirectory) || !Directory.Exists(TargetDirectory))
            {
                ToastService.Instance.ShowWarning("لطفاً ابتدا پوشه یا درایو مقصد را انتخاب کنید.");
                return;
            }

            var itemsToScan = BrokenItems.Where(i => !i.IsResolved).ToList();
            if (itemsToScan.Count == 0)
            {
                ToastService.Instance.ShowInfo("تمام فایل‌های موجود در لیست ترمیم شده‌اند.");
                return;
            }

            IsScanning = true;
            ScanProgressText = "در حال اسکن فایل‌های پوشه مقصد...";

            try
            {
                await Task.Run(async () =>
                {
                    var allCandidateFiles = new List<string>();
                    SafeGetVideoFiles(TargetDirectory, allCandidateFiles);

                    if (allCandidateFiles.Count == 0)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ToastService.Instance.ShowWarning("هیچ فایل ویدیویی در پوشه مقصد یافت نشد.");
                        });
                        return;
                    }

                    // ── Phase 1: Exact File Name Match ──
                    ScanProgressText = "مرحله ۱: تطبیق دقیق نام فایل‌ها...";
                    foreach (var item in itemsToScan.Where(i => !i.IsResolved))
                    {
                        string oldFileName = Path.GetFileName(item.OldFilePath);
                        var match = allCandidateFiles.FirstOrDefault(f => 
                            Path.GetFileName(f).Equals(oldFileName, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "تطبیق نام ⚡";
                                item.IsResolved = true;
                            });
                            allCandidateFiles.Remove(match);
                        }
                    }

                    // ── Phase 2: Exact Normalized Title Match (Numbering & Release Tag Agnostic) ──
                    ScanProgressText = "مرحله ۲: تطبیق هوشمند عناوین فیلم‌ها و سریال‌ها...";
                    foreach (var item in itemsToScan.Where(i => !i.IsResolved))
                    {
                        string normTitle = NormalizeTitleForMatching(item.Title);
                        string normOldName = NormalizeTitleForMatching(item.FileName);

                        string? match = allCandidateFiles.FirstOrDefault(f =>
                        {
                            string normCand = NormalizeTitleForMatching(f);
                            return (!string.IsNullOrEmpty(normTitle) && normCand == normTitle) ||
                                   (!string.IsNullOrEmpty(normOldName) && normCand == normOldName);
                        });

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "تطبیق هوشمند 🧠";
                                item.IsResolved = true;
                            });
                            allCandidateFiles.Remove(match);
                        }
                    }

                    // ── Phase 3: Series Season/Episode + Show Title Match ──
                    ScanProgressText = "مرحله ۳: تطبیق فصل و قسمت سریال‌ها...";
                    foreach (var item in itemsToScan.Where(i => !i.IsResolved && i.Season.HasValue && i.Episode.HasValue))
                    {
                        int s = item.Season.Value;
                        int e = item.Episode.Value;
                        string normSeries = NormalizeTitleForMatching(item.Title);

                        var match = allCandidateFiles.FirstOrDefault(f =>
                        {
                            string fName = Path.GetFileNameWithoutExtension(f);
                            var (foundS, foundE) = ExtractSeasonEpisode(fName);
                            if (foundS == s && foundE == e)
                            {
                                string normF = NormalizeTitleForMatching(f);
                                if (normF.Contains(normSeries, StringComparison.OrdinalIgnoreCase) ||
                                    normSeries.Contains(normF, StringComparison.OrdinalIgnoreCase) ||
                                    IsTitleFuzzyMatch(normSeries, normF))
                                {
                                    return true;
                                }
                            }
                            return false;
                        });

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "تطبیق قسمت 📺";
                                item.IsResolved = true;
                            });
                            allCandidateFiles.Remove(match);
                        }
                    }

                    // ── Phase 4: Token / Fuzzy Similarity Match (e.g. Subtitles & Sequels) ──
                    ScanProgressText = "مرحله ۴: تطبیق تشابه واژگان و دنباله‌ها...";
                    foreach (var item in itemsToScan.Where(i => !i.IsResolved))
                    {
                        string normTitle = NormalizeTitleForMatching(item.Title);
                        string normOldName = NormalizeTitleForMatching(item.FileName);

                        var match = allCandidateFiles.FirstOrDefault(f =>
                        {
                            string normCand = NormalizeTitleForMatching(f);
                            return IsTitleFuzzyMatch(normTitle, normCand) || IsTitleFuzzyMatch(normOldName, normCand);
                        });

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "تطبیق فازی 🎯";
                                item.IsResolved = true;
                            });
                            allCandidateFiles.Remove(match);
                        }
                    }

                    // ── Phase 5: Exact File Size Match (for exact files moved/renamed) ──
                    ScanProgressText = "مرحله ۵: تطبیق حجم بایت فایل‌ها...";
                    foreach (var item in itemsToScan.Where(i => !i.IsResolved && i.OldFileSizeBytes > 0))
                    {
                        var match = allCandidateFiles.FirstOrDefault(f =>
                        {
                            try { return new FileInfo(f).Length == item.OldFileSizeBytes; }
                            catch { return false; }
                        });

                        if (match != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.NewFilePath = match;
                                item.Status = "تطبیق حجم 📏";
                                item.IsResolved = true;
                            });
                            allCandidateFiles.Remove(match);
                        }
                    }

                    // ── Phase 6: TMDB Deep Match ──
                    var remainingWithTmdb = itemsToScan.Where(i => !i.IsResolved && i.TmdbId.HasValue).ToList();
                    if (remainingWithTmdb.Count > 0 && allCandidateFiles.Count > 0)
                    {
                        ScanProgressText = "مرحله ۶: شناسایی عمیق با پایگاه داده TMDb...";
                        foreach (var file in allCandidateFiles.ToList())
                        {
                            if (!remainingWithTmdb.Any(m => !m.IsResolved)) break;

                            var tempFile = new VideoFile { FilePath = file, FileName = Path.GetFileName(file) };
                            tempFile = await _identifyMediaService.IdentifyMediaAsync(tempFile);

                            if (tempFile.TmdbId.HasValue)
                            {
                                var matched = remainingWithTmdb.FirstOrDefault(m => !m.IsResolved && m.TmdbId == tempFile.TmdbId &&
                                    (m.MediaType != "Series" || (m.Season == tempFile.Season && m.Episode == tempFile.Episode)));

                                if (matched != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        matched.NewFilePath = file;
                                        matched.Status = "شناسایی TMDb 🔍";
                                        matched.IsResolved = true;
                                    });
                                    allCandidateFiles.Remove(file);
                                }
                            }
                        }
                    }
                });

                UpdateStats();
                int newlyResolved = BrokenItems.Count(i => i.IsResolved);
                ToastService.Instance.ShowSuccess($"اسکن به پایان رسید. {newlyResolved} فایل آماده ذخیره هستند.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error scanning files", ex);
                ToastService.Instance.ShowError($"خطا در اسکن و تطبیق: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                ScanProgressText = string.Empty;
            }
        }

        [RelayCommand]
        public async Task ApplyFixesAsync()
        {
            var resolvedItems = BrokenItems.Where(i => i.IsResolved && !string.IsNullOrEmpty(i.NewFilePath)).ToList();
            if (resolvedItems.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ فایل ترمیم‌شده‌ای برای ذخیره وجود ندارد.");
                return;
            }

            IsScanning = true;
            ScanProgressText = "در حال ذخیره مسیرهای جدید در دیتابیس...";

            try
            {
                int savedCount = 0;
                foreach (var item in resolvedItems)
                {
                    var file = await _dbContext.VideoFiles.FindAsync(item.DbId);
                    if (file != null && File.Exists(item.NewFilePath))
                    {
                        file.FilePath = item.NewFilePath;
                        file.FileName = Path.GetFileName(item.NewFilePath);
                        try
                        {
                            file.FileSizeBytes = new FileInfo(item.NewFilePath).Length;
                        }
                        catch { }
                        savedCount++;
                    }
                }

                await _dbContext.SaveChangesAsync();
                ToastService.Instance.ShowSuccess($"{savedCount} مسیر با موفقیت در دیتابیس بروزرسانی شد!");

                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                await LoadBrokenLinksAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error saving relocated paths", ex);
                ToastService.Instance.ShowError($"خطا در ذخیره اطلاعات: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                ScanProgressText = string.Empty;
            }
        }

        [RelayCommand]
        private void MoveToTrash(BrokenItemModel item)
        {
            if (item == null) return;

            var settings = SettingsManager.LoadSettings();
            settings.TrashedBrokenDbIds.Add(item.DbId);
            SettingsManager.SaveSettings(settings);

            BrokenItems.Remove(item);
            item.Status = "سطل زباله 🗑️";
            item.IsSelected = false;
            TrashedItems.Add(item);

            UpdateStats();
            ToastService.Instance.ShowInfo($"«{item.Title}» به سطل زباله منتقل شد.");
        }

        [RelayCommand]
        private void MoveSelectedToTrash()
        {
            var selected = BrokenItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ موردی انتخاب نشده است.");
                return;
            }

            var settings = SettingsManager.LoadSettings();
            foreach (var item in selected)
            {
                settings.TrashedBrokenDbIds.Add(item.DbId);
                BrokenItems.Remove(item);
                item.Status = "سطل زباله 🗑️";
                item.IsSelected = false;
                TrashedItems.Add(item);
            }

            SettingsManager.SaveSettings(settings);
            UpdateStats();
            ToastService.Instance.ShowInfo($"{selected.Count} مورد به سطل زباله منتقل شدند.");
        }

        [RelayCommand]
        private void RestoreFromTrash(BrokenItemModel item)
        {
            if (item == null) return;

            var settings = SettingsManager.LoadSettings();
            settings.TrashedBrokenDbIds.Remove(item.DbId);
            SettingsManager.SaveSettings(settings);

            TrashedItems.Remove(item);
            item.Status = "گمشده ❌";
            item.IsSelected = false;
            BrokenItems.Add(item);

            UpdateStats();
            ToastService.Instance.ShowSuccess($"«{item.Title}» به لیست ترمیم بازگردانده شد.");
        }

        [RelayCommand]
        private void RestoreSelectedFromTrash()
        {
            var selected = TrashedItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ موردی انتخاب نشده است.");
                return;
            }

            var settings = SettingsManager.LoadSettings();
            foreach (var item in selected)
            {
                settings.TrashedBrokenDbIds.Remove(item.DbId);
                TrashedItems.Remove(item);
                item.Status = "گمشده ❌";
                item.IsSelected = false;
                BrokenItems.Add(item);
            }

            SettingsManager.SaveSettings(settings);
            UpdateStats();
            ToastService.Instance.ShowSuccess($"{selected.Count} مورد به لیست ترمیم بازگردانده شدند.");
        }

        [RelayCommand]
        private async Task DeletePermanentlyAsync(BrokenItemModel item)
        {
            if (item == null) return;

            var result = MessageBox.Show($"آیا مطمئن هستید که می‌خواهید «{item.Title}» را برای همیشه از دیتابیس برنامه حذف کنید؟",
                "حذف دائمی", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var file = await _dbContext.VideoFiles.FindAsync(item.DbId);
                if (file != null)
                {
                    _dbContext.VideoFiles.Remove(file);
                    await _dbContext.SaveChangesAsync();
                }

                var settings = SettingsManager.LoadSettings();
                settings.TrashedBrokenDbIds.Remove(item.DbId);
                SettingsManager.SaveSettings(settings);

                TrashedItems.Remove(item);
                BrokenItems.Remove(item);

                UpdateStats();
                ToastService.Instance.ShowSuccess($"«{item.Title}» برای همیشه از برنامه حذف شد.");
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            }
        }

        [RelayCommand]
        private async Task EmptyTrashAsync()
        {
            if (TrashedItems.Count == 0)
            {
                ToastService.Instance.ShowInfo("سطل زباله خالی است.");
                return;
            }

            var result = MessageBox.Show($"آیا مطمئن هستید که می‌خواهید تمام {TrashedItems.Count} آیتم موجود در سطل زباله را برای همیشه از دیتابیس برنامه حذف کنید؟",
                "تخلیه کامل سطل زباله", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                IsScanning = true;
                ScanProgressText = "در حال حذف آیتم‌ها از دیتابیس...";

                try
                {
                    var trashedList = TrashedItems.ToList();
                    var settings = SettingsManager.LoadSettings();

                    foreach (var item in trashedList)
                    {
                        var file = await _dbContext.VideoFiles.FindAsync(item.DbId);
                        if (file != null) _dbContext.VideoFiles.Remove(file);
                        settings.TrashedBrokenDbIds.Remove(item.DbId);
                    }

                    await _dbContext.SaveChangesAsync();
                    SettingsManager.SaveSettings(settings);

                    TrashedItems.Clear();
                    UpdateStats();

                    ToastService.Instance.ShowSuccess("سطل زباله با موفقیت تخلیه شد.");
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
                catch (Exception ex)
                {
                    LoggerService.Error("Error emptying trash", ex);
                    ToastService.Instance.ShowError($"خطا در تخلیه سطل زباله: {ex.Message}");
                }
                finally
                {
                    IsScanning = false;
                    ScanProgressText = string.Empty;
                }
            }
        }

        [RelayCommand]
        private void ToggleSelectAll()
        {
            IsAllSelected = !IsAllSelected;
        }

        private void UpdateStats()
        {
            TotalBroken = BrokenItems.Count;
            TotalResolved = BrokenItems.Count(i => i.IsResolved);
            TotalTrashed = TrashedItems.Count;
        }

        private void SafeGetVideoFiles(string path, List<string> files)
        {
            try
            {
                if (!Directory.Exists(path)) return;

                // 1. Get files in current directory
                try
                {
                    var directoryFiles = Directory.GetFiles(path).Where(f => IsVideoFile(f));
                    files.AddRange(directoryFiles);
                }
                catch { }

                // 2. Get subdirectories safely
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(path);
                }
                catch
                {
                    return;
                }

                foreach (var d in directories)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(d);
                        // Skip system and hidden folders like $RECYCLE.BIN, System Volume Information
                        if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                            (dirInfo.Attributes & FileAttributes.System) != 0 ||
                            d.Contains("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                            d.Contains("System Volume Information", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        SafeGetVideoFiles(d, files);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private bool IsVideoFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".wmv" || ext == ".mov" || ext == ".flv" || ext == ".webm" || ext == ".m4v" || ext == ".ts";
        }

        private static string NormalizeTitleForMatching(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // 1. Remove extension
            string text = Path.GetFileNameWithoutExtension(input);

            // 2. Replace dots, underscores, dashes, brackets with spaces
            text = Regex.Replace(text, @"[\._\-+\[\]\(\)\{\}:]+", " ");

            // 3. Remove leading numbering (e.g. "01.", "01 ", "02 - ", "1", "1Captain", "1. ")
            text = Regex.Replace(text, @"^\s*\d{1,3}\s*[\.\-]?\s*", " ");
            text = Regex.Replace(text, @"^\s*\d{1,3}(?=[A-Za-z\u0600-\u06FF])", " ");

            // 4. Remove release keywords, qualities, codecs, site tags
            text = Regex.Replace(text, @"\b(1080p|720p|480p|2160p|4k|uhd|fhd|hd|sd|remastered|proper|bluray|blu\-ray|brrip|web[\- ]?dl|webrip|hdtc|hdcam|dvd|remux|pahe|psa|yify|golchindl|valamovie|film2media|farsi|dubbed|duble|dub|softsub|sub|x264|x265|hevc|10bit|aac|ac3|dts|6ch|dd\+?7\.1|v2|v3)\b", "", RegexOptions.IgnoreCase);

            // 5. Remove 4-digit years (1900-2099)
            text = Regex.Replace(text, @"\b(19\d{2}|20\d{2})\b", "");

            // 6. Clean whitespace and lowercase
            text = Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();
            return text;
        }

        private static bool IsTitleFuzzyMatch(string norm1, string norm2)
        {
            if (string.IsNullOrWhiteSpace(norm1) || string.IsNullOrWhiteSpace(norm2)) return false;
            if (norm1 == norm2) return true;

            // Token set overlap
            var words1 = norm1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var words2 = norm2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (words1.Count > 0 && words2.Count > 0)
            {
                int common = words1.Intersect(words2).Count();
                int minWords = Math.Min(words1.Count, words2.Count);
                if (minWords > 0 && common >= minWords) return true;
                double ratio = (double)common / Math.Max(words1.Count, words2.Count);
                if (ratio >= 0.75) return true;
            }

            return false;
        }

        private (int? season, int? episode) ExtractSeasonEpisode(string fileName)
        {
            var match = Regex.Match(fileName, @"[Ss](\d{1,2})[Ee](\d{1,3})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }

            var altMatch = Regex.Match(fileName, @"(\d{1,2})x(\d{1,3})", RegexOptions.IgnoreCase);
            if (altMatch.Success)
            {
                return (int.Parse(altMatch.Groups[1].Value), int.Parse(altMatch.Groups[2].Value));
            }

            return (null, null);
        }
    }
}
