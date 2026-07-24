using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class AnilistSearchResult
    {
        public int Id { get; set; }
        public string TitleRomaji { get; set; } = string.Empty;
        public string TitleEnglish { get; set; } = string.Empty;
        public string TitleNative { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public string BannerImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public int Episodes { get; set; }
        public string Season { get; set; } = string.Empty;
        public int SeasonYear { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Genres { get; set; } = string.Empty;
    }

    public class AnilistService
    {
        private static readonly HttpClient _httpClient;

        static AnilistService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<AnilistSearchResult?> SearchAnimeAsync(string title, string? year = null)
        {
            try
            {
                // Simple query to search by title
                var query = @"
                query ($search: String) {
                  Media (search: $search, type: ANIME, sort: SEARCH_MATCH) {
                    id
                    title {
                      romaji
                      english
                      native
                    }
                    coverImage {
                      extraLarge
                      large
                    }
                    bannerImage
                    description
                    averageScore
                    episodes
                    season
                    seasonYear
                    status
                    genres
                  }
                }";

                var requestBody = new
                {
                    query = query,
                    variables = new { search = title }
                };

                string jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                string url = "https://graphql.anilist.co";
                LoggerService.Info($"[AniList] ارسال درخواست جستجوی انیمه: {title} -> {url}");
                var response = await _httpClient.PostAsync(SettingsManager.WrapUrlWithProxy(url), content);
                
                if (response.IsSuccessStatusCode)
                {
                    LoggerService.Info($"[AniList] یافت شد: {title}");
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("data", out var data) && data.TryGetProperty("Media", out var media) && media.ValueKind == JsonValueKind.Object)
                    {
                        var result = new AnilistSearchResult();
                        
                        if (media.TryGetProperty("id", out var id))
                            result.Id = id.GetInt32();

                        if (media.TryGetProperty("title", out var titleObj))
                        {
                            if (titleObj.TryGetProperty("english", out var eng) && eng.ValueKind == JsonValueKind.String)
                                result.TitleEnglish = eng.GetString() ?? "";
                            if (titleObj.TryGetProperty("romaji", out var rom) && rom.ValueKind == JsonValueKind.String)
                                result.TitleRomaji = rom.GetString() ?? "";
                            if (titleObj.TryGetProperty("native", out var nat) && nat.ValueKind == JsonValueKind.String)
                                result.TitleNative = nat.GetString() ?? "";
                        }

                        if (media.TryGetProperty("coverImage", out var cover))
                        {
                            if (cover.TryGetProperty("extraLarge", out var el) && el.ValueKind == JsonValueKind.String)
                                result.CoverImageUrl = el.GetString() ?? "";
                            else if (cover.TryGetProperty("large", out var l) && l.ValueKind == JsonValueKind.String)
                                result.CoverImageUrl = l.GetString() ?? "";
                        }

                        if (media.TryGetProperty("bannerImage", out var banner) && banner.ValueKind == JsonValueKind.String)
                            result.BannerImageUrl = banner.GetString() ?? "";

                        if (media.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                            result.Description = desc.GetString() ?? "";

                        if (media.TryGetProperty("averageScore", out var score) && score.ValueKind == JsonValueKind.Number)
                            result.AverageScore = score.GetDouble() / 10.0; // Convert 100-based to 10-based

                        if (media.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Number)
                            result.Episodes = eps.GetInt32();

                        if (media.TryGetProperty("season", out var season) && season.ValueKind == JsonValueKind.String)
                            result.Season = season.GetString() ?? "";

                        if (media.TryGetProperty("seasonYear", out var sYear) && sYear.ValueKind == JsonValueKind.Number)
                            result.SeasonYear = sYear.GetInt32();

                        if (media.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
                            result.Status = status.GetString() ?? "";

                        if (media.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                        {
                            var genreList = new System.Collections.Generic.List<string>();
                            foreach (var g in genres.EnumerateArray())
                            {
                                if (g.ValueKind == JsonValueKind.String)
                                    genreList.Add(g.GetString()!);
                            }
                            result.Genres = string.Join("، ", genreList);
                        }

                        // Remove HTML tags from description
                        if (!string.IsNullOrEmpty(result.Description))
                        {
                            result.Description = System.Text.RegularExpressions.Regex.Replace(result.Description, "<.*?>", String.Empty);
                        }

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error($"AniList search failed for {title}", ex);
            }

            return null;
        }
    }
}

