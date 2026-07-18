using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FolderIconManager.WPF.Services
{
    public class TmdbService
    {
        private readonly HttpClient _httpClient;
        private int _apiKeyIndex = 0;

        public TmdbService()
        {
            _httpClient = new HttpClient();
        }

        private string? GetNextApiKey()
        {
            var keys = SettingsService.Instance.CurrentSettings.TmdbApiKeys;
            if (keys == null || keys.Count == 0) return null;

            if (_apiKeyIndex >= keys.Count)
                _apiKeyIndex = 0;

            string key = keys[_apiKeyIndex];
            _apiKeyIndex++;
            return key;
        }

        public async Task<(string? Title, string? PosterPath, double? Rating)> SearchSeriesAsync(string query)
        {
            string? apiKey = GetNextApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("هیچ کلید API برای TMDB در تنظیمات ثبت نشده است.");

            string url = $"https://api.themoviedb.org/3/search/tv?api_key={apiKey}&query={Uri.EscapeDataString(query)}";

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("کلید API نامعتبر است (Unauthorized).");
                response.EnsureSuccessStatusCode();
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            var root = doc.RootElement;
            if (root.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var firstResult = results[0];
                string? title = firstResult.TryGetProperty("name", out var n) ? n.GetString() : null;
                title ??= firstResult.TryGetProperty("original_name", out var on) ? on.GetString() : null;
                
                string? posterPath = firstResult.TryGetProperty("poster_path", out var p) ? p.GetString() : null;
                double? rating = firstResult.TryGetProperty("vote_average", out var r) ? r.GetDouble() : null;

                return (title, posterPath, rating);
            }

            return (null, null, null);
        }

        public async Task<string> DownloadPosterAsync(string posterPath, string saveDirectory)
        {
            string url = $"https://image.tmdb.org/t/p/w500{posterPath}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
            
            string fileName = Path.GetFileName(posterPath.TrimStart('/'));
            if (string.IsNullOrEmpty(fileName)) fileName = Guid.NewGuid().ToString() + ".jpg";

            string savePath = Path.Combine(saveDirectory, fileName);
            await File.WriteAllBytesAsync(savePath, imageBytes);

            return savePath;
        }
    }
}
