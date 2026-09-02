using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class TvMazeShowInfo
    {
        public int? TvMazeId { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? AirDay { get; set; }
        public string? AirTime { get; set; }
        public string? NetworkName { get; set; }
        public string? NextEpisodeDate { get; set; }
        public string? NextEpisodeTime { get; set; }
        public int? NextEpisodeSeason { get; set; }
        public int? NextEpisodeNumber { get; set; }
        public string? NextEpisodeName { get; set; }
        public int? TotalSeasonsCount { get; set; }
        public int? TotalEpisodesCount { get; set; }
        public string? Summary { get; set; }
    }

    public class TvMazeSearchResult
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? PosterUrl { get; set; }
        public string? Summary { get; set; }
        public string? Genres { get; set; }
        public double AverageRating { get; set; }
        public string? Status { get; set; }
        public string? Premiered { get; set; }
        public string? Ended { get; set; }
        public string? Network { get; set; }
        public string? ScheduleDays { get; set; }
        public string? ScheduleTime { get; set; }
    }

    public class TvMazeService
    {
        private static readonly HttpClient _httpClient = new(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static TvMazeService()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieManagerDesktop/2.5");
            }
        }

        public async Task<TvMazeSearchResult?> SearchSeriesAsync(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            try
            {
                string cleanQuery = CleanSeriesTitle(query);
                if (string.IsNullOrWhiteSpace(cleanQuery)) return null;

                string url = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(cleanQuery)}";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var result = new TvMazeSearchResult();

                    if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        result.Id = idProp.GetInt32();

                    if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        result.Title = nameProp.GetString();

                    if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                        result.Status = statusProp.GetString();

                    if (root.TryGetProperty("premiered", out var premProp) && premProp.ValueKind == JsonValueKind.String)
                        result.Premiered = premProp.GetString();

                    if (root.TryGetProperty("ended", out var endProp) && endProp.ValueKind == JsonValueKind.String)
                        result.Ended = endProp.GetString();

                    if (root.TryGetProperty("summary", out var sumProp) && sumProp.ValueKind == JsonValueKind.String)
                        result.Summary = StripHtml(sumProp.GetString());

                    if (root.TryGetProperty("rating", out var ratProp) && ratProp.TryGetProperty("average", out var avgProp) && avgProp.ValueKind == JsonValueKind.Number)
                        result.AverageRating = avgProp.GetDouble();

                    if (root.TryGetProperty("image", out var imgProp) && imgProp.ValueKind == JsonValueKind.Object)
                    {
                        if (imgProp.TryGetProperty("medium", out var medImg) && medImg.ValueKind == JsonValueKind.String)
                            result.PosterUrl = medImg.GetString();
                        else if (imgProp.TryGetProperty("original", out var origImg) && origImg.ValueKind == JsonValueKind.String)
                            result.PosterUrl = origImg.GetString();
                    }

                    if (root.TryGetProperty("genres", out var genProp) && genProp.ValueKind == JsonValueKind.Array)
                    {
                        var gList = genProp.EnumerateArray().Select(g => g.GetString()).Where(g => !string.IsNullOrEmpty(g)).ToList();
                        if (gList.Any())
                            result.Genres = string.Join("، ", gList);
                    }

                    // Network or Web Channel
                    string? networkName = null;
                    if (root.TryGetProperty("network", out var netProp) && netProp.ValueKind == JsonValueKind.Object)
                    {
                        if (netProp.TryGetProperty("name", out var netName) && netName.ValueKind == JsonValueKind.String)
                            networkName = netName.GetString();
                    }
                    if (string.IsNullOrEmpty(networkName) && root.TryGetProperty("webChannel", out var webProp) && webProp.ValueKind == JsonValueKind.Object)
                    {
                        if (webProp.TryGetProperty("name", out var webName) && webName.ValueKind == JsonValueKind.String)
                            networkName = webName.GetString();
                    }
                    result.Network = networkName;

                    // Schedule
                    if (root.TryGetProperty("schedule", out var schedProp) && schedProp.ValueKind == JsonValueKind.Object)
                    {
                        if (schedProp.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                            result.ScheduleTime = timeProp.GetString();

                        if (schedProp.TryGetProperty("days", out var daysProp) && daysProp.ValueKind == JsonValueKind.Array)
                        {
                            var days = daysProp.EnumerateArray().Select(d => d.GetString()).Where(d => !string.IsNullOrEmpty(d)).ToList();
                            if (days.Any())
                                result.ScheduleDays = string.Join("، ", days);
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[TVMaze] خطا در جستجوی سریال {query}: {ex.Message}");
            }

            return null;
        }

        public static async Task<TvMazeShowInfo?> GetShowInfoAsync(string? imdbId, string? title, CancellationToken ct = default)
        {
            try
            {
                // 1. Try lookup by IMDb ID if available
                if (!string.IsNullOrWhiteSpace(imdbId) && imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    var showByImdb = await FetchByImdbAsync(imdbId.Trim(), ct);
                    if (showByImdb != null)
                    {
                        return showByImdb;
                    }
                }

                // 2. Fallback: Search by clean show title
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = CleanSeriesTitle(title);
                    if (!string.IsNullOrWhiteSpace(cleanTitle))
                    {
                        var showByTitle = await FetchByTitleAsync(cleanTitle, ct);
                        if (showByTitle != null)
                        {
                            return showByTitle;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[TVMaze] Error fetching show info for {title ?? imdbId}: {ex.Message}");
            }

            return null;
        }

        private static async Task<TvMazeShowInfo?> FetchByImdbAsync(string imdbId, CancellationToken ct)
        {
            try
            {
                string url = $"https://api.tvmaze.com/lookup/shows?imdb={Uri.EscapeDataString(imdbId)}";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url), ct);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    int showId = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                    if (showId > 0)
                    {
                        // Fetch with embedded next episode, seasons and episodes
                        return await FetchByIdWithEmbedAsync(showId, ct);
                    }

                    return ParseShowJson(root);
                }
            }
            catch { }
            return null;
        }

        private static async Task<TvMazeShowInfo?> FetchByTitleAsync(string title, CancellationToken ct)
        {
            try
            {
                string url = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(title)}&embed[]=nextepisode&embed[]=seasons&embed[]=episodes";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url), ct);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    return ParseShowJson(doc.RootElement);
                }
            }
            catch { }
            return null;
        }

        private static async Task<TvMazeShowInfo?> FetchByIdWithEmbedAsync(int showId, CancellationToken ct)
        {
            try
            {
                string url = $"https://api.tvmaze.com/shows/{showId}?embed[]=nextepisode&embed[]=seasons&embed[]=episodes";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url), ct);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    return ParseShowJson(doc.RootElement);
                }
            }
            catch { }
            return null;
        }

        private static TvMazeShowInfo ParseShowJson(JsonElement root)
        {
            var info = new TvMazeShowInfo();

            if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                info.TvMazeId = idProp.GetInt32();

            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                info.Name = nameProp.GetString();

            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                info.Status = statusProp.GetString();

            // Schedule (Days & Time)
            if (root.TryGetProperty("schedule", out var scheduleProp) && scheduleProp.ValueKind == JsonValueKind.Object)
            {
                if (scheduleProp.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                {
                    string? t = timeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(t))
                        info.AirTime = t;
                }

                if (scheduleProp.TryGetProperty("days", out var daysProp) && daysProp.ValueKind == JsonValueKind.Array)
                {
                    var daysList = daysProp.EnumerateArray()
                        .Select(d => d.GetString())
                        .Where(d => !string.IsNullOrEmpty(d))
                        .ToList();
                    if (daysList.Any())
                    {
                        info.AirDay = string.Join("، ", daysList);
                    }
                }
            }

            // Network or Web Channel (Streaming Platform)
            string? networkName = null;
            if (root.TryGetProperty("network", out var netProp) && netProp.ValueKind == JsonValueKind.Object)
            {
                if (netProp.TryGetProperty("name", out var netName) && netName.ValueKind == JsonValueKind.String)
                    networkName = netName.GetString();
            }
            if (string.IsNullOrEmpty(networkName) && root.TryGetProperty("webChannel", out var webProp) && webProp.ValueKind == JsonValueKind.Object)
            {
                if (webProp.TryGetProperty("name", out var webName) && webName.ValueKind == JsonValueKind.String)
                    networkName = webName.GetString();
            }
            info.NetworkName = networkName;

            // Summary
            if (root.TryGetProperty("summary", out var sumProp) && sumProp.ValueKind == JsonValueKind.String)
                info.Summary = StripHtml(sumProp.GetString());

            // Check _embedded for Next Episode, Seasons, and Episodes
            if (root.TryGetProperty("_embedded", out var embedded) && embedded.ValueKind == JsonValueKind.Object)
            {
                // 1. Next Episode
                if (embedded.TryGetProperty("nextepisode", out var nep) && nep.ValueKind == JsonValueKind.Object)
                {
                    if (nep.TryGetProperty("airdate", out var adProp) && adProp.ValueKind == JsonValueKind.String)
                        info.NextEpisodeDate = adProp.GetString();

                    if (nep.TryGetProperty("airtime", out var atProp) && atProp.ValueKind == JsonValueKind.String)
                    {
                        string? nt = atProp.GetString();
                        if (!string.IsNullOrWhiteSpace(nt))
                            info.NextEpisodeTime = nt;
                    }

                    if (nep.TryGetProperty("season", out var sProp) && sProp.ValueKind == JsonValueKind.Number)
                        info.NextEpisodeSeason = sProp.GetInt32();

                    if (nep.TryGetProperty("number", out var numProp) && numProp.ValueKind == JsonValueKind.Number)
                        info.NextEpisodeNumber = numProp.GetInt32();

                    if (nep.TryGetProperty("name", out var epNameProp) && epNameProp.ValueKind == JsonValueKind.String)
                        info.NextEpisodeName = epNameProp.GetString();
                }

                // 2. Seasons count
                if (embedded.TryGetProperty("seasons", out var seasonsProp) && seasonsProp.ValueKind == JsonValueKind.Array)
                {
                    var seasonElements = seasonsProp.EnumerateArray().ToList();
                    if (seasonElements.Any())
                    {
                        info.TotalSeasonsCount = seasonElements.Count;
                    }
                }

                // 3. Episodes count
                if (embedded.TryGetProperty("episodes", out var epsProp) && epsProp.ValueKind == JsonValueKind.Array)
                {
                    var epElements = epsProp.EnumerateArray().ToList();
                    if (epElements.Any())
                    {
                        info.TotalEpisodesCount = epElements.Count;
                    }
                }
            }

            return info;
        }

        private static string CleanSeriesTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            string clean = System.Text.RegularExpressions.Regex.Replace(title, @"\s*[\[\(]?(?:S\d+|Season\s*\d+|فصل\s*\d+)[\]\)]?.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\b(?:1080p|720p|480p|2160p|4k|bluray|web-dl|webrip|x264|x265|hevc)\b.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return clean.Trim();
        }

        private static string StripHtml(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }
    }
}
