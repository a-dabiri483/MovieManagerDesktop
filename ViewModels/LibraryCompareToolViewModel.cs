using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public enum CompareCategory
    {
        MissingInMine,
        UpgradeCandidate,
        Matched,
        OnlyInMine
    }

    public class CompareItem
    {
        public string Title { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string? MyQuality { get; set; }
        public string? OtherQuality { get; set; }
        public string? MyPath { get; set; }
        public string? OtherPath { get; set; }
        public string MediaType { get; set; } = "Movie"; // Movie or Series
        public CompareCategory Category { get; set; }
        public int? TmdbId { get; set; }
        public double? Rating { get; set; }
        public string? PosterUrl { get; set; }

        public string StatusBadgeText => Category switch
        {
            CompareCategory.MissingInMine => "کسری شما (در هارد او موجود است)",
            CompareCategory.UpgradeCandidate => $"کیفیت بهتر در هارد او ({OtherQuality} > {MyQuality})",
            CompareCategory.Matched => "مشترک در هر دو آرشیو",
            CompareCategory.OnlyInMine => "انحصاری در آرشیو شما",
            _ => ""
        };

        public string StatusBadgeColor => Category switch
        {
            CompareCategory.MissingInMine => "#EB3B5A",
            CompareCategory.UpgradeCandidate => "#20BF6B",
            CompareCategory.Matched => "#4B6584",
            CompareCategory.OnlyInMine => "#FA8231",
            _ => "#778CA3"
        };
    }

    public partial class LibraryCompareToolViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _targetSourcePath = string.Empty;

        [ObservableProperty]
        private bool _isDatabaseMode = true; // true: SQLite DB, false: Folder Scan

        [ObservableProperty]
        private bool _isComparing = false;

        [ObservableProperty]
        private string _statusMessage = "منبع دوم (فایل دیتابیس یا پوشه هارد دوم) را انتخاب و روی «شروع مقایسه» کلیک کنید.";

        [ObservableProperty]
        private int _totalMyLibraryCount = 0;

        [ObservableProperty]
        private int _totalOtherSourceCount = 0;

        [ObservableProperty]
        private int _missingCount = 0;

        [ObservableProperty]
        private int _upgradeCount = 0;

        [ObservableProperty]
        private int _matchedCount = 0;

        [ObservableProperty]
        private int _onlyInMineCount = 0;

        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: Missing, 1: Upgrade, 2: Matched, 3: OnlyMine

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        private List<CompareItem> _allResults = new();

        [ObservableProperty]
        private ObservableCollection<CompareItem> _filteredResults = new();

        public LibraryCompareToolViewModel()
        {
            LoadMyLibraryCount();
        }

        private void LoadMyLibraryCount()
        {
            try
            {
                using var db = new AppDbContext();
                TotalMyLibraryCount = db.VideoFiles.Count();
            }
            catch { }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        [RelayCommand]
        private void BrowseTargetFile()
        {
            if (IsDatabaseMode)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "انتخاب فایل دیتابیس دوم (.db / .sqlite)",
                    Filter = "فایل‌های دیتابیس SQLite (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|همه فایل‌ها (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    TargetSourcePath = dialog.FileName;
                    StatusMessage = $"فایل دیتابیس انتخاب شد: {Path.GetFileName(TargetSourcePath)}";
                }
            }
            else
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "انتخاب پوشه هارد دوم برای مقایسه"
                };

                if (dialog.ShowDialog() == true)
                {
                    TargetSourcePath = dialog.FolderName;
                    StatusMessage = $"پوشه انتخاب شد: {TargetSourcePath}";
                }
            }
        }

        [RelayCommand]
        private async Task StartCompareAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetSourcePath))
            {
                ToastService.Instance.ShowWarning("لطفاً ابتدا فایل دیتابیس یا پوشه هارد دوم را انتخاب کنید.");
                return;
            }

            if (!File.Exists(TargetSourcePath) && !Directory.Exists(TargetSourcePath))
            {
                ToastService.Instance.ShowError("مسیر انتخاب‌شده معتبر نیست یا وجود ندارد.");
                return;
            }

            IsComparing = true;
            StatusMessage = "در حال خواندن اطلاعات آرشیو شما و منبع دوم...";
            _allResults.Clear();
            FilteredResults.Clear();

            await Task.Run(async () =>
            {
                try
                {
                    // 1. Load local library
                    List<VideoFile> myFiles;
                    using (var db = new AppDbContext())
                    {
                        myFiles = db.VideoFiles.ToList();
                    }

                    // 2. Load target library
                    List<VideoFile> otherFiles = new();

                    if (IsDatabaseMode && File.Exists(TargetSourcePath))
                    {
                        otherFiles = LoadFromExternalDatabase(TargetSourcePath);
                    }
                    else if (!IsDatabaseMode && Directory.Exists(TargetSourcePath))
                    {
                        otherFiles = ScanFolderQuick(TargetSourcePath);
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TotalMyLibraryCount = myFiles.Count;
                        TotalOtherSourceCount = otherFiles.Count;
                        StatusMessage = $"در حال مقایسه {myFiles.Count} آیتم داخلی با {otherFiles.Count} آیتم منبع دوم...";
                    });

                    // 3. Perform smart comparison
                    var results = PerformComparison(myFiles, otherFiles);
                    _allResults = results;

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        MissingCount = _allResults.Count(x => x.Category == CompareCategory.MissingInMine);
                        UpgradeCount = _allResults.Count(x => x.Category == CompareCategory.UpgradeCandidate);
                        MatchedCount = _allResults.Count(x => x.Category == CompareCategory.Matched);
                        OnlyInMineCount = _allResults.Count(x => x.Category == CompareCategory.OnlyInMine);

                        ApplyFilter();
                        StatusMessage = $"مقایسه کامل شد! {MissingCount} فیلم کسری و {UpgradeCount} مورد با کیفیت بهتر پیدا شد.";
                        ToastService.Instance.ShowSuccess("مقایسه آرشیوها با موفقیت پایان یافت.");
                    });
                }
                catch (Exception ex)
                {
                    LoggerService.Error("Error comparing libraries", ex);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"خطا در مقایسه: {ex.Message}";
                        ToastService.Instance.ShowError("خطایی در حین مقایسه رخ داد.");
                    });
                }
                finally
                {
                    App.Current.Dispatcher.Invoke(() => IsComparing = false);
                }
            });
        }

        private List<VideoFile> LoadFromExternalDatabase(string dbPath)
        {
            var list = new List<VideoFile>();
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT FormattedTitle, Year, Resolution, Quality, MediaType, TmdbId, Rating, PosterUrl, FilePath FROM VideoFiles;";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new VideoFile
                    {
                        FormattedTitle = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        Year = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Resolution = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Quality = reader.IsDBNull(3) ? null : reader.GetString(3),
                        MediaType = reader.IsDBNull(4) ? "Movie" : reader.GetString(4),
                        TmdbId = reader.IsDBNull(5) ? null : (int)reader.GetInt64(5),
                        Rating = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                        PosterUrl = reader.IsDBNull(7) ? null : reader.GetString(7),
                        FilePath = reader.IsDBNull(8) ? "" : reader.GetString(8)
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to read external db", ex);
            }
            return list;
        }

        private List<VideoFile> ScanFolderQuick(string folderPath)
        {
            var list = new List<VideoFile>();
            var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".wmv", ".ts" };

            try
            {
                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                     .Where(f => videoExtensions.Contains(Path.GetExtension(f)))
                                     .Take(5000)
                                     .ToList();

                var parser = new FileNameParser();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    string fileName = Path.GetFileName(file);
                    var parsed = parser.Parse(fileName, file);

                    list.Add(new VideoFile
                    {
                        FilePath = file,
                        FileName = fileName,
                        FormattedTitle = parsed.ParsedTitle,
                        Year = parsed.Year?.ToString(),
                        Resolution = parsed.Quality,
                        Quality = parsed.Quality,
                        FileSizeBytes = fileInfo.Length,
                        MediaType = parsed.MediaType
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to scan folder", ex);
            }
            return list;
        }

        private List<CompareItem> PerformComparison(List<VideoFile> myFiles, List<VideoFile> otherFiles)
        {
            var results = new List<CompareItem>();

            // Group files by simplified title key for fast lookup
            var myLookup = new Dictionary<string, VideoFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in myFiles)
            {
                string key = NormalizeTitle(f.FormattedTitle);
                if (!string.IsNullOrEmpty(key) && !myLookup.ContainsKey(key))
                {
                    myLookup[key] = f;
                }
            }

            var otherLookup = new Dictionary<string, VideoFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in otherFiles)
            {
                string key = NormalizeTitle(f.FormattedTitle);
                if (!string.IsNullOrEmpty(key) && !otherLookup.ContainsKey(key))
                {
                    otherLookup[key] = f;
                }
            }

            // 1. Check other files against my files
            foreach (var other in otherLookup.Values)
            {
                string key = NormalizeTitle(other.FormattedTitle);

                if (myLookup.TryGetValue(key, out var myMatch))
                {
                    int myQualityRank = GetQualityRank(myMatch.Resolution ?? myMatch.Quality ?? "");
                    int otherQualityRank = GetQualityRank(other.Resolution ?? other.Quality ?? "");

                    if (otherQualityRank > myQualityRank)
                    {
                        results.Add(new CompareItem
                        {
                            Title = other.FormattedTitle,
                            Year = other.Year ?? myMatch.Year,
                            MyQuality = myMatch.Resolution ?? myMatch.Quality ?? "کیفیت نامشخص",
                            OtherQuality = other.Resolution ?? other.Quality ?? "کیفیت بالاتر",
                            MyPath = myMatch.FilePath,
                            OtherPath = other.FilePath,
                            MediaType = other.MediaType ?? myMatch.MediaType ?? "Movie",
                            Category = CompareCategory.UpgradeCandidate,
                            TmdbId = other.TmdbId ?? myMatch.TmdbId,
                            Rating = other.Rating ?? myMatch.Rating,
                            PosterUrl = other.PosterUrl ?? myMatch.PosterUrl
                        });
                    }
                    else
                    {
                        results.Add(new CompareItem
                        {
                            Title = other.FormattedTitle,
                            Year = other.Year ?? myMatch.Year,
                            MyQuality = myMatch.Resolution ?? myMatch.Quality ?? "مشترک",
                            OtherQuality = other.Resolution ?? other.Quality ?? "مشترک",
                            MyPath = myMatch.FilePath,
                            OtherPath = other.FilePath,
                            MediaType = other.MediaType ?? myMatch.MediaType ?? "Movie",
                            Category = CompareCategory.Matched,
                            TmdbId = other.TmdbId ?? myMatch.TmdbId,
                            Rating = other.Rating ?? myMatch.Rating,
                            PosterUrl = other.PosterUrl ?? myMatch.PosterUrl
                        });
                    }
                }
                else
                {
                    // Missing in My Library
                    results.Add(new CompareItem
                    {
                        Title = other.FormattedTitle,
                        Year = other.Year,
                        MyQuality = "ندارید",
                        OtherQuality = other.Resolution ?? other.Quality ?? "1080p",
                        MyPath = "-",
                        OtherPath = other.FilePath,
                        MediaType = other.MediaType ?? "Movie",
                        Category = CompareCategory.MissingInMine,
                        TmdbId = other.TmdbId,
                        Rating = other.Rating,
                        PosterUrl = other.PosterUrl
                    });
                }
            }

            // 2. Check items only in My Library
            foreach (var my in myLookup.Values)
            {
                string key = NormalizeTitle(my.FormattedTitle);
                if (!otherLookup.ContainsKey(key))
                {
                    results.Add(new CompareItem
                    {
                        Title = my.FormattedTitle,
                        Year = my.Year,
                        MyQuality = my.Resolution ?? my.Quality ?? "دارید",
                        OtherQuality = "ندارد",
                        MyPath = my.FilePath,
                        OtherPath = "-",
                        MediaType = my.MediaType ?? "Movie",
                        Category = CompareCategory.OnlyInMine,
                        TmdbId = my.TmdbId,
                        Rating = my.Rating,
                        PosterUrl = my.PosterUrl
                    });
                }
            }

            return results;
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            string normalized = title.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\b(19|20)\d{2}\b", ""); // remove year
            normalized = Regex.Replace(normalized, @"[._\-\(\)\[\]\{\}]", " "); // remove separators
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private static int GetQualityRank(string quality)
        {
            string q = (quality ?? "").ToLower();
            if (q.Contains("2160") || q.Contains("4k") || q.Contains("uhd")) return 40;
            if (q.Contains("1080") || q.Contains("fhd") || q.Contains("fullhd")) return 30;
            if (q.Contains("720") || q.Contains("hd")) return 20;
            if (q.Contains("480") || q.Contains("sd") || q.Contains("dvd")) return 10;
            return 5;
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            ApplyFilter();
        }

        partial void OnSearchFilterChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            CompareCategory targetCategory = SelectedTabIndex switch
            {
                0 => CompareCategory.MissingInMine,
                1 => CompareCategory.UpgradeCandidate,
                2 => CompareCategory.Matched,
                3 => CompareCategory.OnlyInMine,
                _ => CompareCategory.MissingInMine
            };

            var list = _allResults.Where(x => x.Category == targetCategory);

            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                list = list.Where(x => x.Title.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                                       (x.Year != null && x.Year.Contains(SearchFilter)));
            }

            FilteredResults = new ObservableCollection<CompareItem>(list.OrderBy(x => x.Title));
        }

        [RelayCommand]
        private void ExportMissingToTxt()
        {
            var missingList = _allResults.Where(x => x.Category == CompareCategory.MissingInMine).ToList();
            if (missingList.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ عنوان کسری برای خروجی وجود ندارد.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "ذخیره لیست فیلم‌های کسری",
                Filter = "فایل متنی (*.txt)|*.txt|فایل اکسل/CSV (*.csv)|*.csv",
                FileName = $"Missing_Movies_{DateTime.Now:yyyyMMdd}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine("Title,Year,MediaType,Quality,OtherPath");
                        foreach (var item in missingList)
                        {
                            sb.AppendLine($"\"{item.Title}\",\"{item.Year}\",\"{item.MediaType}\",\"{item.OtherQuality}\",\"{item.OtherPath}\"");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"=== لیست فیلم‌ها و سریال‌های کسری آرشیو (تعداد: {missingList.Count}) ===");
                        sb.AppendLine($"تاریخ استخراج: {DateTime.Now:yyyy/MM/dd HH:mm}");
                        sb.AppendLine(new string('-', 60));
                        int idx = 1;
                        foreach (var item in missingList)
                        {
                            sb.AppendLine($"{idx++}. {item.Title} ({item.Year}) [{item.OtherQuality}]");
                        }
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    ToastService.Instance.ShowSuccess("لیست کسری‌ها با موفقیت ذخیره شد.");
                }
                catch (Exception ex)
                {
                    ToastService.Instance.ShowError($"خطا در ذخیره فایل: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void CopyMissingToClipboard()
        {
            var missingList = _allResults.Where(x => x.Category == CompareCategory.MissingInMine).ToList();
            if (missingList.Count == 0)
            {
                ToastService.Instance.ShowWarning("عنوان کسری برای کپی وجود ندارد.");
                return;
            }

            var sb = new StringBuilder();
            int idx = 1;
            foreach (var item in missingList)
            {
                sb.AppendLine($"{idx++}. {item.Title} ({item.Year})");
            }

            Clipboard.SetText(sb.ToString());
            ToastService.Instance.ShowSuccess($"{missingList.Count} مورد در کلیپ‌بورد کپی شد.");
        }
    }
}
