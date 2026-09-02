using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class AniListAnimeDetails
    {
        public int Id { get; set; }
        public string TitleRomaji { get; set; } = string.Empty;
        public string TitleEnglish { get; set; } = string.Empty;
        public string TitleNative { get; set; } = string.Empty;
        public int? Episodes { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Status { get; set; }
        public int? AverageScore { get; set; }
        public string? Description { get; set; }
        public string? Genres { get; set; }
        public string? Studio { get; set; }
        public int? Year { get; set; }
        public int? SeasonYear => Year;
        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }

        public string? CoverImageUrl => PosterUrl;
        public string? BannerImageUrl => BannerUrl;

        public string PreferredTitle => !string.IsNullOrWhiteSpace(TitleEnglish) ? TitleEnglish : TitleRomaji;
    }

    public static class AniListService
    {
        private static readonly HttpClient _httpClient = new(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static AniListService()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieManagerDesktop/2.5");
            }
        }

        public static async Task<AniListAnimeDetails?> SearchAnimeAsync(string query, string? year = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            try
            {
                string cleanQuery = CleanAnimeTitle(query);
                if (string.IsNullOrWhiteSpace(cleanQuery)) return null;

                int? parsedYear = null;
                if (!string.IsNullOrWhiteSpace(year) && int.TryParse(year, out int y) && y > 1900)
                {
                    parsedYear = y;
                }

                string graphQlQuery = parsedYear.HasValue ? @"
                query ($search: String, $seasonYear: Int) {
                    Media(search: $search, seasonYear: $seasonYear, type: ANIME, sort: SEARCH_MATCH) {
                        id
                        title {
                            romaji
                            english
                            native
                        }
                        episodes
                        duration
                        status
                        averageScore
                        description(asHtml: false)
                        genres
                        studios(isMain: true) {
                            nodes {
                                name
                            }
                        }
                        startDate {
                            year
                        }
                        coverImage {
                            extraLarge
                            large
                        }
                        bannerImage
                    }
                }" : @"
                query ($search: String) {
                    Media(search: $search, type: ANIME, sort: SEARCH_MATCH) {
                        id
                        title {
                            romaji
                            english
                            native
                        }
                        episodes
                        duration
                        status
                        averageScore
                        description(asHtml: false)
                        genres
                        studios(isMain: true) {
                            nodes {
                                name
                            }
                        }
                        startDate {
                            year
                        }
                        coverImage {
                            extraLarge
                            large
                        }
                        bannerImage
                    }
                }";

                object payload = parsedYear.HasValue
                    ? new { query = graphQlQuery, variables = new { search = cleanQuery, seasonYear = parsedYear.Value } }
                    : new { query = graphQlQuery, variables = new { search = cleanQuery } };

                string jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(SettingsManager.WrapUrlWithProxy("https://graphql.anilist.co"), content, ct);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("data", out var data) && data.TryGetProperty("Media", out var media) && media.ValueKind == JsonValueKind.Object)
                    {
                        return ParseAnimeDetails(media);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[AniList] Error searching anime '{query}': {ex.Message}");
            }

            return null;
        }

        public static Task<AniListAnimeDetails?> SearchAnimeAsync(string query, CancellationToken ct) =>
            SearchAnimeAsync(query, null, ct);

        private static AniListAnimeDetails ParseAnimeDetails(JsonElement media)
        {
            var details = new AniListAnimeDetails();

            if (media.TryGetProperty("id", out var idProp))
                details.Id = idProp.GetInt32();

            if (media.TryGetProperty("title", out var tProp))
            {
                if (tProp.TryGetProperty("romaji", out var r) && r.ValueKind == JsonValueKind.String)
                    details.TitleRomaji = r.GetString() ?? "";
                if (tProp.TryGetProperty("english", out var e) && e.ValueKind == JsonValueKind.String)
                    details.TitleEnglish = e.GetString() ?? "";
                if (tProp.TryGetProperty("native", out var n) && n.ValueKind == JsonValueKind.String)
                    details.TitleNative = n.GetString() ?? "";
            }

            if (media.TryGetProperty("episodes", out var ep) && ep.ValueKind == JsonValueKind.Number)
                details.Episodes = ep.GetInt32();

            if (media.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number)
                details.DurationMinutes = dur.GetInt32();

            if (media.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String)
                details.Status = st.GetString();

            if (media.TryGetProperty("averageScore", out var sc) && sc.ValueKind == JsonValueKind.Number)
                details.AverageScore = sc.GetInt32();

            if (media.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                details.Description = StripHtml(desc.GetString());

            if (media.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array)
            {
                var glist = g.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                if (glist.Any())
                    details.Genres = string.Join("، ", glist);
            }

            if (media.TryGetProperty("studios", out var std) && std.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
            {
                var sList = nodes.EnumerateArray()
                    .Select(x => x.TryGetProperty("name", out var sn) ? sn.GetString() : null)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                if (sList.Any())
                    details.Studio = string.Join("، ", sList);
            }

            if (media.TryGetProperty("startDate", out var sd) && sd.TryGetProperty("year", out var yr) && yr.ValueKind == JsonValueKind.Number)
                details.Year = yr.GetInt32();

            if (media.TryGetProperty("coverImage", out var cov))
            {
                if (cov.TryGetProperty("extraLarge", out var xl) && xl.ValueKind == JsonValueKind.String)
                    details.PosterUrl = xl.GetString();
                else if (cov.TryGetProperty("large", out var lg) && lg.ValueKind == JsonValueKind.String)
                    details.PosterUrl = lg.GetString();
            }

            if (media.TryGetProperty("bannerImage", out var bi) && bi.ValueKind == JsonValueKind.String)
                details.BannerUrl = bi.GetString();

            return details;
        }

        private static string CleanAnimeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            string clean = Regex.Replace(title, @"\s*[\[\(]?(?:S\d+|Season\s*\d+|فصل\s*\d+)[\]\)]?.*", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\b(?:1080p|720p|480p|2160p|4k|bluray|web-dl|webrip|x264|x265|hevc)\b.*", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"[\.\[\]\(\)\-_]", " ");
            return Regex.Replace(clean, @"\s+", " ").Trim();
        }

        private static string StripHtml(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }
    }

    public class AnilistService
    {
        public Task<AniListAnimeDetails?> SearchAnimeAsync(string query, string? year = null, CancellationToken ct = default)
        {
            return AniListService.SearchAnimeAsync(query, year, ct);
        }
    }
}
