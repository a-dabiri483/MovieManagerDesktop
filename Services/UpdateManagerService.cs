using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MovieManagerDesktop.Helpers;
using MovieManagerDesktop.Views;

namespace MovieManagerDesktop.Services
{
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
    /// notify users and open download links.
    /// </summary>
    public static class UpdateManagerService
    {
        public const string CurrentAppVersion = "2.6.0";
        public const int CurrentVersionCode = 260;
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
    }
}
