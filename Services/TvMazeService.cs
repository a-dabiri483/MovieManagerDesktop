using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class TvMazeSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public string Genres { get; set; } = string.Empty;
        public string Network { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Premiered { get; set; } = string.Empty;
        public string Ended { get; set; } = string.Empty;
        public int Runtime { get; set; }
        public string ScheduleTime { get; set; } = string.Empty;
        public string ScheduleDays { get; set; } = string.Empty;
    }

    public class TvMazeService
    {
        private static readonly HttpClient _httpClient;

        static TvMazeService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<TvMazeSearchResult?> SearchSeriesAsync(string title)
        {
            try
            {
                string url = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(title)}";
                LoggerService.Info($"[TVMaze] جستجوی سریال: {title} -> {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                
                if (response.IsSuccessStatusCode)
                {
                    LoggerService.Info($"[TVMaze] یافت شد: {title}");
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var result = new TvMazeSearchResult();
                    
                    if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        result.Id = idProp.GetInt32();
                        
                    if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        result.Title = nameProp.GetString() ?? "";
                        
                    if (root.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.String)
                    {
                        var summary = summaryProp.GetString() ?? "";
                        result.Summary = Regex.Replace(summary, "<.*?>", String.Empty);
                    }
                        
                    if (root.TryGetProperty("image", out var imgProp) && imgProp.ValueKind == JsonValueKind.Object)
                    {
                        if (imgProp.TryGetProperty("original", out var origProp) && origProp.ValueKind == JsonValueKind.String)
                            result.PosterUrl = SettingsManager.WrapUrlWithProxy(origProp.GetString() ?? "");
                        else if (imgProp.TryGetProperty("medium", out var medProp) && medProp.ValueKind == JsonValueKind.String)
                            result.PosterUrl = SettingsManager.WrapUrlWithProxy(medProp.GetString() ?? "");
                    }
                    
                    if (root.TryGetProperty("rating", out var ratingProp) && ratingProp.ValueKind == JsonValueKind.Object)
                    {
                        if (ratingProp.TryGetProperty("average", out var avgProp) && avgProp.ValueKind == JsonValueKind.Number)
                            result.AverageRating = avgProp.GetDouble();
                    }
                    
                    if (root.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
                    {
                        var genres = new System.Collections.Generic.List<string>();
                        foreach (var g in genresProp.EnumerateArray())
                        {
                            if (g.ValueKind == JsonValueKind.String)
                                genres.Add(g.GetString()!);
                        }
                        result.Genres = string.Join("، ", genres);
                    }
                    
                    if (root.TryGetProperty("network", out var networkProp) && networkProp.ValueKind == JsonValueKind.Object)
                    {
                        if (networkProp.TryGetProperty("name", out var netName) && netName.ValueKind == JsonValueKind.String)
                            result.Network = netName.GetString() ?? "";
                    }
                    else if (root.TryGetProperty("webChannel", out var webProp) && webProp.ValueKind == JsonValueKind.Object)
                    {
                        if (webProp.TryGetProperty("name", out var webName) && webName.ValueKind == JsonValueKind.String)
                            result.Network = webName.GetString() ?? "";
                    }
                    
                    if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                        result.Status = statusProp.GetString() ?? "";
                        
                    if (root.TryGetProperty("premiered", out var premieredProp) && premieredProp.ValueKind == JsonValueKind.String)
                        result.Premiered = premieredProp.GetString() ?? "";
                        
                    if (root.TryGetProperty("ended", out var endedProp) && endedProp.ValueKind == JsonValueKind.String)
                        result.Ended = endedProp.GetString() ?? "";
                        
                    if (root.TryGetProperty("averageRuntime", out var runtimeProp) && runtimeProp.ValueKind == JsonValueKind.Number)
                        result.Runtime = runtimeProp.GetInt32();
                        
                    if (root.TryGetProperty("schedule", out var scheduleProp) && scheduleProp.ValueKind == JsonValueKind.Object)
                    {
                        if (scheduleProp.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
                            result.ScheduleTime = timeProp.GetString() ?? "";
                            
                        if (scheduleProp.TryGetProperty("days", out var daysProp) && daysProp.ValueKind == JsonValueKind.Array)
                        {
                            var days = new System.Collections.Generic.List<string>();
                            foreach (var d in daysProp.EnumerateArray())
                            {
                                if (d.ValueKind == JsonValueKind.String)
                                    days.Add(d.GetString()!);
                            }
                            result.ScheduleDays = string.Join("، ", days);
                        }
                    }
                        
                    return result;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"TVMaze search failed for {title}", ex);
            }
            
            return null;
        }
    }
}

