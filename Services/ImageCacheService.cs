using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public static class ImageCacheService
    {
        private static readonly string CacheDirectory;
        private static readonly HttpClient _httpClient;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5); // limit concurrent downloads

        static ImageCacheService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            CacheDirectory = Path.Combine(appData, "CineTrackManager", "ImageCache");
            
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }

            _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static async Task<string> GetCachedImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // If it's already a local path, return it
            if (File.Exists(url) || url.StartsWith("pack://"))
                return url;

            string fileName = GetHashString(url) + Path.GetExtension(url).Split('?')[0];
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                fileName += ".jpg";
            }
            
            string localPath = Path.Combine(CacheDirectory, fileName);

            if (File.Exists(localPath))
            {
                return localPath;
            }

            // Not in cache, need to download
            await _semaphore.WaitAsync();
            try
            {
                // Double check if another thread downloaded it while we waited
                if (File.Exists(localPath))
                {
                    return localPath;
                }

                var wrappedUrl = SettingsManager.WrapUrlWithProxy(url);
                LoggerService.Info($"[Network] Fetching image from: {wrappedUrl}");
                var response = await _httpClient.GetAsync(wrappedUrl);
                response.EnsureSuccessStatusCode();
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                
                // Write to a temp file first, then move to avoid partial files
                string tempPath = localPath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, imageBytes);
                File.Move(tempPath, localPath, true);

                LoggerService.Info($"[Network] ✔ Successfully cached image: {url}");
                return localPath;
            }
            catch (Exception ex)
            {
                // Log or ignore, return null if failed
                System.Diagnostics.Debug.WriteLine($"Failed to cache image {url}: {ex.Message}");
                LoggerService.Error($"[Network] ❌ Failed to fetch image {url}", ex);
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    var files = Directory.GetFiles(CacheDirectory);
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        private static string GetHashString(string inputString)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
            {
                var hashBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }
    }
}
