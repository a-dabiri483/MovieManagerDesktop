using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public static class BingWallpaperService
    {
        private static readonly HttpClient _httpClient = new(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static string GetCacheDirectory()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop", "Cache");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
            return appData;
        }

        public static async Task<string?> GetDailyWallpaperPathAsync(CancellationToken ct = default)
        {
            try
            {
                string cacheDir = GetCacheDirectory();
                string wallpaperPath = Path.Combine(cacheDir, "bing_daily.jpg");
                string timestampPath = Path.Combine(cacheDir, "bing_daily_date.txt");

                string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

                // Check if already downloaded today
                if (File.Exists(wallpaperPath) && File.Exists(timestampPath))
                {
                    string lastDate = await File.ReadAllTextAsync(timestampPath, ct);
                    if (lastDate.Trim() == todayStr && new FileInfo(wallpaperPath).Length > 1000)
                    {
                        return wallpaperPath;
                    }
                }

                // Fetch new image URL from Bing API
                string apiUrl = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US";
                var response = await _httpClient.GetAsync(apiUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
                    {
                        var firstImg = images.EnumerateArray().FirstOrDefault();
                        if (firstImg.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                        {
                            string imgUrl = "https://www.bing.com" + urlProp.GetString();
                            var imgBytes = await _httpClient.GetByteArrayAsync(imgUrl, ct);

                            if (imgBytes.Length > 1000)
                            {
                                await File.WriteAllBytesAsync(wallpaperPath, imgBytes, ct);
                                await File.WriteAllTextAsync(timestampPath, todayStr, ct);
                                return wallpaperPath;
                            }
                        }
                    }
                }

                if (File.Exists(wallpaperPath))
                {
                    return wallpaperPath;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[Bing] Error fetching daily wallpaper: {ex.Message}");
            }

            return null;
        }
    }
}
