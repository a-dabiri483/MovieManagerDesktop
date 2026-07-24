using MovieManagerDesktop.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;

namespace MovieManagerDesktop.Services
{
    public class TmdbSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReleaseYear { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
    }

    public class IdentifyMediaService
    {
        private static readonly HttpClient _httpClient;
        private readonly string _imagesDirectory;
        
        private static readonly Dictionary<string, string> TmdbGenres = new Dictionary<string, string>
        {
            {"28", "اکشن"}, {"12", "ماجراجویی"}, {"16", "انیمیشن"}, {"35", "کمدی"}, {"80", "جنایی"}, 
            {"99", "مستند"}, {"18", "درام"}, {"10751", "خانوادگی"}, {"14", "فانتزی"}, {"36", "تاریخی"}, 
            {"27", "ترسناک"}, {"10402", "موسیقی"}, {"9648", "معمایی"}, {"10749", "عاشقانه"}, {"878", "علمی تخیلی"}, 
            {"10770", "فیلم تلویزیونی"}, {"53", "هیجان انگیز"}, {"10752", "جنگی"}, {"37", "وسترن"},
            {"10759", "اکشن ماجراجویی"}, {"10762", "کودکان"}, {"10765", "علمی تخیلی فانتزی"}, {"10768", "سیاسی جنگی"}
        };

        static IdentifyMediaService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(25);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieManagerDesktop/1.0");
        }

        public IdentifyMediaService()
        {
            

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _imagesDirectory = Path.Combine(appData, "CineTrack", "Images");
            if (!Directory.Exists(_imagesDirectory))
            {
                Directory.CreateDirectory(_imagesDirectory);
            }
        }

