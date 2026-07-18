using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class FolderScannerService
    {
        /// <summary>مسیر فایل ایکون سفارشی پوشه را از desktop.ini می‌خواند.</summary>
        public static string? ResolveIconPath(string folderPath)
        {
            try
            {
                string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                if (!File.Exists(desktopIniPath))
                    return TryDefaultIconPath(folderPath);

                foreach (var rawLine in File.ReadAllLines(desktopIniPath))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line["IconResource=".Length..].Trim();
                        int comma = value.IndexOf(',');
                        if (comma >= 0)
                            value = value[..comma].Trim();

                        value = Environment.ExpandEnvironmentVariables(value);
                        if (File.Exists(value))
                            return value;
                    }
                    else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line["IconFile=".Length..].Trim();
                        value = Environment.ExpandEnvironmentVariables(value);
                        if (!Path.IsPathRooted(value))
                            value = Path.Combine(folderPath, value);
                        if (File.Exists(value))
                            return value;
                    }
                }

                return TryDefaultIconPath(folderPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving icon path for {folderPath}: {ex.Message}");
                return null;
            }
        }

        private static string? TryDefaultIconPath(string folderPath)
        {
            string defaultIcon = Path.Combine(folderPath, "ICON", "icon.ico");
            return File.Exists(defaultIcon) ? defaultIcon : null;
        }

        private static bool ShouldSkipFolder(string folderPath)
        {
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(name)) return true;
            if (name.Equals("ICON", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith('$') || name.StartsWith('.')) return true;
            return false;
        }

        /// <summary>فقط زیرپوشه‌های مستقیم (لایه اول) را اسکن می‌کند.</summary>
        public Task<(List<FolderInfo> withIcons, List<FolderInfo> withoutIcons)> ScanFoldersAsync(
            string rootPath, IProgress<ScanProgress>? progress = null)
        {
            return Task.Run(() =>
            {
                var foldersWithIcons = new List<FolderInfo>();
                var foldersWithoutIcons = new List<FolderInfo>();

                if (!Directory.Exists(rootPath))
                    return (foldersWithIcons, foldersWithoutIcons);

                var directories = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(d => !ShouldSkipFolder(d))
                    .ToList();

                int total = directories.Count;
                int processed = 0;

                foreach (var folder in directories)
                {

                    var iconPath = ResolveIconPath(folder);
                    var folderInfo = new FolderInfo
                    {
                        Path = folder,
                        Name = Path.GetFileName(folder),
                        HasIcon = iconPath != null,
                        IconPath = iconPath ?? string.Empty
                    };

                    if (folderInfo.HasIcon)
                        foldersWithIcons.Add(folderInfo);
                    else
                        foldersWithoutIcons.Add(folderInfo);

                    processed++;
                    progress?.Report(new ScanProgress
                    {
                        Processed = processed,
                        Total = total,
                        CurrentFolder = folderInfo.Name
                    });
                }

                return (foldersWithIcons, foldersWithoutIcons);
            });
        }

        public Task<(bool Success, string ErrorMessage)> ApplyIconToFolderAsync(string folderPath, string iconPath)
        {
            return Task.Run(() =>
            {
                try
                {
                    string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                    string desktopIniContent = $@"[.ShellClassInfo]
IconResource=ICON\icon.ico,0

[ViewState]
Mode=
Vid=
FolderType=Generic
";

                    if (File.Exists(desktopIniPath))
                    {
                        File.SetAttributes(desktopIniPath, FileAttributes.Normal);
                    }

                    File.WriteAllText(desktopIniPath, desktopIniContent, System.Text.Encoding.Unicode);

                    File.SetAttributes(desktopIniPath, FileAttributes.System | FileAttributes.Hidden);
                    
                    var dirInfo = new DirectoryInfo(folderPath);
                    dirInfo.Attributes |= FileAttributes.ReadOnly;

                    WindowsApiService.RefreshFolder(folderPath);
                    return (true, string.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying icon to {folderPath}: {ex.Message}");
                    return (false, ex.Message);
                }
            });
        }

        public Task<bool> RemoveIconFromFolderAsync(string folderPath)
        {
            return Task.Run(() =>
            {
                try
                {
                    string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                    string iconFolder = Path.Combine(folderPath, "ICON");

                    if (File.Exists(desktopIniPath))
                    {
                        File.SetAttributes(desktopIniPath, FileAttributes.Normal);
                        File.Delete(desktopIniPath);
                    }

                    if (Directory.Exists(iconFolder))
                    {
                        Directory.Delete(iconFolder, true);
                    }

                    var dirInfo = new DirectoryInfo(folderPath);
                    dirInfo.Attributes &= ~FileAttributes.ReadOnly;

                    WindowsApiService.RefreshFolder(folderPath);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error removing icon from {folderPath}: {ex.Message}");
                    return false;
                }
            });
        }
    }

    public class ScanProgress
    {
        public int Processed { get; set; }
        public int Total { get; set; }
        public string CurrentFolder { get; set; } = string.Empty;
    }
}
