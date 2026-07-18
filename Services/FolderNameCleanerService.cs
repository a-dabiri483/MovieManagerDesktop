using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class RenameProgressReport
    {
        public FolderRenameItem Item { get; init; } = null!;
        public RenameStatus Status { get; init; }
        public string? ErrorMessage { get; init; }
        public string? NewPath { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public class FolderNameCleanerService
    {
        private static readonly Regex DirtyPattern = new Regex(@"[._]+", RegexOptions.Compiled);

        public Task<List<FolderRenameItem>> ScanForDirtyFoldersAsync(
            string rootPath, IProgress<string>? progress = null)
        {
            return Task.Run(() =>
            {
                var dirtyFolders = new List<FolderRenameItem>();
                try
                {
                    progress?.Report("در حال جستجوی پوشه‌ها...");
                    foreach (var dir in Directory.GetDirectories(rootPath))
                    {
                        string name = Path.GetFileName(dir);
                        if (name.Equals("ICON", StringComparison.OrdinalIgnoreCase)) continue;

                        string cleanName = name;
                        try 
                        {
                            var detector = new MovieFileLibrary.MovieDetector();
                            var movie = detector.GetInfo(name);
                            if (movie != null && !string.IsNullOrWhiteSpace(movie.Title))
                            {
                                cleanName = movie.Title + (string.IsNullOrEmpty(movie.Year) ? "" : $" {movie.Year}");
                                cleanName = SanitizeFolderName(cleanName);
                            }
                        }
                        catch { }

                        if (cleanName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!DirtyPattern.IsMatch(name)) continue;
                            cleanName = SanitizeFolderName(DirtyPattern.Replace(name, " ").Trim());
                        }

                        if (string.IsNullOrWhiteSpace(cleanName) ||
                            cleanName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        dirtyFolders.Add(new FolderRenameItem
                        {
                            OriginalPath = dir,
                            OriginalName = name,
                            NewName = cleanName,
                            HasIcon = HasCustomIcon(dir)
                        });
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"خطا در اسکن: {ex.Message}");
                }

                return dirtyFolders;
            });
        }

        public Task CleanFoldersAsync(
            IEnumerable<FolderRenameItem> items, IProgress<RenameProgressReport>? progress = null)
        {
            return Task.Run(() =>
            {
                int totalCount = items.Count();
                int current = 0;

                foreach (var item in items)
                {
                    current++;
                    if (item.Status == RenameStatus.Success) continue;

                    progress?.Report(new RenameProgressReport
                    {
                        Item = item,
                        Status = RenameStatus.Pending,
                        Message = $"در حال اصلاح ({current}/{totalCount}): {item.OriginalName}"
                    });

                    try
                    {
                        string parentDir = Path.GetDirectoryName(item.OriginalPath)!;
                        string newPath = Path.Combine(parentDir, item.NewName);

                        if (Directory.Exists(newPath))
                        {
                            progress?.Report(new RenameProgressReport
                            {
                                Item = item,
                                Status = RenameStatus.Error,
                                ErrorMessage = "پوشه‌ای با این نام جدید از قبل وجود دارد.",
                                Message = $"❌ {item.OriginalName}: نام تکراری"
                            });
                            continue;
                        }

                        RemoveReadonlyAttribute(item.OriginalPath);
                        Directory.Move(item.OriginalPath, newPath);

                        if (item.HasIcon)
                            FixDesktopIni(newPath, item.OriginalPath);

                        progress?.Report(new RenameProgressReport
                        {
                            Item = item,
                            Status = RenameStatus.Success,
                            NewPath = newPath,
                            Message = $"✅ {item.OriginalName} → {item.NewName}"
                        });
                    }
                    catch (Exception ex)
                    {
                        progress?.Report(new RenameProgressReport
                        {
                            Item = item,
                            Status = RenameStatus.Error,
                            ErrorMessage = ex.Message,
                            Message = $"❌ {item.OriginalName}: {ex.Message}"
                        });
                    }
                }
            });
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) < 0)
                    sb.Append(c);
            }
            return sb.ToString().Trim().TrimEnd('.');
        }

        private static bool HasCustomIcon(string folderPath)
        {
            try
            {
                string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                if (!File.Exists(desktopIniPath)) return false;

                return File.ReadAllLines(desktopIniPath).Any(line =>
                    line.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking custom icon in {folderPath}: {ex.Message}");
                return false;
            }
        }

        private static void RemoveReadonlyAttribute(string folderPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing readonly attribute from {folderPath}: {ex.Message}");
            }
        }

        private static void FixDesktopIni(string newFolderPath, string oldFolderPath)
        {
            try
            {
                string desktopIniPath = Path.Combine(newFolderPath, "desktop.ini");
                if (!File.Exists(desktopIniPath)) return;

                File.SetAttributes(desktopIniPath, FileAttributes.Normal);

                string[] lines = File.ReadAllLines(desktopIniPath);
                bool modified = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase) ||
                        lines[i].StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (lines[i].Contains(oldFolderPath, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = lines[i].Replace(oldFolderPath, newFolderPath, StringComparison.OrdinalIgnoreCase);
                            modified = true;
                        }
                    }
                }

                if (modified)
                    File.WriteAllLines(desktopIniPath, lines);

                WindowsApiService.SetFileAttributes(desktopIniPath,
                    WindowsApiService.FILE_ATTRIBUTE_SYSTEM | WindowsApiService.FILE_ATTRIBUTE_HIDDEN);
                WindowsApiService.SetFileAttributes(newFolderPath,
                    WindowsApiService.FILE_ATTRIBUTE_READONLY);

                WindowsApiService.RefreshFolder(newFolderPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fixing desktop.ini in {newFolderPath}: {ex.Message}");
            }
        }
    }
}