        public async Task<string?> DownloadImageAsync(string? url, string fileNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                // clean up title for filename
                var cleanPrefix = string.Join("_", fileNamePrefix.Split(Path.GetInvalidFileNameChars()));
                string ext = Path.GetExtension(url.Split('?')[0]);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                
                string fileName = $"{cleanPrefix}_{Guid.NewGuid().ToString("N").Substring(0,6)}{ext}";
                string filePath = Path.Combine(_imagesDirectory, fileName);
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, imageBytes);
                return filePath; // Return local path!
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error($"Failed to download image from {url}", ex);
                return null;
            }
        }

        public async Task<int?> GetTmdbIdFromImdbIdAsync(string imdbId)
        {
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = settings.TmdbApiKey ?? "3272e27041f0b0ee11dbaf0315ce5b21";
                string url = $"https://api.themoviedb.org/3/find/{imdbId}?api_key={apiKey}&external_source=imdb_id";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("movie_results", out var mr) && mr.GetArrayLength() > 0)
                        return mr[0].GetProperty("id").GetInt32();
                    if (root.TryGetProperty("tv_results", out var tr) && tr.GetArrayLength() > 0)
                        return tr[0].GetProperty("id").GetInt32();
                }
            }
            catch { }
            return null;
        }

        public async Task<List<TmdbSearchResult>> SearchMediaAsync(string query, string language = "en-US")
        {
            var settings = SettingsManager.LoadSettings();
            string source = settings.SelectedDataSource ?? "FM_DB";
            
            if (source == "FM_DB")
                return await SearchFmDbAsync(query);
            if (source == "OMDB_ONLY")
                return await SearchOmdbAsync(query, settings.OmdbApiKey);
                
            return await SearchTmdbInternalAsync(query, language);
        }

        private async Task<List<TmdbSearchResult>> SearchFmDbAsync(string query)
        {
            var results = new List<TmdbSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;
            try
            {
                string url = $"https://imdb.iamidiotareyoutoo.com/search?q={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var fmdbResponse = JsonSerializer.Deserialize<FmDbResponse>(json);
                    if (fmdbResponse != null && fmdbResponse.Ok && fmdbResponse.Description != null)
                    {
                        foreach (var item in fmdbResponse.Description)
                        {
                            int tmdbId = 0;
                            if (!string.IsNullOrWhiteSpace(item.ImdbId))
                            {
                                int? realTmdbId = await GetTmdbIdFromImdbIdAsync(item.ImdbId);
                                if (realTmdbId.HasValue) tmdbId = realTmdbId.Value;
                            }
                            results.Add(new TmdbSearchResult
                            {
                                Id = tmdbId,
                                Title = item.Title ?? "",
                                ReleaseYear = item.Year?.ToString() ?? "",
                                PosterUrl = item.ImgPoster ?? "",
                                MediaType = "movie"
                            });
                        }
                    }
                }
            }
            catch { }
            return results;
        }

        private async Task<List<TmdbSearchResult>> SearchOmdbAsync(string query, string apiKey)
        {
            var results = new List<TmdbSearchResult>();
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(apiKey)) return results;
            try
            {
                string url = $"https://www.omdbapi.com/?apikey={apiKey}&s={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Search", out var searchResults) && searchResults.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in searchResults.EnumerateArray())
                        {
                            int tmdbId = 0;
                            if (item.TryGetProperty("imdbID", out var imdbProp) && imdbProp.GetString() != "N/A")
                            {
                                int? realTmdbId = await GetTmdbIdFromImdbIdAsync(imdbProp.GetString());
                                if (realTmdbId.HasValue) tmdbId = realTmdbId.Value;
                            }
                            string title = item.TryGetProperty("Title", out var tProp) ? tProp.GetString() ?? "" : "";
                            string year = item.TryGetProperty("Year", out var yProp) ? yProp.GetString() ?? "" : "";
                            string poster = item.TryGetProperty("Poster", out var pProp) && pProp.GetString() != "N/A" ? pProp.GetString() ?? "" : "";
                            
                            results.Add(new TmdbSearchResult
                            {
                                Id = tmdbId,
                                Title = title,
                                ReleaseYear = year,
                                PosterUrl = poster,
                                MediaType = "movie"
                            });
                        }
                    }
                }
            }
            catch { }
            return results;
        }

        private async Task<List<TmdbSearchResult>> SearchTmdbInternalAsync(string query, string language = "en-US")
        {
            var results = new List<TmdbSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = settings.TmdbApiKey ?? "3272e27041f0b0ee11dbaf0315ce5b21";
                string encodedQuery = Uri.EscapeDataString(query);
                
                string url;
                if (query.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    // It's an IMDB ID, find Tmdb ID first
                    int? tmdbId = await GetTmdbIdFromImdbIdAsync(query);
                    if (tmdbId.HasValue)
                    {
                        // We need to fetch details to get a TmdbSearchResult, just fetch as movie and tv
                        // but actually, we can just return one result. Let's do a multi search if it's not tt
                        url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}&language={language}";
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode)
                        {
                            url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language={language}";
                            response = await _httpClient.GetAsync(url);
                        }
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var res = new TmdbSearchResult { Id = tmdbId.Value };
                            if (root.TryGetProperty("title", out var titleProp)) res.Title = titleProp.GetString() ?? "";
                            else if (root.TryGetProperty("name", out var nameProp)) res.Title = nameProp.GetString() ?? "";
                            
                            if (root.TryGetProperty("release_date", out var rd) && !string.IsNullOrEmpty(rd.GetString()))
                                res.ReleaseYear = rd.GetString()!.Substring(0, 4);
                            else if (root.TryGetProperty("first_air_date", out var fad) && !string.IsNullOrEmpty(fad.GetString()))
                                res.ReleaseYear = fad.GetString()!.Substring(0, 4);

                            if (root.TryGetProperty("poster_path", out var pp) && pp.ValueKind == JsonValueKind.String)
                                res.PosterUrl = $"https://image.tmdb.org/t/p/w92{pp.GetString()}";

                            results.Add(res);
                            return results;
                        }
                    }
                }
                else if (int.TryParse(query, out int tmdbId))
                {
                    // Direct ID lookup
                    url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}&language={language}";
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language={language}";
                        response = await _httpClient.GetAsync(url);
                    }
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var res = new TmdbSearchResult { Id = tmdbId };
                        if (root.TryGetProperty("title", out var titleProp)) res.Title = titleProp.GetString() ?? "";
                        else if (root.TryGetProperty("name", out var nameProp)) res.Title = nameProp.GetString() ?? "";
                        
                        if (root.TryGetProperty("release_date", out var rd) && !string.IsNullOrEmpty(rd.GetString()))
                            res.ReleaseYear = rd.GetString()!.Substring(0, 4);
                        else if (root.TryGetProperty("first_air_date", out var fad) && !string.IsNullOrEmpty(fad.GetString()))
                            res.ReleaseYear = fad.GetString()!.Substring(0, 4);

                        if (root.TryGetProperty("poster_path", out var pp) && pp.ValueKind == JsonValueKind.String)
                            res.PosterUrl = $"https://image.tmdb.org/t/p/w92{pp.GetString()}";

                        results.Add(res);
                        return results;
                    }
                }

                url = $"https://api.themoviedb.org/3/search/multi?api_key={apiKey}&query={encodedQuery}&language={language}";
                var resp = await _httpClient.GetAsync(url);
                
                if (!resp.IsSuccessStatusCode && language == "en-US")
                {
                     url = url.Replace("language=en-US", "language=fa-IR");
                     resp = await _httpClient.GetAsync(url);
                }

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("results", out var resArray) && resArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in resArray.EnumerateArray())
                        {
                            var res = new TmdbSearchResult();
                            if (item.TryGetProperty("id", out var id)) res.Id = id.GetInt32();
                            
                            if (item.TryGetProperty("title", out var titleProp)) res.Title = titleProp.GetString() ?? "";
                            else if (item.TryGetProperty("name", out var nameProp)) res.Title = nameProp.GetString() ?? "";
                            
                            if (item.TryGetProperty("media_type", out var typeProp)) res.MediaType = typeProp.GetString() ?? "";
                            if (res.MediaType == "person") continue; // skip actors

                            if (item.TryGetProperty("release_date", out var rd) && rd.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(rd.GetString()))
                                res.ReleaseYear = rd.GetString()!.Substring(0, 4);
                            else if (item.TryGetProperty("first_air_date", out var fad) && fad.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(fad.GetString()))
                                res.ReleaseYear = fad.GetString()!.Substring(0, 4);

                            if (item.TryGetProperty("poster_path", out var pp) && pp.ValueKind == JsonValueKind.String)
                                res.PosterUrl = $"https://image.tmdb.org/t/p/w92{pp.GetString()}";
                                
                            results.Add(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
            }
            return results;
        }

        public async Task<VideoFile> IdentifyMediaAsync(VideoFile file)
        {
            if (string.IsNullOrWhiteSpace(file.FormattedTitle))
                return file;

            try
            {
                var settings = SettingsManager.LoadSettings();
                string source = settings.SelectedDataSource ?? "FM_DB";
                string language = settings.TmdbLanguage ?? "fa-IR";

                if (file.TmdbId.HasValue && file.TmdbId > 0)
                {
                    await IdentifyWithTmdb(file, settings.TmdbApiKey, language);
                }
                else if (source == "FM_DB")
                {
                    await IdentifyWithFmDb(file);
                    
                    if (file.TmdbId.HasValue && file.TmdbId > 0)
                    {
                        // FM-DB found the TmdbId, now let's fetch the full details and high-quality TMDB poster
                        await IdentifyWithTmdb(file, settings.TmdbApiKey, language);
                    }
                }
                else if (source == "TMDB_ONLY")
                {
                    await IdentifyWithTmdb(file, settings.TmdbApiKey, language);
                }
                else if (source == "OMDB_ONLY")
                {
                    await IdentifyWithOmdb(file, settings.OmdbApiKey);
                }

                if (file.MediaType == "Series")
                {
                    bool isAnime = false;
                    bool primaryFailed = string.IsNullOrWhiteSpace(file.PosterUrl) && string.IsNullOrWhiteSpace(file.Overview);
                    
                    if (!string.IsNullOrWhiteSpace(file.Genres) && file.Genres.Contains("Animation", StringComparison.OrdinalIgnoreCase))
                    {
                        isAnime = true;
                    }
                    
                    if (primaryFailed || isAnime)
                    {
                        await IdentifyWithJikanAsync(file);
                    }
                }
                
                // Fallback for backdrop if missing
                if (string.IsNullOrWhiteSpace(file.BackdropUrl) && !string.IsNullOrWhiteSpace(file.PosterUrl))
                {
                    file.BackdropUrl = file.PosterUrl;
                }

                // Download images after identification
                if (!string.IsNullOrWhiteSpace(file.PosterUrl) && file.PosterUrl.StartsWith("http"))
                {
                    file.PosterUrl = await DownloadImageAsync(file.PosterUrl, file.FormattedTitle + "_poster");
                }
                if (!string.IsNullOrWhiteSpace(file.BackdropUrl) && file.BackdropUrl.StartsWith("http"))
                {
                    file.BackdropUrl = await DownloadImageAsync(file.BackdropUrl, file.FormattedTitle + "_backdrop");
                }
                
                return file;
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error($"Error identifying {file.FileName}", ex);
                Console.WriteLine($"Error identifying {file.FileName}: {ex.Message}");
                return file;
            }
        }

        private async Task IdentifyWithFmDb(VideoFile file)
        {
            string query = file.FormattedTitle;
            string url = $"https://imdb.iamidiotareyoutoo.com/search?q={Uri.EscapeDataString(query)}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var fmdbResponse = JsonSerializer.Deserialize<FmDbResponse>(json);
                if (fmdbResponse != null && fmdbResponse.Ok && fmdbResponse.Description != null && fmdbResponse.Description.Any())
                {
                    var firstMatch = fmdbResponse.Description.First();
                    if (!string.IsNullOrWhiteSpace(firstMatch.ImdbId))
                    {
                        int? realTmdbId = await GetTmdbIdFromImdbIdAsync(firstMatch.ImdbId);
                        if (realTmdbId.HasValue) file.TmdbId = realTmdbId.Value;
                    }
                    
                    file.PosterUrl = firstMatch.ImgPoster;
                    
                    if (firstMatch.Year.HasValue)
                        file.Year = firstMatch.Year.Value.ToString();
                        
                    file.Actors = firstMatch.Actors;
                    // Do NOT mutate MediaType because it causes Series to split into Movie + Series
                }
            }
        }

        private async Task IdentifyWithTmdb(VideoFile file, string apiKey, string language)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = "3272e27041f0b0ee11dbaf0315ce5b21"; // Sample key
            
            string query = Uri.EscapeDataString(file.FormattedTitle);
            string url = string.Empty;
            bool isDirectIdLookup = false;

            if (file.TmdbId.HasValue && file.TmdbId > 0)
            {
                isDirectIdLookup = true;
                string typeStr = file.MediaType == "Series" ? "tv" : "movie";
                url = $"https://api.themoviedb.org/3/{typeStr}/{file.TmdbId}?api_key={apiKey}&language={language}";
            }
            else
            {
                string typeStr = file.MediaType == "Series" ? "tv" : "movie";
                if (file.MediaType == "Series")
                {
                    if (!string.IsNullOrWhiteSpace(file.Year))
                        url = $"https://api.themoviedb.org/3/search/tv?api_key={apiKey}&query={query}&language={language}&first_air_date_year={file.Year}";
                    else
                        url = $"https://api.themoviedb.org/3/search/tv?api_key={apiKey}&query={query}&language={language}";
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(file.Year))
                        url = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={query}&language={language}&primary_release_year={file.Year}";
                    else
                        url = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={query}&language={language}";
                }
            }

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode && language == "fa-IR")
            {
                 url = url.Replace("language=fa-IR", "language=en-US");
                 response = await _httpClient.GetAsync(url);
            }

            if (!response.IsSuccessStatusCode && isDirectIdLookup)
            {
                // Direct ID lookup failed (e.g. FM-DB gave a Movie ID for a Series)
                // Fall back to title search
                isDirectIdLookup = false;
                file.TmdbId = null;
                
                if (file.MediaType == "Series")
                {
                    if (!string.IsNullOrWhiteSpace(file.Year))
                        url = $"https://api.themoviedb.org/3/search/tv?api_key={apiKey}&query={query}&language={language}&first_air_date_year={file.Year}";
                    else
                        url = $"https://api.themoviedb.org/3/search/tv?api_key={apiKey}&query={query}&language={language}";
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(file.Year))
                        url = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={query}&language={language}&primary_release_year={file.Year}";
                    else
                        url = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={query}&language={language}";
                }
                
                response = await _httpClient.GetAsync(url);
            }

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                
                if (!isDirectIdLookup && root.TryGetProperty("results", out var results) && results.GetArrayLength() == 0 && !string.IsNullOrWhiteSpace(file.Year))
                {
                    // Fallback: try searching without year
                    string fallbackType = file.MediaType == "Series" ? "tv" : "movie";
                    string fallbackUrl = $"https://api.themoviedb.org/3/search/{fallbackType}?api_key={apiKey}&query={query}&language={language}";
                    response = await _httpClient.GetAsync(fallbackUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        json = await response.Content.ReadAsStringAsync();
                        using var fallbackDoc = JsonDocument.Parse(json);
                        root = fallbackDoc.RootElement.Clone();
                    }
                }
                
                JsonElement firstMatch = default;
                bool hasMatch = false;

                if (isDirectIdLookup)
                {
                    firstMatch = root;
                    hasMatch = true;
                }
                else if (root.TryGetProperty("results", out results) && results.GetArrayLength() > 0)
                {
                    firstMatch = results[0];
                    hasMatch = true;
                }
                
                // If still no match and we were using fa-IR, fallback to en-US
                if (!hasMatch && !isDirectIdLookup && language == "fa-IR")
                {
                    string enUrl = url.Replace("language=fa-IR", "language=en-US");
                    var enResponse = await _httpClient.GetAsync(enUrl);
                    if (enResponse.IsSuccessStatusCode)
                    {
                        var enJson = await enResponse.Content.ReadAsStringAsync();
                        using var enDoc = JsonDocument.Parse(enJson);
                        var enRoot = enDoc.RootElement;
                        
                        // Try without year if year was provided and failed
                        if (enRoot.TryGetProperty("results", out var enRes) && enRes.GetArrayLength() == 0 && !string.IsNullOrWhiteSpace(file.Year))
                        {
                            string enFallbackUrl = $"https://api.themoviedb.org/3/search/multi?api_key={apiKey}&query={query}&language=en-US";
                            var enFbResp = await _httpClient.GetAsync(enFallbackUrl);
                            if (enFbResp.IsSuccessStatusCode)
                            {
                                enJson = await enFbResp.Content.ReadAsStringAsync();
                                using var enFbDoc = JsonDocument.Parse(enJson);
                                enRoot = enFbDoc.RootElement.Clone();
                            }
                        }
                        
                        if (enRoot.TryGetProperty("results", out enRes) && enRes.GetArrayLength() > 0)
                        {
                            firstMatch = enRes[0];
                            hasMatch = true;
                            root = enRoot.Clone(); // update root for following logic
                        }
                    }
                }

                if (hasMatch)
                {
                    if (firstMatch.TryGetProperty("poster_path", out var posterPath) && posterPath.ValueKind == JsonValueKind.String)
                    {
                        var path = posterPath.GetString();
                        if (!string.IsNullOrEmpty(path))
                            file.PosterUrl = $"https://image.tmdb.org/t/p/w500{path}";
                    }
                    if (firstMatch.TryGetProperty("backdrop_path", out var backdropPath) && backdropPath.ValueKind == JsonValueKind.String)
                    {
                        var path = backdropPath.GetString();
                        if (!string.IsNullOrEmpty(path))
                            file.BackdropUrl = $"https://image.tmdb.org/t/p/original{path}";
                    }
                    
                    if (firstMatch.TryGetProperty("genre_ids", out var genreIds) && genreIds.ValueKind == JsonValueKind.Array)
                    {
                        var ids = genreIds.EnumerateArray().Select(g => g.GetInt32().ToString()).ToList();
                        if (language == "fa-IR") 
                        {
                            var persianGenres = ids.Select(id => TmdbGenres.ContainsKey(id) ? TmdbGenres[id] : id).ToList();
                            file.Genres = string.Join("، ", persianGenres);
                        }
                    }
                    
                    if (firstMatch.TryGetProperty("overview", out var overview) && overview.ValueKind == JsonValueKind.String)
                    {
                        file.Overview = overview.GetString();
                    }
                    if (firstMatch.TryGetProperty("vote_average", out var rating) && rating.ValueKind == JsonValueKind.Number)
                    {
                        file.Rating = Math.Round(rating.GetDouble(), 1);
                    }
                    
                    int tmdbId = 0;
                    if (firstMatch.TryGetProperty("id", out var id))
                    {
                        tmdbId = id.GetInt32();
                        file.TmdbId = tmdbId;
                    }
                    
                    string mediaType = "movie";
                    if (firstMatch.TryGetProperty("media_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                    {
                        mediaType = typeProp.GetString();
                    }
                    
                    // Do NOT mutate MediaType because it causes Series to split into Movie + Series

                    if (tmdbId > 0)
                    {
                        try
                        {
                            string detailsUrl = $"https://api.themoviedb.org/3/{mediaType}/{tmdbId}?api_key={apiKey}&append_to_response=credits&language={language}";
                            var detailsResp = await _httpClient.GetAsync(detailsUrl);
                            if (detailsResp.IsSuccessStatusCode)
                            {
                                var detailsJson = await detailsResp.Content.ReadAsStringAsync();
                                using var detailsDoc = JsonDocument.Parse(detailsJson);
                                var detailsRoot = detailsDoc.RootElement;
                                
                                if (string.IsNullOrWhiteSpace(file.Overview) && detailsRoot.TryGetProperty("overview", out var detailsOverview))
                                {
                                    file.Overview = detailsOverview.GetString();
                                }
                                
                                if (detailsRoot.TryGetProperty("credits", out var credits) && credits.TryGetProperty("cast", out var cast) && cast.ValueKind == JsonValueKind.Array)
                                {
                                    var actors = cast.EnumerateArray()
                                        .Take(8)
                                        .Select(a => a.TryGetProperty("name", out var name) ? name.GetString() : "")
                                        .Where(n => !string.IsNullOrEmpty(n))
                                        .ToList();
                                    if (actors.Any())
                                    {
                                        file.Actors = string.Join("، ", actors);
                                    }
                                }
                                
                                if (detailsRoot.TryGetProperty("belongs_to_collection", out var collection) && collection.ValueKind != JsonValueKind.Null)
                                {
                                    if (collection.TryGetProperty("name", out var collectionName) && collectionName.ValueKind == JsonValueKind.String)
                                    {
                                        file.CollectionName = collectionName.GetString();
                                    }
                                }
                                
                                if (detailsRoot.TryGetProperty("credits", out var crewCredits) && crewCredits.TryGetProperty("crew", out var crew) && crew.ValueKind == JsonValueKind.Array)
                                {
                                    var directors = crew.EnumerateArray()
                                        .Where(c => c.TryGetProperty("job", out var job) && job.GetString() == "Director")
                                        .Select(c => c.TryGetProperty("name", out var name) ? name.GetString() : "")
                                        .Where(n => !string.IsNullOrEmpty(n))
                                        .ToList();
                                    if (directors.Any())
                                    {
                                        file.Director = string.Join("، ", directors);
                                    }
                                }
                                
                                // Fallback to English for missing fields if language is Persian
                                if (language == "fa-IR" && (string.IsNullOrWhiteSpace(file.Overview) || string.IsNullOrWhiteSpace(file.Actors) || string.IsNullOrWhiteSpace(file.Director) || string.IsNullOrWhiteSpace(file.PosterUrl) || string.IsNullOrWhiteSpace(file.BackdropUrl)))
                                {
                                    string enDetailsUrl = $"https://api.themoviedb.org/3/{mediaType}/{tmdbId}?api_key={apiKey}&append_to_response=credits&language=en-US";
                                    var enDetailsResp = await _httpClient.GetAsync(enDetailsUrl);
                                    if (enDetailsResp.IsSuccessStatusCode)
                                    {
                                        var enDetailsJson = await enDetailsResp.Content.ReadAsStringAsync();
                                        using var enDetailsDoc = JsonDocument.Parse(enDetailsJson);
                                        var enDetailsRoot = enDetailsDoc.RootElement;
                                        
                                        if (string.IsNullOrWhiteSpace(file.Overview) && enDetailsRoot.TryGetProperty("overview", out var enOverview) && enOverview.ValueKind == JsonValueKind.String)
                                        {
                                            file.Overview = enOverview.GetString();
                                        }
                                            
                                        if (string.IsNullOrWhiteSpace(file.Actors) && enDetailsRoot.TryGetProperty("credits", out var enCredits) && enCredits.TryGetProperty("cast", out var enCast) && enCast.ValueKind == JsonValueKind.Array)
                                        {
                                            var enActors = enCast.EnumerateArray().Take(8).Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : "").Where(n => !string.IsNullOrEmpty(n)).ToList();
                                            if (enActors.Any()) file.Actors = string.Join("، ", enActors);
                                        }
                                        
                                        if (string.IsNullOrWhiteSpace(file.Director) && enDetailsRoot.TryGetProperty("credits", out var enCrewCredits) && enCrewCredits.TryGetProperty("crew", out var enCrew) && enCrew.ValueKind == JsonValueKind.Array)
                                        {
                                            var enDirectors = enCrew.EnumerateArray().Where(c => c.TryGetProperty("job", out var j) && j.GetString() == "Director").Select(c => c.TryGetProperty("name", out var n) ? n.GetString() : "").Where(n => !string.IsNullOrEmpty(n)).ToList();
                                            if (enDirectors.Any()) file.Director = string.Join("، ", enDirectors);
                                        }

                                        if (string.IsNullOrWhiteSpace(file.PosterUrl) && enDetailsRoot.TryGetProperty("poster_path", out var enPosterPath) && enPosterPath.ValueKind == JsonValueKind.String)
                                        {
                                            var path = enPosterPath.GetString();
                                            if (!string.IsNullOrEmpty(path))
                                                file.PosterUrl = $"https://image.tmdb.org/t/p/w500{path}";
                                        }
                                        
                                        if (string.IsNullOrWhiteSpace(file.BackdropUrl) && enDetailsRoot.TryGetProperty("backdrop_path", out var enBackdropPath) && enBackdropPath.ValueKind == JsonValueKind.String)
                                        {
                                            var path = enBackdropPath.GetString();
                                            if (!string.IsNullOrEmpty(path))
                                                file.BackdropUrl = $"https://image.tmdb.org/t/p/original{path}";
                                        }
                                    }
                                }
                            }
                            if (mediaType == "tv")
                            {
                                await IdentifySeriesDetailsAsync(file, apiKey, language);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        public async Task IdentifySeriesDetailsAsync(VideoFile file, string apiKey, string language)
        {
            if (!file.TmdbId.HasValue || file.TmdbId.Value <= 0) return;
            
            try
            {
                string url = $"https://api.themoviedb.org/3/tv/{file.TmdbId}?api_key={apiKey}&language={language}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode && language == "fa-IR")
                {
                    url = $"https://api.themoviedb.org/3/tv/{file.TmdbId}?api_key={apiKey}&language=en-US";
                    response = await _httpClient.GetAsync(url);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    // First Air Date
                    if (root.TryGetProperty("first_air_date", out var firstAirDate) && firstAirDate.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(firstAirDate.GetString(), out var date))
                            file.FirstAirDate = date;
                    }
                    
                    // Last Air Date
                    if (root.TryGetProperty("last_air_date", out var lastAirDate) && lastAirDate.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(lastAirDate.GetString(), out var date))
                            file.LastAirDate = date;
                    }
                    
                    // Status
                    if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
                    {
                        file.SeriesStatus = status.GetString();
                    }
                    
                    // Networks
                    if (root.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Array)
                    {
                        var networkList = networks.EnumerateArray()
                            .Select(n => n.TryGetProperty("name", out var name) ? name.GetString() : "")
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList();
                        if (networkList.Any())
                            file.NetworkName = string.Join("، ", networkList);
                    }
                    
                    // Total Seasons
                    if (root.TryGetProperty("number_of_seasons", out var seasons) && seasons.ValueKind == JsonValueKind.Number)
                    {
                        file.TotalSeasonsCount = seasons.GetInt32();
                    }
                    
                    // Total Episodes
                    if (root.TryGetProperty("number_of_episodes", out var episodes) && episodes.ValueKind == JsonValueKind.Number)
                    {
                        file.TotalEpisodesCount = episodes.GetInt32();
                    }
                    
                    // Next Episode to Air
                    if (root.TryGetProperty("next_episode_to_air", out var nextEp) && nextEp.ValueKind != JsonValueKind.Null)
                    {
                        if (nextEp.TryGetProperty("air_date", out var airDate) && airDate.ValueKind == JsonValueKind.String)
                        {
                            file.NextEpisodeDate = airDate.GetString();
                        }
                        if (nextEp.TryGetProperty("episode_number", out var epNum) && epNum.ValueKind == JsonValueKind.Number)
                        {
                            file.NextEpisodeNumber = epNum.GetInt32();
                        }
                    }
                    
                    // Seasons details (for air day/time)
                    if (root.TryGetProperty("seasons", out var seasonsArray) && seasonsArray.ValueKind == JsonValueKind.Array)
                    {
                        var seasonList = seasonsArray.EnumerateArray().ToList();
                        if (seasonList.Any())
                        {
                            var firstSeasonId = seasonList[0].TryGetProperty("season_number", out var sn) && sn.ValueKind == JsonValueKind.Number ? sn.GetInt32() : -1;
                            // usually season 1 has the regular episodes
                            var seasonToFetch = firstSeasonId == 0 && seasonList.Count > 1 ? 1 : firstSeasonId;
                            
                            if (seasonToFetch >= 0)
                            {
                                string seasonUrl = $"https://api.themoviedb.org/3/tv/{file.TmdbId}/season/{seasonToFetch}?api_key={apiKey}&language={language}";
                                var seasonResp = await _httpClient.GetAsync(seasonUrl);
                                if (seasonResp.IsSuccessStatusCode)
                                {
                                    var seasonJson = await seasonResp.Content.ReadAsStringAsync();
                                    using var seasonDoc = JsonDocument.Parse(seasonJson);
                                    var seasonRoot = seasonDoc.RootElement;
                                    
                                    if (seasonRoot.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
                                    {
                                        var epList = eps.EnumerateArray().ToList();
                                        if (epList.Any())
                                        {
                                            if (epList[0].TryGetProperty("air_date", out var epAirDate) && epAirDate.ValueKind == JsonValueKind.String)
                                            {
                                                var airDateStr = epAirDate.GetString();
                                                if (!string.IsNullOrEmpty(airDateStr) && DateTime.TryParse(airDateStr, out var epDate))
                                                {
                                                    file.AirDay = epDate.DayOfWeek.ToString();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore errors to not break the flow
            }
        }
        
        private async Task IdentifyWithOmdb(VideoFile file, string apiKey)
        {
            string query = Uri.EscapeDataString(file.FormattedTitle);
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            string url = $"https://www.omdbapi.com/?apikey={apiKey}&t={query}";
            if (!string.IsNullOrWhiteSpace(file.Year))
            {
                url += $"&y={file.Year}";
            }

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "True")
                {
                    if (root.TryGetProperty("Poster", out var poster) && poster.GetString() != "N/A")
                        file.PosterUrl = poster.GetString();
                        
                    if (root.TryGetProperty("imdbRating", out var rating) && rating.GetString() != "N/A" && double.TryParse(rating.GetString(), out double parsedRating))
                        file.Rating = parsedRating;
                        
                    if (root.TryGetProperty("Plot", out var plot) && plot.GetString() != "N/A")
                        file.Overview = plot.GetString();
                        
                    if (root.TryGetProperty("Actors", out var actors) && actors.GetString() != "N/A")
                        file.Actors = actors.GetString();
                        
                    if (root.TryGetProperty("Genre", out var genre) && genre.GetString() != "N/A")
                        file.Genres = genre.GetString();

                    if (root.TryGetProperty("imdbID", out var imdbProp) && imdbProp.GetString() != "N/A")
                    {
                        int? realTmdbId = await GetTmdbIdFromImdbIdAsync(imdbProp.GetString());
                        if (realTmdbId.HasValue) file.TmdbId = realTmdbId.Value;
                    }

                    if (root.TryGetProperty("Type", out var type) && type.GetString() != "N/A")
                    {
                        var t = type.GetString();
                        // Do NOT mutate file.MediaType
                    }
                }
            }
        }
        public async Task<VideoFile> UpdateSeriesStatusAsync(VideoFile file)
        {
            if (file.MediaType != "Series" || !file.TmdbId.HasValue) return file;

            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = settings.TmdbApiKey ?? "3272e27041f0b0ee11dbaf0315ce5b21";
                string url = $"https://api.themoviedb.org/3/tv/{file.TmdbId.Value}?api_key={apiKey}&language=fa-IR";
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    url = $"https://api.themoviedb.org/3/tv/{file.TmdbId.Value}?api_key={apiKey}&language=en-US";
                    response = await _httpClient.GetAsync(url);
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var statusProp))
                    {
                        string status = statusProp.GetString() ?? "";
                        file.SeriesStatus = status switch
                        {
                            "Ended" => "پایان یافته",
                            "Returning Series" => "در حال پخش",
                            "Canceled" => "لغو شده",
                            "In Production" => "در دست ساخت",
                            _ => status
                        };
                    }

                    if (root.TryGetProperty("number_of_seasons", out var seasonsProp))
                    {
                        file.NumberOfSeasons = seasonsProp.GetInt32();
                    }

                    if (root.TryGetProperty("number_of_episodes", out var episodesProp))
                    {
                        file.NumberOfEpisodes = episodesProp.GetInt32();
                    }

                    if (root.TryGetProperty("last_episode_to_air", out var lastEpisodeProp) && lastEpisodeProp.ValueKind == JsonValueKind.Object)
                    {
                        if (lastEpisodeProp.TryGetProperty("season_number", out var snProp))
                        {
                            file.LastAiredSeason = snProp.GetInt32();
                        }
                    }

                    if (root.TryGetProperty("next_episode_to_air", out var nextEpisodeProp) && nextEpisodeProp.ValueKind == JsonValueKind.Object)
                    {
                        if (nextEpisodeProp.TryGetProperty("air_date", out var airDateProp) && airDateProp.ValueKind == JsonValueKind.String)
                        {
                            file.NextEpisodeDate = airDateProp.GetString();
                        }
                        if (nextEpisodeProp.TryGetProperty("season_number", out var nsnProp))
                        {
                            file.NextEpisodeSeason = nsnProp.GetInt32();
                        }
                        if (nextEpisodeProp.TryGetProperty("episode_number", out var nenProp))
                        {
                            file.NextEpisodeNumber = nenProp.GetInt32();
                        }
                    }
                    else
                    {
                        file.NextEpisodeDate = null;
                        file.NextEpisodeSeason = null;
                        file.NextEpisodeNumber = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating series status: {ex.Message}");
            }
            
            return file;
        }
        public async Task<(List<TvSeason> Seasons, List<TvEpisode> Episodes)> FetchSeriesDetailsAsync(int tmdbId)
        {
            var seasons = new List<TvSeason>();
            var episodes = new List<TvEpisode>();
            try
            {
                using var db = new Data.AppDbContext();
                var settings = SettingsManager.LoadSettings();
                string apiKey = string.IsNullOrEmpty(settings.TmdbApiKey) ? "3272e27041f0b0ee11dbaf0315ce5b21" : settings.TmdbApiKey;
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;

                // Step 1: Fetch series to get seasons
                string url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language={language}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language=en-US";
                    response = await _httpClient.GetAsync(url);
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("seasons", out var seasonsArray))
                    {
                        foreach (var s in seasonsArray.EnumerateArray())
                        {
                            var season = new TvSeason
                            {
                                TmdbSeriesId = tmdbId,
                                SeasonNumber = s.TryGetProperty("season_number", out var sn) && sn.ValueKind == JsonValueKind.Number ? sn.GetInt32() : 0,
                                Name = s.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null,
                                Overview = s.TryGetProperty("overview", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetString() : null,
                                PosterPath = s.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null ? pp.GetString() : null,
                                AirDate = s.TryGetProperty("air_date", out var ad) && ad.ValueKind != JsonValueKind.Null ? ad.GetString() : null,
                                EpisodeCount = s.TryGetProperty("episode_count", out var ec) && ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : 0
                            };
                            if (season.SeasonNumber > 0) // Skip specials usually (0) or keep them? Keep all.
                                seasons.Add(season);
                        }
                    }
                }

                // Step 2: Fetch episodes for each season
                foreach (var season in seasons)
                {
                    if (season.SeasonNumber == 0) continue; // Skip specials

                    string seasonUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{season.SeasonNumber}?api_key={apiKey}&language={language}";
                    var sResponse = await _httpClient.GetAsync(seasonUrl);
                    if (!sResponse.IsSuccessStatusCode)
                    {
                        seasonUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{season.SeasonNumber}?api_key={apiKey}&language=en-US";
                        sResponse = await _httpClient.GetAsync(seasonUrl);
                    }

                    if (sResponse.IsSuccessStatusCode)
                    {
                        var sJson = await sResponse.Content.ReadAsStringAsync();
                        using var sDoc = JsonDocument.Parse(sJson);
                        var sRoot = sDoc.RootElement;

                        if (sRoot.TryGetProperty("episodes", out var epArray))
                        {
                            foreach (var ep in epArray.EnumerateArray())
                            {
                                var episode = new TvEpisode
                                {
                                    TmdbSeriesId = tmdbId,
                                    SeasonNumber = season.SeasonNumber,
                                    EpisodeNumber = ep.TryGetProperty("episode_number", out var en) && en.ValueKind == JsonValueKind.Number ? en.GetInt32() : 0,
                                    Name = ep.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null,
                                    Overview = ep.TryGetProperty("overview", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetString() : null,
                                    StillPath = ep.TryGetProperty("still_path", out var sp) && sp.ValueKind != JsonValueKind.Null ? sp.GetString() : null,
                                    AirDate = ep.TryGetProperty("air_date", out var ad) && ad.ValueKind != JsonValueKind.Null ? ad.GetString() : null,
                                    VoteAverage = ep.TryGetProperty("vote_average", out var va) && va.ValueKind == JsonValueKind.Number ? va.GetDouble() : 0
                                };
                                episodes.Add(episode);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching series details: {ex.Message}");
                throw;
            }

            return (seasons, episodes);
        }

        private async Task IdentifyWithJikanAsync(VideoFile file)
        {
            try
            {
                string query = Uri.EscapeDataString(file.FormattedTitle);
                string searchUrl = $"https://api.jikan.moe/v4/anime?q={query}&limit=1&sfw=true";
                
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
                {
                    var anime = dataArray[0];
                    
                    if (anime.TryGetProperty("title_english", out var titleEn) && titleEn.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(titleEn.GetString()))
                    {
                        file.FormattedTitle = titleEn.GetString();
                    }
                    else if (anime.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    {
                        file.FormattedTitle = title.GetString();
                    }

                    if (anime.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
                    {
                        file.SeriesStatus = status.GetString();
                    }

                    if (anime.TryGetProperty("episodes", out var episodes) && episodes.ValueKind == JsonValueKind.Number)
                    {
                        file.TotalEpisodesCount = episodes.GetInt32();
                        file.NumberOfEpisodes = episodes.GetInt32();
                    }
                    
                    file.TotalSeasonsCount = 1; 
                    file.NumberOfSeasons = 1;

                    if (anime.TryGetProperty("aired", out var aired) && aired.ValueKind == JsonValueKind.Object)
                    {
                        if (aired.TryGetProperty("from", out var fromDate) && fromDate.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(fromDate.GetString(), out var parsedDate))
                            {
                                file.FirstAirDate = parsedDate;
                                if (file.Year == null) file.Year = parsedDate.Year.ToString();
                            }
                        }
                        if (aired.TryGetProperty("to", out var toDate) && toDate.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(toDate.GetString()))
                        {
                            if (DateTime.TryParse(toDate.GetString(), out var parsedEndDate))
                            {
                                file.LastAirDate = parsedEndDate;
                            }
                        }
                    }

                    if (anime.TryGetProperty("broadcast", out var broadcast) && broadcast.ValueKind == JsonValueKind.Object)
                    {
                        if (broadcast.TryGetProperty("string", out var broadcastStr) && broadcastStr.ValueKind == JsonValueKind.String)
                        {
                            file.AirDay = broadcastStr.GetString();
                        }
                    }

                    if (anime.TryGetProperty("studios", out var studios) && studios.ValueKind == JsonValueKind.Array && studios.GetArrayLength() > 0)
                    {
                        if (studios[0].TryGetProperty("name", out var studioName) && studioName.ValueKind == JsonValueKind.String)
                        {
                            file.NetworkName = studioName.GetString();
                        }
                    }

                    if (anime.TryGetProperty("next_episode_to_air", out var nextEp) && nextEp.ValueKind == JsonValueKind.Object)
                    {
                        if (nextEp.TryGetProperty("aired_at", out var nextAirDate) && nextAirDate.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(nextAirDate.GetString(), out var parsedNextDate))
                            {
                                file.NextEpisodeDate = parsedNextDate.ToString("yyyy-MM-dd");
                            }
                        }
                        if (nextEp.TryGetProperty("episode", out var nextEpNum) && nextEpNum.ValueKind == JsonValueKind.Number)
                        {
                            file.NextEpisodeNumber = nextEpNum.GetInt32();
                        }
                    }

                    if (anime.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number)
                    {
                        file.Rating = Math.Round(score.GetDouble(), 1);
                    }
                    
                    if (anime.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                    {
                        var genreList = genres.EnumerateArray()
                            .Select(g => g.TryGetProperty("name", out var name) ? name.GetString() : "")
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList();
                        if (genreList.Any())
                        {
                            file.Genres = string.Join("، ", genreList);
                        }
                    }

                    if (anime.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object)
                    {
                        if (images.TryGetProperty("jpg", out var jpg) && jpg.ValueKind == JsonValueKind.Object)
                        {
                            if (jpg.TryGetProperty("large_image_url", out var imgUrl) && imgUrl.ValueKind == JsonValueKind.String)
                            {
                                file.PosterUrl = imgUrl.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Jikan API identification failed", ex);
            }
        }
        public async Task<List<string>> GetMediaPostersAsync(int tmdbId, string mediaType)
        {
            var posters = new List<string>();
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = settings.TmdbApiKey ?? "3272e27041f0b0ee11dbaf0315ce5b21";
                string type = mediaType.ToLower() == "series" ? "tv" : "movie";
                
                string url = $"https://api.themoviedb.org/3/{type}/{tmdbId}/images?api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("posters", out var postersArray))
                    {
                        foreach (var poster in postersArray.EnumerateArray())
                        {
                            if (poster.TryGetProperty("file_path", out var path))
                            {
                                posters.Add($"https://image.tmdb.org/t/p/w500{path.GetString()}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Failed to fetch posters", ex);
            }
            return posters;
        }
        
        public async Task<string?> DownloadAndSaveImageAsync(string url, string fileNamePrefix)
        {
            return await DownloadImageAsync(url, fileNamePrefix);
        }
    }
}
