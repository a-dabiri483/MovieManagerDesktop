using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MovieManagerDesktop.Helpers;
using MovieManagerDesktop.Views;

namespace MovieManagerDesktop.Services
{
    public class UpdateDownloadProgress
    {
        public double Percentage { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSecond { get; set; }
        public string StatusMessage { get; set; } = string.Empty;

        public string DownloadedText => $"{DownloadedBytes / (1024.0 * 1024.0):0.1} مگابایت";
        public string TotalText => TotalBytes > 0 ? $"{TotalBytes / (1024.0 * 1024.0):0.1} مگابایت" : "نامشخص";
        public string SpeedText => SpeedBytesPerSecond > 1024 * 1024
            ? $"{SpeedBytesPerSecond / (1024.0 * 1024.0):0.1} MB/s"
            : $"{SpeedBytesPerSecond / 1024.0:0} KB/s";
    }

    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public int VersionCode { get; set; } = 1;
        public bool IsMandatory { get; set; }
        public string ReleaseDate { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service to check for software updates from moviemanager.ir server,
    /// notify users and handle direct in-app download and installation.
    /// </summary>
    public static class UpdateManagerService
    {
        public const string CurrentAppVersion = "2.7.0";
        public const int CurrentVersionCode = 270;
        private const string CheckUpdateUrl = "https://moviemanager.ir/license/api.php?action=check_update&platform=windows";

        private static bool _isDialogOpen = false;

        /// <summary>
        /// Checks the official API for Windows updates.
        /// </summary>
        public static async Task<UpdateCheckResult?> CheckForUpdatesAsync(bool silent = true)
        {
            try
            {
                string requestUrl = $"{CheckUpdateUrl}&version={Uri.EscapeDataString(CurrentAppVersion)}";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                var response = await client.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    LoggerService.Warning($"[UpdateCheck] Server returned HTTP {response.StatusCode}");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                if (!success)
                {
                    return null;
                }

                var result = new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = root.TryGetProperty("current_version", out var cv) ? cv.GetString() ?? CurrentAppVersion : CurrentAppVersion,
                    LatestVersion = root.TryGetProperty("latest_version", out var lv) ? lv.GetString() ?? CurrentAppVersion : CurrentAppVersion,
                    HasUpdate = root.TryGetProperty("has_update", out var hu) && hu.GetBoolean(),
                    IsMandatory = root.TryGetProperty("is_mandatory", out var im) && im.GetBoolean(),
                    ReleaseDate = root.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "",
                    DownloadUrl = root.TryGetProperty("download_url", out var du) ? du.GetString() ?? "" : "",
                    FileSize = root.TryGetProperty("file_size", out var fs) ? fs.GetString() ?? "" : "",
                    Changelog = root.TryGetProperty("changelog", out var cl) ? cl.GetString() ?? "" : "",
                    Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : ""
                };

                if (root.TryGetProperty("version_code", out var vc) && vc.TryGetInt32(out int code))
                {
                    result.VersionCode = code;
                }

                return result;
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    LoggerService.Error("[UpdateCheck] Error checking for updates", ex);
                }
                return null;
            }
        }

        /// <summary>
        /// Displays the update modal dialog with full changelog and direct download link.
        /// </summary>
        public static void ShowUpdateDialog(UpdateCheckResult updateInfo)
        {
            if (_isDialogOpen) return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    _isDialogOpen = true;
                    var win = new UpdateAvailableWindow(updateInfo);
                    WindowHelper.SafeShowDialog(win);
                }
                catch (Exception ex)
                {
                    LoggerService.Error("[UpdateDialog] Failed to display update dialog", ex);
                }
                finally
                {
                    _isDialogOpen = false;
                }
            });
        }

        /// <summary>
        /// Helper to launch URL in default browser.
        /// </summary>
        public static void OpenDownloadUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[UpdateCheck] Failed to open download URL: {url}", ex);
                MessageBox.Show($"خطا در باز کردن لینک دانلود:\n{url}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Downloads the update installer file in chunks with live progress and speed reporting.
        /// Returns the path to the downloaded installer file in temp folder.
        /// </summary>
        public static async Task<string?> DownloadUpdateFileAsync(
            string downloadUrl,
            string version,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl)) return null;

            string cleanVer = (version ?? "latest").Trim().TrimStart('v', 'V');
            string tempFileName = $"MovieManager_Setup_v{cleanVer}.exe";
            string tempPath = Path.Combine(Path.GetTempPath(), tempFileName);

            // If an earlier file exists, remove it
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalDownloaded = 0;
            int bytesRead;

            var stopwatch = Stopwatch.StartNew();
            long lastBytes = 0;
            double currentSpeed = 0;
            long lastSpeedCheck = stopwatch.ElapsedMilliseconds;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalDownloaded += bytesRead;

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastSpeedCheck >= 400)
                {
                    double elapsedSec = (now - lastSpeedCheck) / 1000.0;
                    long bytesDelta = totalDownloaded - lastBytes;
                    currentSpeed = elapsedSec > 0 ? (bytesDelta / elapsedSec) : 0;
                    lastBytes = totalDownloaded;
                    lastSpeedCheck = now;
                }

                double percent = totalBytes > 0 ? ((double)totalDownloaded / totalBytes * 100.0) : 0;
                progress?.Report(new UpdateDownloadProgress
                {
                    Percentage = percent,
                    DownloadedBytes = totalDownloaded,
                    TotalBytes = totalBytes,
                    SpeedBytesPerSecond = currentSpeed,
                    StatusMessage = "در حال دریافت بسته بروزرسانی..."
                });
            }

            await fileStream.FlushAsync(cancellationToken);

            progress?.Report(new UpdateDownloadProgress
            {
                Percentage = 100,
                DownloadedBytes = totalDownloaded,
                TotalBytes = totalBytes > 0 ? totalBytes : totalDownloaded,
                SpeedBytesPerSecond = 0,
                StatusMessage = "دانلود بسته بروزرسانی تکمیل شد."
            });

            return tempPath;
        }

        /// <summary>
        /// Executes the downloaded installer and gracefully exits the application
        /// so Inno Setup can overwrite files without file locks.
        /// </summary>
        public static void LaunchInstallerAndExit(string installerFilePath)
        {
            try
            {
                if (!File.Exists(installerFilePath))
                {
                    MessageBox.Show("فایل نصبی یافت نشد.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerFilePath,
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[AutoUpdater] Failed to launch installer: {installerFilePath}", ex);
                MessageBox.Show($"خطا در اجرای برنامه نصب:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
