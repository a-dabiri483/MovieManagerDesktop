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
        public string OriginalTitle { get; set; } = string.Empty;
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
            _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
            _httpClient.Timeout = TimeSpan.FromSeconds(25);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
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
                
                string proxyUrl = SettingsManager.WrapUrlWithProxy(url);
                var request = new HttpRequestMessage(HttpMethod.Get, proxyUrl);
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
                string apiKey = SettingsManager.GetTmdbApiKey();
                string url = $"https://api.themoviedb.org/3/find/{imdbId}?api_key={apiKey}&external_source=imdb_id";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                source = "TMDB_ONLY";
            if (source == "OMDB_ONLY")
                return await SearchOmdbAsync(query, settings.OmdbApiKey);
                
            return await SearchTmdbInternalAsync(query, language);
        }

        public async Task<List<TmdbSearchResult>> SearchAnimeManualAsync(string query)
        {
            var results = new List<TmdbSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;
            
            var anilistService = new AnilistService();
            var data = await anilistService.SearchAnimeAsync(query);
            if (data != null)
            {
                results.Add(new TmdbSearchResult
                {
                    Id = data.Id, // We store Anilist ID temporarily in TmdbId for the UI
                    Title = !string.IsNullOrEmpty(data.TitleEnglish) ? data.TitleEnglish : data.TitleRomaji,
                    ReleaseYear = data.SeasonYear > 0 ? data.SeasonYear.ToString() : "",
                    PosterUrl = data.CoverImageUrl,
                    MediaType = "Anime"
                });
            }
            return results;
        }

        private async Task<List<TmdbSearchResult>> SearchFmDbAsync(string query)
        {
            var results = new List<TmdbSearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;
            try
            {
                string url = $"https://imdb.iamidiotareyoutoo.com/search?q={Uri.EscapeDataString(query)}";
                LoggerService.Info($"[موتور جستجو - دستی] ارسال درخواست به FM_DB: {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                LoggerService.Info($"[موتور جستجو - دستی] ارسال درخواست به OMDB: {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                string apiKey = SettingsManager.GetTmdbApiKey();
                string encodedQuery = Uri.EscapeDataString(query);
                
                string url;
                if (query.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    LoggerService.Info($"[موتور جستجو - دستی] جستجو با شناسه IMDB: {query}");
                    
                    url = $"https://api.themoviedb.org/3/find/{query}?api_key={apiKey}&external_source=imdb_id&language={language}";
                    var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        JsonElement? foundItem = null;
                        string mediaType = "movie";
                        if (root.TryGetProperty("movie_results", out var mr) && mr.GetArrayLength() > 0)
                        {
                            foundItem = mr[0];
                            mediaType = "movie";
                        }
                        else if (root.TryGetProperty("tv_results", out var tr) && tr.GetArrayLength() > 0)
                        {
                            foundItem = tr[0];
                            mediaType = "tv";
                        }

                        if (foundItem.HasValue)
                        {
                            var item = foundItem.Value;
                            var res = new TmdbSearchResult { MediaType = mediaType };
                            if (item.TryGetProperty("id", out var idProp)) res.Id = idProp.GetInt32();
                            if (item.TryGetProperty("title", out var titleProp)) res.Title = titleProp.GetString() ?? "";
                            else if (item.TryGetProperty("name", out var nameProp)) res.Title = nameProp.GetString() ?? "";
                            
                            if (item.TryGetProperty("release_date", out var rd) && !string.IsNullOrEmpty(rd.GetString()))
                                res.ReleaseYear = rd.GetString()!.Substring(0, 4);
                            else if (item.TryGetProperty("first_air_date", out var fad) && !string.IsNullOrEmpty(fad.GetString()))
                                res.ReleaseYear = fad.GetString()!.Substring(0, 4);

                            if (item.TryGetProperty("poster_path", out var pp) && pp.ValueKind == JsonValueKind.String)
                                res.PosterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w92{pp.GetString()}");

                            results.Add(res);
                            return results;
                        }
                    }
                }
                else if (int.TryParse(query, out int tmdbId))
                {
                    // Direct ID lookup
                    url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}&language={language}";
                    var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                    if (!response.IsSuccessStatusCode)
                    {
                        url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language={language}";
                        response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                            res.PosterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w92{pp.GetString()}");

                        results.Add(res);
                        return results;
                    }
                }

                url = $"https://api.themoviedb.org/3/search/multi?api_key={apiKey}&query={encodedQuery}&language={language}";
                var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                
                if (!resp.IsSuccessStatusCode && language == "en-US")
                {
                     url = url.Replace("language=en-US", "language=fa-IR");
                     resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                            
                            if (item.TryGetProperty("original_title", out var oTitleProp)) res.OriginalTitle = oTitleProp.GetString() ?? "";
                            else if (item.TryGetProperty("original_name", out var oNameProp)) res.OriginalTitle = oNameProp.GetString() ?? "";
                            
                            if (item.TryGetProperty("media_type", out var typeProp)) res.MediaType = typeProp.GetString() ?? "";
                            if (res.MediaType == "person") continue; // skip actors

                            if (item.TryGetProperty("release_date", out var rd) && rd.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(rd.GetString()))
                                res.ReleaseYear = rd.GetString()!.Substring(0, 4);
                            else if (item.TryGetProperty("first_air_date", out var fad) && fad.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(fad.GetString()))
                                res.ReleaseYear = fad.GetString()!.Substring(0, 4);

                            if (item.TryGetProperty("poster_path", out var pp) && pp.ValueKind == JsonValueKind.String)
                                res.PosterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w92{pp.GetString()}");
                                
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

                LoggerService.Info($"[اسکنر] شروع بررسی ارتباطات برای: {file.FormattedTitle}");

                bool configApiSuccess = false;

                // 1. First Step: Configured API
                LoggerService.Info($"[موتور جستجو] (مرحله 1) شروع جستجو در سرور تنظیمات ({source})...");
                
                if (file.TmdbId.HasValue && file.TmdbId > 0)
                {
                    LoggerService.Info($"[موتور جستجو] آیدی TMDB موجود است. درخواست مستقیم از TMDB...");
                    await IdentifyWithTmdb(file, SettingsManager.GetTmdbApiKey(), language);
                    configApiSuccess = !string.IsNullOrWhiteSpace(file.PosterUrl) && !string.IsNullOrWhiteSpace(file.Overview);
                }
                else if (source == "FM_DB")
                {
                    LoggerService.Info($"[موتور جستجو] درخواست از سرور رایگان FM_DB...");
                    await IdentifyWithFmDb(file);
                    
                    if (file.TmdbId.HasValue && file.TmdbId > 0)
                    {
                        LoggerService.Info($"[موتور جستجو] اطلاعات پایه از FM_DB دریافت شد. تکمیل اطلاعات از TMDB...");
                        await IdentifyWithTmdb(file, SettingsManager.GetTmdbApiKey(), language);
                        configApiSuccess = !string.IsNullOrWhiteSpace(file.PosterUrl) && !string.IsNullOrWhiteSpace(file.Overview);
                    }
                }
                else if (source == "TMDB_ONLY")
                {
                    LoggerService.Info($"[موتور جستجو] درخواست مستقیم از سرور اصلی TMDB...");
                    await IdentifyWithTmdb(file, SettingsManager.GetTmdbApiKey(), language);
                    configApiSuccess = !string.IsNullOrWhiteSpace(file.PosterUrl) && !string.IsNullOrWhiteSpace(file.Overview);
                }
                else if (source == "OMDB_ONLY")
                {
                    LoggerService.Info($"[موتور جستجو] درخواست مستقیم از سرور OMDB...");
                    await IdentifyWithOmdb(file, settings.OmdbApiKey);
                    configApiSuccess = !string.IsNullOrWhiteSpace(file.PosterUrl) && !string.IsNullOrWhiteSpace(file.Overview);
                }

                // 2. Second Step: TVMaze (only if Configured API failed and it's a Series)
                bool tvmazeSuccess = false;
                if (!configApiSuccess && file.MediaType == "Series")
                {
                    LoggerService.Info($"[موتور جستجو] (مرحله 2) سرور تنظیمات موفق نبود. سویچ به سرور جایگزین سریال (TVMaze)...");
                    var tvmazeService = new TvMazeService();
                    var tvmazeData = await tvmazeService.SearchSeriesAsync(file.FormattedTitle);
                    if (tvmazeData != null && !string.IsNullOrEmpty(tvmazeData.Title))
                    {
                        if (string.IsNullOrWhiteSpace(file.FormattedTitle) || file.FormattedTitle == file.FileName) file.FormattedTitle = tvmazeData.Title;
                        if (string.IsNullOrWhiteSpace(file.PosterUrl)) file.PosterUrl = tvmazeData.PosterUrl;
                        if (string.IsNullOrWhiteSpace(file.Overview)) file.Overview = tvmazeData.Summary;
                        if (string.IsNullOrWhiteSpace(file.Genres)) file.Genres = tvmazeData.Genres;
                        if (file.Rating == null && tvmazeData.AverageRating > 0) file.Rating = tvmazeData.AverageRating;
                        if (string.IsNullOrWhiteSpace(file.SeriesStatus)) file.SeriesStatus = tvmazeData.Status;
                        
                        if (!file.FirstAirDate.HasValue && DateTime.TryParse(tvmazeData.Premiered, out var prem)) file.FirstAirDate = prem;
                        if (!file.LastAirDate.HasValue && DateTime.TryParse(tvmazeData.Ended, out var end)) file.LastAirDate = end;
                        
                        if (string.IsNullOrWhiteSpace(file.NetworkName)) file.NetworkName = tvmazeData.Network;
                        if (string.IsNullOrWhiteSpace(file.AirDay)) file.AirDay = tvmazeData.ScheduleDays;
                        if (string.IsNullOrWhiteSpace(file.AirTime)) file.AirTime = tvmazeData.ScheduleTime;

                        if (!file.TmdbId.HasValue || file.TmdbId <= 0) file.TmdbId = tvmazeData.Id;
                        
                        tvmazeSuccess = true;
                    }
                }

                // 3. Fallback for BOTH Movies and Series: AniList
                bool isAnimeFromTvMaze = file.Genres != null && file.Genres.Contains("Anime", StringComparison.OrdinalIgnoreCase);
                bool primaryFailed = string.IsNullOrWhiteSpace(file.PosterUrl) && string.IsNullOrWhiteSpace(file.Overview);
                
                if (primaryFailed || isAnimeFromTvMaze)
                {
                    if (isAnimeFromTvMaze)
                        LoggerService.Info($"[موتور جستجو] سیستم تشخیص داد این عنوان یک انیمه است. دریافت اطلاعات تخصصی از سرور AniList...");
                    else
                        LoggerService.Info($"[موتور جستجو] (مرحله نهایی) سرورهای قبلی پاسخگو نبودند یا دیتایی نداشتند. درخواست از سرور انیمه (AniList)...");
                        
                    var anilistService = new AnilistService();
                    var anilistData = await anilistService.SearchAnimeAsync(file.FormattedTitle, file.Year);
                    if (anilistData != null)
                    {
                        if (!string.IsNullOrEmpty(anilistData.TitleEnglish)) file.FormattedTitle = anilistData.TitleEnglish;
                        else if (!string.IsNullOrEmpty(anilistData.TitleRomaji)) file.FormattedTitle = anilistData.TitleRomaji;

                        if (!string.IsNullOrEmpty(anilistData.CoverImageUrl)) file.PosterUrl = anilistData.CoverImageUrl;
                        if (!string.IsNullOrEmpty(anilistData.BannerImageUrl)) file.BackdropUrl = anilistData.BannerImageUrl;
                        if (!string.IsNullOrEmpty(anilistData.Description)) file.Overview = anilistData.Description;
                        if (anilistData.AverageScore > 0) file.Rating = anilistData.AverageScore;
                        if (!string.IsNullOrEmpty(anilistData.Genres)) file.Genres = anilistData.Genres;
                        if (!string.IsNullOrEmpty(anilistData.Status)) file.SeriesStatus = anilistData.Status;
                        
                        file.TmdbId = anilistData.Id;
                        if (anilistData.Episodes > 1) file.TotalEpisodesCount = anilistData.Episodes;
                    }
                }
                
                // Clean up any previously appended tracker info from overview
                CleanTrackerInfoFromOverview(file);

                // Fallback for backdrop if missing
                if (string.IsNullOrWhiteSpace(file.BackdropUrl) && !string.IsNullOrWhiteSpace(file.PosterUrl))
                {
                    file.BackdropUrl = file.PosterUrl;
                }

                // Fallback: extract 4-digit year from filename if Year is still empty
                if (string.IsNullOrWhiteSpace(file.Year) && !string.IsNullOrWhiteSpace(file.FileName))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(file.FileName, @"\b(19\d{2}|20\d{2})\b");
                    if (match.Success)
                    {
                        file.Year = match.Groups[1].Value;
                    }
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

            LoggerService.Info($"[FM_DB] ارسال درخواست جستجو: {url}");
            var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
            if (response.IsSuccessStatusCode)
            {
                LoggerService.Info($"[FM_DB] پاسخ موفق دریافت شد.");
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

            LoggerService.Info($"[TMDB] ارسال درخواست جستجو/دریافت: {url}");
            var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
            
            if (!response.IsSuccessStatusCode && language == "fa-IR")
            {
                 LoggerService.Info($"[TMDB] پاسخ با زبان فارسی ناموفق بود. تلاش مجدد با زبان انگلیسی...");
                 url = url.Replace("language=fa-IR", "language=en-US");
                 LoggerService.Info($"[TMDB] ارسال درخواست جایگزین: {url}");
                 response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
            }

            if (!response.IsSuccessStatusCode && isDirectIdLookup)
            {
                LoggerService.Info($"[TMDB] درخواست مستقیم آیدی با خطا مواجه شد. تلاش با جستجوی عنوان...");
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
                
                LoggerService.Info($"[TMDB] ارسال درخواست جستجوی عنوان: {url}");
                response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
            }

            bool hasMatch = false;
            if (response.IsSuccessStatusCode)
            {
                LoggerService.Info($"[TMDB] پاسخ موفق دریافت شد (200 OK)");
                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                
                if (!isDirectIdLookup && root.TryGetProperty("results", out var results) && results.GetArrayLength() == 0 && !string.IsNullOrWhiteSpace(file.Year))
                {
                    LoggerService.Info($"[TMDB] جستجو با سال ساخت نتیجه‌ای نداشت. جستجوی مجدد بدون سال...");
                    // Fallback: try searching without year
                    string fallbackType = file.MediaType == "Series" ? "tv" : "movie";
                    string fallbackUrl = $"https://api.themoviedb.org/3/search/{fallbackType}?api_key={apiKey}&query={query}&language={language}";
                    LoggerService.Info($"[TMDB] ارسال درخواست بدون سال: {fallbackUrl}");
                    response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(fallbackUrl));
                    if (response.IsSuccessStatusCode)
                    {
                        LoggerService.Info($"[TMDB] پاسخ درخواست بدون سال موفق بود.");
                        json = await response.Content.ReadAsStringAsync();
                        using var fallbackDoc = JsonDocument.Parse(json);
                        root = fallbackDoc.RootElement.Clone();
                    }
                }
                
                JsonElement firstMatch = default;

                if (isDirectIdLookup)
                {
                    firstMatch = root;
                    hasMatch = true;
                }
                else if (root.TryGetProperty("results", out results) && results.GetArrayLength() > 0)
                {
                    var validResults = results.EnumerateArray().Where(res => {
                        if (res.TryGetProperty("vote_count", out var vc) && vc.ValueKind == JsonValueKind.Number)
                        {
                            return vc.GetInt32() >= 2 || !string.IsNullOrWhiteSpace(file.Year);
                        }
                        return !string.IsNullOrWhiteSpace(file.Year);
                    }).ToList();

                    if (validResults.Count > 0)
                    {
                        firstMatch = validResults[0];
                        hasMatch = true;
                    }
                }
                
                // If still no match and we were using fa-IR, fallback to en-US
                if (!hasMatch && !isDirectIdLookup && language == "fa-IR")
                {
                    string enUrl = url.Replace("language=fa-IR", "language=en-US");
                    var enResponse = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(enUrl));
                    if (enResponse.IsSuccessStatusCode)
                    {
                        var enJson = await enResponse.Content.ReadAsStringAsync();
                        using var enDoc = JsonDocument.Parse(enJson);
                        var enRoot = enDoc.RootElement;
                        
                        // Try without year if year was provided and failed
                        if (enRoot.TryGetProperty("results", out var enRes) && enRes.GetArrayLength() == 0 && !string.IsNullOrWhiteSpace(file.Year))
                        {
                            string enFallbackUrl = $"https://api.themoviedb.org/3/search/multi?api_key={apiKey}&query={query}&language=en-US";
                            var enFbResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(enFallbackUrl));
                            if (enFbResp.IsSuccessStatusCode)
                            {
                                enJson = await enFbResp.Content.ReadAsStringAsync();
                                using var enFbDoc = JsonDocument.Parse(enJson);
                                enRoot = enFbDoc.RootElement.Clone();
                            }
                        }
                        
                        if (enRoot.TryGetProperty("results", out enRes) && enRes.GetArrayLength() > 0)
                        {
                            var validEnResults = enRes.EnumerateArray().Where(res => {
                                if (res.TryGetProperty("vote_count", out var vc) && vc.ValueKind == JsonValueKind.Number)
                                {
                                    return vc.GetInt32() >= 2 || !string.IsNullOrWhiteSpace(file.Year);
                                }
                                return !string.IsNullOrWhiteSpace(file.Year);
                            }).ToList();

                            if (validEnResults.Count > 0)
                            {
                                firstMatch = validEnResults[0];
                                hasMatch = true;
                                root = enRoot.Clone(); // update root for following logic
                            }
                        }
                    }
                }

                if (hasMatch)
                {
                    if (firstMatch.TryGetProperty("poster_path", out var posterPath) && posterPath.ValueKind == JsonValueKind.String)
                    {
                        var path = posterPath.GetString();
                        if (!string.IsNullOrEmpty(path))
                            file.PosterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w500{path}");
                    }
                    if (firstMatch.TryGetProperty("backdrop_path", out var backdropPath) && backdropPath.ValueKind == JsonValueKind.String)
                    {
                        var path = backdropPath.GetString();
                        if (!string.IsNullOrEmpty(path))
                            file.BackdropUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/original{path}");
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

                    // Extract Release Year and Air Dates
                    if (string.IsNullOrWhiteSpace(file.Year))
                    {
                        if (firstMatch.TryGetProperty("release_date", out var rd) && !string.IsNullOrEmpty(rd.GetString()) && rd.GetString()!.Length >= 4)
                        {
                            file.Year = rd.GetString()!.Substring(0, 4);
                        }
                        else if (firstMatch.TryGetProperty("first_air_date", out var fad) && !string.IsNullOrEmpty(fad.GetString()) && fad.GetString()!.Length >= 4)
                        {
                            file.Year = fad.GetString()!.Substring(0, 4);
                        }
                    }

                    if (firstMatch.TryGetProperty("first_air_date", out var faDate) && DateTime.TryParse(faDate.GetString(), out var parsedFad))
                    {
                        file.FirstAirDate = parsedFad;
                    }
                    else if (firstMatch.TryGetProperty("release_date", out var relDate) && DateTime.TryParse(relDate.GetString(), out var parsedRel))
                    {
                        file.FirstAirDate = parsedRel;
                    }
                    
                    int tmdbId = 0;
                    if (firstMatch.TryGetProperty("id", out var id))
                    {
                        tmdbId = id.GetInt32();
                        file.TmdbId = tmdbId;
                    }
                    
                    string mediaType = (file.MediaType == "Series" || file.MediaType == "Anime") ? "tv" : "movie";
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
                            LoggerService.Info($"[TMDB] دریافت اطلاعات تکمیلی (بازیگران و عوامل): {detailsUrl}");
                            var detailsResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(detailsUrl));
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
                                    var enDetailsResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(enDetailsUrl));
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
                                                file.PosterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w500{path}");
                                        }
                                        
                                        if (string.IsNullOrWhiteSpace(file.BackdropUrl) && enDetailsRoot.TryGetProperty("backdrop_path", out var enBackdropPath) && enBackdropPath.ValueKind == JsonValueKind.String)
                                        {
                                            var path = enBackdropPath.GetString();
                                            if (!string.IsNullOrEmpty(path))
                                                file.BackdropUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/original{path}");
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
                LoggerService.Info($"[TMDB] استخراج اطلاعات دقیق سریال: {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                
                if (!response.IsSuccessStatusCode && language == "fa-IR")
                {
                    url = $"https://api.themoviedb.org/3/tv/{file.TmdbId}?api_key={apiKey}&language=en-US";
                    LoggerService.Info($"[TMDB] تلاش مجدد با زبان انگلیسی: {url}");
                    response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                }
                
                if (response.IsSuccessStatusCode)
                {
                    LoggerService.Info($"[TMDB] دریافت اطلاعات سریال موفق بود.");
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
                                var seasonResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(seasonUrl));
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

            // Fallback to TVMaze ONLY if crucial info (Network) is missing from TMDB
            try
            {
                if (string.IsNullOrWhiteSpace(file.NetworkName) || string.IsNullOrWhiteSpace(file.AirDay))
                {
                    LoggerService.Info($"[TMDB] اطلاعات شبکه یا روز پخش در TMDB یافت نشد، تلاش از طریق TVMaze...");
                    var tvmazeService = new TvMazeService();
                    var tvmazeResult = await tvmazeService.SearchSeriesAsync(file.FormattedTitle);
                    if (tvmazeResult == null && !string.IsNullOrWhiteSpace(file.FileName))
                    {
                        LoggerService.Info($"[TVMaze] جستجو با نام اصلی فایل: {file.FileName}");
                        tvmazeResult = await tvmazeService.SearchSeriesAsync(file.FileName);
                    }
                    if (tvmazeResult != null)
                    {
                        if (string.IsNullOrWhiteSpace(file.NetworkName) && !string.IsNullOrWhiteSpace(tvmazeResult.Network))
                            file.NetworkName = tvmazeResult.Network;
                        if (string.IsNullOrWhiteSpace(file.AirDay) && !string.IsNullOrWhiteSpace(tvmazeResult.ScheduleDays))
                            file.AirDay = tvmazeResult.ScheduleDays;
                        if (string.IsNullOrWhiteSpace(file.AirTime) && !string.IsNullOrWhiteSpace(tvmazeResult.ScheduleTime))
                            file.AirTime = tvmazeResult.ScheduleTime;
                        if (string.IsNullOrWhiteSpace(file.SeriesStatus) && !string.IsNullOrWhiteSpace(tvmazeResult.Status))
                            file.SeriesStatus = tvmazeResult.Status;
                    }
                }
            }
            catch
            {
                // Ignore TVMaze fallback errors
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

            LoggerService.Info($"[OMDB] ارسال درخواست جستجو: {url}");
            var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
            if (response.IsSuccessStatusCode)
            {
                LoggerService.Info($"[OMDB] پاسخ موفق دریافت شد.");
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
                string apiKey = SettingsManager.GetTmdbApiKey();
                string url = $"https://api.themoviedb.org/3/tv/{file.TmdbId.Value}?api_key={apiKey}&language=fa-IR";
                
                LoggerService.Info($"[TMDB] استخراج مجدد وضعیت سریال: {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                if (!response.IsSuccessStatusCode)
                {
                    url = $"https://api.themoviedb.org/3/tv/{file.TmdbId.Value}?api_key={apiKey}&language=en-US";
                    LoggerService.Info($"[TMDB] تلاش مجدد با زبان انگلیسی: {url}");
                    response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                }

                if (response.IsSuccessStatusCode)
                {
                    LoggerService.Info($"[TMDB] دریافت وضعیت سریال موفق بود.");
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
            
            CleanTrackerInfoFromOverview(file);
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
                string apiKey = SettingsManager.GetTmdbApiKey();
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;

                // Step 1: Fetch series to get seasons
                string url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language={language}";
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                if (!response.IsSuccessStatusCode)
                {
                    url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&language=en-US";
                    response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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
                    var sResponse = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(seasonUrl));
                    if (!sResponse.IsSuccessStatusCode)
                    {
                        seasonUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{season.SeasonNumber}?api_key={apiKey}&language=en-US";
                        sResponse = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(seasonUrl));
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

        public void CleanTrackerInfoFromOverview(VideoFile file)
        {
            if (string.IsNullOrWhiteSpace(file.Overview)) return;
            
            string cleanOverview = file.Overview.Trim();
            int separatorIndex = cleanOverview.IndexOf("\n---");
            if (separatorIndex > -1)
            {
                string possibleTrackerBlock = cleanOverview.Substring(separatorIndex);
                if (possibleTrackerBlock.Contains("وضعیت:") || possibleTrackerBlock.Contains("تاریخ شروع:") || possibleTrackerBlock.Contains("شبکه:") || possibleTrackerBlock.Contains("تعداد قسمت‌ها:"))
                {
                    file.Overview = cleanOverview.Substring(0, separatorIndex).Trim();
                }
            }
            else
            {
                int statusIndex = cleanOverview.LastIndexOf("وضعیت:");
                if (statusIndex > 0)
                {
                    string possibleTrackerBlock = cleanOverview.Substring(statusIndex);
                    if (possibleTrackerBlock.Contains("تاریخ شروع:") || possibleTrackerBlock.Contains("شبکه:") || possibleTrackerBlock.Contains("برنامه پخش:"))
                    {
                        file.Overview = cleanOverview.Substring(0, statusIndex).Trim();
                    }
                }
            }
        }

        public async Task<List<string>> GetMediaPostersAsync(int tmdbId, string mediaType)
        {
            var posters = new List<string>();
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = SettingsManager.GetTmdbApiKey();
                string type = mediaType.ToLower() == "series" ? "tv" : "movie";
                
                string url = $"https://api.themoviedb.org/3/{type}/{tmdbId}/images?api_key={apiKey}";
                LoggerService.Info($"[TMDB] دریافت لیست پوسترهای جایگزین: {url}");
                var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
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

