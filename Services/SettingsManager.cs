using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MovieManagerDesktop.Helpers;

namespace MovieManagerDesktop.Services
{
    public class SettingsModel
    {
        public string SelectedDataSource { get; set; } = "TMDB_ONLY"; // TMDB_ONLY, OMDB_ONLY
        public string TmdbApiKey { get; set; } = string.Empty;
        public string OmdbApiKey { get; set; } = string.Empty;
        public string ApiProxyUrl { get; set; } = string.Empty;
        public string DynamicProxySourceUrl { get; set; } = string.Empty;
        public bool IsApiProxyEnabled { get; set; } = true;
        public string TmdbLanguage { get; set; } = "fa-IR"; // fa-IR or en-US
        public int PosterSize { get; set; } = 220;
        public string Theme { get; set; } = "Cyan"; // Cyan, MidnightBlue, OLEDBlack
        public bool IsDarkTheme { get; set; } = true;
        public int MediaTypeFilterIndex { get; set; } = 0;
        public int WatchedFilterIndex { get; set; } = 0;
        public int ListFilterIndex { get; set; } = 0;
        public int SortIndex { get; set; } = 0;
        public int SortDirectionIndex { get; set; } = 0;
        public int SelectedGenreIndex { get; set; } = 0;
        public bool IsQuickFilterMovies { get; set; } = false;
        public bool IsQuickFilterSeries { get; set; } = false;
        public bool IsQuickFilterUnwatched { get; set; } = false;
        
        // Auto Backup Settings
        public bool IsLocalAutoBackupEnabled { get; set; } = false;
        public string LocalAutoBackupPath { get; set; } = string.Empty;
        public bool IsGoogleDriveAutoBackupEnabled { get; set; } = false;
        public int BackupFrequencyIndex { get; set; } = 0; // 0: Always on exit, 1: Daily, 2: Weekly
        public DateTime LastBackupTime { get; set; } = DateTime.MinValue;

        // Trashed Broken Items (AutoRelocator)
        public List<int> TrashedBrokenDbIds { get; set; } = new();
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static SettingsModel LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var options = new JsonSerializerOptions 
                    { 
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                    };
                    return JsonSerializer.Deserialize<SettingsModel>(json, options) ?? new SettingsModel();
                }
                catch
                {
                    return new SettingsModel();
                }
            }
            return new SettingsModel();
        }

        // Default API keys matching Android app
        public static readonly string[] DefaultTmdbKeys = { "a8a9cd082993b9e77b813263981e408b", "c0d46b49ab0f16cd8f7101f2d49defc9" };
        public static readonly string[] DefaultOmdbKeys = { "14722d17", "a3c969fb" };
        
        // No hardcoded proxy URLs in code; dynamically fetched from secure GitHub remote
        public static readonly string[] DefaultProxyUrls = Array.Empty<string>();

        public static string GetTmdbApiKey()
        {
            var settings = LoadSettings();
            var keys = string.IsNullOrWhiteSpace(settings.TmdbApiKey) 
                ? DefaultTmdbKeys 
                : settings.TmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (keys.Length == 0) return DefaultTmdbKeys[0];
            return keys[new Random().Next(keys.Length)].Trim();
        }

        public static string GetOmdbApiKey()
        {
            var settings = LoadSettings();
            var keys = string.IsNullOrWhiteSpace(settings.OmdbApiKey) 
                ? DefaultOmdbKeys 
                : settings.OmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (keys.Length == 0) return DefaultOmdbKeys[0];
            return keys[new Random().Next(keys.Length)].Trim();
        }

        public static void SaveSettings(SettingsModel settings)
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsFilePath, json);
        }

        /// <summary>
        /// Fetches the encrypted proxy list from remote repository, decrypts it in-memory using AES-256,
        /// and updates the settings with the active proxies.
        /// </summary>
        public static async Task<(bool success, int count, string message)> SyncEncryptedProxiesAsync(string? customUrl = null)
        {
            try
            {
                var settings = LoadSettings();
                string targetUrl = !string.IsNullOrWhiteSpace(customUrl) 
                    ? customUrl 
                    : (!string.IsNullOrWhiteSpace(settings.DynamicProxySourceUrl) 
                        ? settings.DynamicProxySourceUrl 
                        : CryptoUtils.GetObfuscatedSourceUrl());

                // Prevent CDN caching
                string cacheBuster = targetUrl.Contains("?") ? $"&_t={DateTime.UtcNow.Ticks}" : $"?_t={DateTime.UtcNow.Ticks}";
                string requestUrl = targetUrl + cacheBuster;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };

                var response = await client.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, 0, $"خطا در برقراری ارتباط با منبع پروکسی: {(int)response.StatusCode}");
                }

                string encryptedText = (await response.Content.ReadAsStringAsync()).Trim();
                if (string.IsNullOrWhiteSpace(encryptedText))
                {
                    // Remote list is empty -> Clear local saved proxies
                    settings.ApiProxyUrl = string.Empty;
                    settings.IsApiProxyEnabled = false;
                    SaveSettings(settings);
                    Network.ProxyHttpClientHandler.ClearCache();
                    LoggerService.Info("[ProxySync] Remote proxy list is empty. Cleared all local proxies.");
                    return (true, 0, "لیست پروکسی‌ها در سرور گیت‌هاب خالی است. تمامی سرورها از برنامه حذف و غیرفعال شدند.");
                }

                string? decrypted = CryptoUtils.Decrypt(encryptedText);
                if (string.IsNullOrWhiteSpace(decrypted))
                {
                    settings.ApiProxyUrl = string.Empty;
                    settings.IsApiProxyEnabled = false;
                    SaveSettings(settings);
                    Network.ProxyHttpClientHandler.ClearCache();
                    return (true, 0, "داده‌های پروکسی در سرور نامعتبر یا خالی بودند؛ سرورها پاکسازی شدند.");
                }

                var proxyList = decrypted.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => p.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (proxyList.Count == 0)
                {
                    settings.ApiProxyUrl = string.Empty;
                    settings.IsApiProxyEnabled = false;
                    SaveSettings(settings);
                    Network.ProxyHttpClientHandler.ClearCache();
                    return (true, 0, "هیچ سرور پروکسی فعالی در گیت‌هاب یافت نشد. لیست پروکسی‌ها خالی شد.");
                }

                settings.ApiProxyUrl = string.Join(",", proxyList);
                settings.IsApiProxyEnabled = true;
                SaveSettings(settings);

                Network.ProxyHttpClientHandler.ClearCache();
                LoggerService.Info($"[ProxySync] Successfully synced {proxyList.Count} proxies from cloud.");

                return (true, proxyList.Count, $"{proxyList.Count} سرور پروکسی ضدتحریم با موفقیت دریافت و فعال شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("[ProxySync] Failed to sync proxies", ex);
                return (false, 0, $"خطا در همگام‌سازی پروکسی: {ex.Message}");
            }
        }

        public static string WrapUrlWithProxy(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            // If the URL was previously wrapped with a proxy (?url=...), extract the clean original URL
            int urlParamIndex = url.IndexOf("url=", StringComparison.OrdinalIgnoreCase);
            if (urlParamIndex >= 0)
            {
                string raw = url.Substring(urlParamIndex + 4);
                try
                {
                    return Uri.UnescapeDataString(raw);
                }
                catch
                {
                    return raw;
                }
            }
            return url;
        }
    }
}
