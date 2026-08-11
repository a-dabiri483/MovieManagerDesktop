using System;
using System.IO;
using System.Text.Json;

namespace MovieManagerDesktop.Services
{
    public class SettingsModel
    {
        public string SelectedDataSource { get; set; } = "TMDB_ONLY"; // TMDB_ONLY, OMDB_ONLY
        public string TmdbApiKey { get; set; } = string.Empty;
        public string OmdbApiKey { get; set; } = string.Empty;
        public string ApiProxyUrl { get; set; } = string.Empty;
        public bool IsApiProxyEnabled { get; set; } = false;
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

        // Default keys matching Android app
        public static readonly string[] DefaultTmdbKeys = { "a8a9cd082993b9e77b813263981e408b", "c0d46b49ab0f16cd8f7101f2d49defc9" };
        public static readonly string[] DefaultOmdbKeys = { "14722d17", "a3c969fb" };
        public static readonly string[] DefaultProxyUrls = { "https://moviemanager2.ali483.workers.dev/", "https://my-proxyali.ali-dabiri1.workers.dev/" };

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

        public static string WrapUrlWithProxy(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return url;

            var settings = LoadSettings();
            if (settings.IsApiProxyEnabled && !string.IsNullOrWhiteSpace(settings.ApiProxyUrl))
            {
                string proxy = settings.ApiProxyUrl.Trim();
                if (url.StartsWith(proxy, StringComparison.OrdinalIgnoreCase))
                    return url; // Already proxied

                // Remove trailing slashes
                proxy = proxy.TrimEnd('/');
                
                // Add the correct query parameter for cloudflare worker proxy
                if (proxy.Contains("?"))
                {
                    if (!proxy.EndsWith("url=")) proxy += "&url=";
                }
                else
                {
                    proxy += "/?url=";
                }
                
                return proxy + Uri.EscapeDataString(url);
            }
            return url;
        }
    }
}
