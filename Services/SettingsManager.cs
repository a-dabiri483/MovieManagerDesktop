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
        public bool IsGridView { get; set; } = true;
        
        // Auto Backup Settings
        public bool IsLocalAutoBackupEnabled { get; set; } = false;
        public string LocalAutoBackupPath { get; set; } = string.Empty;
        public bool IsGoogleDriveAutoBackupEnabled { get; set; } = false;
        public int BackupFrequencyIndex { get; set; } = 0; // 0: Always on exit, 1: Daily, 2: Weekly
        public DateTime LastBackupTime { get; set; } = DateTime.MinValue;

        // Internal Encrypted Proxies (Silent background anti-sanction sync)
        public string InternalEncryptedProxies { get; set; } = string.Empty;
        public DateTime InternalProxiesLastSyncTime { get; set; } = DateTime.MinValue;

        // Personalization & Localization Settings
        public string DateFormatOverride { get; set; } = "jalali"; // "jalali" (شمسی) or "gregorian" (میلادی)
        public string GenreLanguageOverride { get; set; } = "fa"; // "fa", "en", "auto"
        public string TranslateToLanguage { get; set; } = "fa"; // "fa", "en", "auto", "fr", "de", "es", "it", "tr", "ar", "ru", "zh", "ja", "ko", "hi", "pt"
        public string FetchInfoLanguage { get; set; } = "fa-IR";
        public bool ShowActorImages { get; set; } = true;
        public bool HideAdultContent { get; set; } = false;

        // Video Player Settings
        public bool UseInternalPlayer { get; set; } = true;
        public string ExternalPlayerType { get; set; } = "SystemDefault"; // SystemDefault, PotPlayer, VLC, Custom
        public string CustomExternalPlayerPath { get; set; } = string.Empty;
        public double? PlayerWindowWidth { get; set; } = null;
        public double? PlayerWindowHeight { get; set; } = null;
        public double? PlayerWindowLeft { get; set; } = null;
        public double? PlayerWindowTop { get; set; } = null;
        public bool PlayerAlwaysOnTop { get; set; } = false;
        public int PlayerVolume { get; set; } = 100;
        public int SubtitleFontSize { get; set; } = 28;
        public string SubtitleColorHex { get; set; } = "#FFFFFF";
        public string SubtitleFontFamily { get; set; } = "Vazirmatn";
        public bool IsSubtitleBold { get; set; } = true;
        public bool HasSubtitleBackground { get; set; } = false;
        public string SubtitleBackgroundColorHex { get; set; } = "#000000";
        public int SubtitleBgOpacityPercent { get; set; } = 75;
        public int SubtitleBottomMargin { get; set; } = 40;
        public string SubtitleAlignment { get; set; } = "Center";
        public bool HasSubtitleShadow { get; set; } = true;

        // Trashed Broken Items (AutoRelocator)
        public List<int> TrashedBrokenDbIds { get; set; } = new();
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        private static readonly object _fileLock = new();
        private static volatile SettingsModel? _cachedSettings = null;

        public static SettingsModel LoadSettings()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            lock (_fileLock)
            {
                if (_cachedSettings != null)
                {
                    return _cachedSettings;
                }

                if (File.Exists(SettingsFilePath))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using var fs = new FileStream(SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var reader = new StreamReader(fs);
                            var json = reader.ReadToEnd();
                            var options = new JsonSerializerOptions 
                            { 
                                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                            };
                            _cachedSettings = JsonSerializer.Deserialize<SettingsModel>(json, options) ?? new SettingsModel();
                            return _cachedSettings;
                        }
                        catch (IOException)
                        {
                            System.Threading.Thread.Sleep(50);
                        }
                        catch
                        {
                            _cachedSettings = new SettingsModel();
                            return _cachedSettings;
                        }
                    }
                }
                _cachedSettings = new SettingsModel();
                return _cachedSettings;
            }
        }

        // Default API keys matching Android app
        public static readonly string[] DefaultTmdbKeys = { "a8a9cd082993b9e77b813263981e408b", "c0d46b49ab0f16cd8f7101f2d49defc9" };
        public static readonly string[] DefaultOmdbKeys = { "14722d17", "a3c969fb" };
        
        // No hardcoded proxy URLs in code; dynamically fetched from secure GitHub remote
        public static readonly string[] DefaultProxyUrls = Array.Empty<string>();

        public static string GetTmdbApiKey()
        {
            var settings = LoadSettings();
            var savedKeys = string.IsNullOrWhiteSpace(settings.TmdbApiKey) 
                ? Array.Empty<string>() 
                : settings.TmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToArray();
            
            var allKeys = DefaultTmdbKeys.Union(savedKeys).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToArray();
            if (allKeys.Length == 0) return DefaultTmdbKeys[0];
            return allKeys[new Random().Next(allKeys.Length)].Trim();
        }

        public static string GetOmdbApiKey()
        {
            var settings = LoadSettings();
            var savedKeys = string.IsNullOrWhiteSpace(settings.OmdbApiKey) 
                ? Array.Empty<string>() 
                : settings.OmdbApiKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToArray();
            
            var allKeys = DefaultOmdbKeys.Union(savedKeys).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToArray();
            if (allKeys.Length == 0) return DefaultOmdbKeys[0];
            return allKeys[new Random().Next(allKeys.Length)].Trim();
        }

        public static List<string> GetEffectiveProxies()
        {
            var settings = LoadSettings();
            var proxies = new List<string>();

            // 1. User manual proxies if enabled
            if (settings.IsApiProxyEnabled && !string.IsNullOrWhiteSpace(settings.ApiProxyUrl))
            {
                proxies.AddRange(settings.ApiProxyUrl.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
            }

            // 2. Internal background proxies (from 24h cloud sync)
            if (!string.IsNullOrWhiteSpace(settings.InternalEncryptedProxies))
            {
                proxies.AddRange(settings.InternalEncryptedProxies.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
            }

            return proxies.Where(p => !string.IsNullOrWhiteSpace(p) && p.StartsWith("http", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        }

        public static void SaveSettings(SettingsModel settings)
        {
            _cachedSettings = settings;
            lock (_fileLock)
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var options = new JsonSerializerOptions 
                        { 
                            WriteIndented = true,
                            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                        };
                        var json = JsonSerializer.Serialize(settings, options);
                        using var fs = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        using var writer = new StreamWriter(fs);
                        writer.Write(json);
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Fetches the encrypted proxy list from remote repository every 24h or on failure,
        /// decrypts it in-memory using AES-256, and stores it in InternalEncryptedProxies.
        /// </summary>
        public static async Task<(bool success, int count, string message)> SyncEncryptedProxiesAsync(bool force = false, string? customUrl = null)
        {
            try
            {
                var settings = LoadSettings();

                // Check 24-hour cache if not forced
                if (!force && !string.IsNullOrWhiteSpace(settings.InternalEncryptedProxies) && (DateTime.UtcNow - settings.InternalProxiesLastSyncTime).TotalHours < 24)
                {
                    var count = settings.InternalEncryptedProxies.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    return (true, count, "پروکسی‌های ضدتحریم قبلاً در ۲۴ ساعت گذشته به‌روزرسانی شده‌اند.");
                }

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
                    settings.InternalEncryptedProxies = string.Empty;
                    settings.InternalProxiesLastSyncTime = DateTime.UtcNow;
                    SaveSettings(settings);
                    Network.ProxyHttpClientHandler.ClearCache();
                    return (true, 0, "لیست پروکسی‌ها در سرور گیت‌هاب خالی است.");
                }

                string? decrypted = CryptoUtils.Decrypt(encryptedText);
                if (string.IsNullOrWhiteSpace(decrypted))
                {
                    return (false, 0, "رمزگشایی داده‌های پروکسی با خطا مواجه شد.");
                }

                var proxyList = decrypted.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => p.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (proxyList.Count == 0)
                {
                    return (true, 0, "هیچ سرور پروکسی فعالی در منبع یافت نشد.");
                }

                settings.InternalEncryptedProxies = string.Join(",", proxyList);
                settings.InternalProxiesLastSyncTime = DateTime.UtcNow;
                SaveSettings(settings);

                Network.ProxyHttpClientHandler.ClearCache();
                LoggerService.Info($"[ProxySync] Successfully synced and cached {proxyList.Count} internal proxies for 24 hours.");

                return (true, proxyList.Count, $"{proxyList.Count} سرور پروکسی ضدتحریم با موفقیت همگام‌سازی شد.");
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
