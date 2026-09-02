using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class CinemaNewsItem : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _summary = string.Empty;

        public string Source { get; set; } = "سینما پرس";
        public string PublishedAt { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        [ObservableProperty]
        private string _originalTitle = string.Empty;

        [ObservableProperty]
        private string _originalSummary = string.Empty;

        [ObservableProperty]
        private string _translatedTitle = string.Empty;

        [ObservableProperty]
        private string _translatedSummary = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TranslateButtonText))]
        private bool _isTranslated;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TranslateButtonText))]
        private bool _isTranslating;

        public string TranslateButtonText => IsTranslating ? "در حال ترجمه..." : (IsTranslated ? "متن اصلی" : "ترجمه");

        public bool IsGlobalNews { get; set; }
    }

    public partial class BoxOfficeItem : ObservableObject
    {
        public int Rank { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalTitle { get; set; } = string.Empty;
        public string WeekendGross { get; set; } = string.Empty;
        public string TotalGross { get; set; } = string.Empty;
        public int WeeksOut { get; set; } = 1;
        public string PosterUrl { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string FormattedRating => Rating > 0 ? Rating.ToString("0.0") : "-";
        public string Genres { get; set; } = string.Empty;
        public int TmdbId { get; set; }
    }

    public partial class UpcomingItem : ObservableObject
    {
        public int TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string JalaliReleaseDate { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public string PosterUrl { get; set; } = string.Empty;
        public string BackdropUrl { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string Genres { get; set; } = string.Empty;
    }

    public class CinemaHubCacheMeta
    {
        public DateTime? IranNewsUpdated { get; set; }
        public DateTime? WorldNewsUpdated { get; set; }
        public DateTime? BoxOfficeUpdated { get; set; }
        public DateTime? UpcomingUpdated { get; set; }
    }

    public partial class CinemaHubViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: News, 1: Box Office, 2: Upcoming

        [ObservableProperty]
        private int _newsCategoryIndex = 0; // 0: سینمای ایران, 1: سینمای جهان

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _lastUpdatedText = string.Empty;

        public ObservableCollection<CinemaNewsItem> IranNewsList { get; } = new();
        public ObservableCollection<CinemaNewsItem> WorldNewsList { get; } = new();
        public ObservableCollection<BoxOfficeItem> BoxOfficeList { get; } = new();
        public ObservableCollection<UpcomingItem> UpcomingList { get; } = new();

        private readonly HttpClient _httpClient;
        private static readonly string CacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CineTrackManager", "CinemaHubCache");
        private CinemaHubCacheMeta _meta = new();

        public CinemaHubViewModel(int initialTab = 0)
        {
            _selectedTabIndex = initialTab;
            _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler())
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }

            LoadMeta();
            _ = LoadDataForCurrentTabAsync(forceRefresh: false);
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            UpdateLastUpdatedDisplay();
            _ = LoadDataForCurrentTabAsync(forceRefresh: false);
        }

        partial void OnNewsCategoryIndexChanged(int value)
        {
            UpdateLastUpdatedDisplay();
            _ = LoadDataForCurrentTabAsync(forceRefresh: false);
        }

        [RelayCommand]
        public async Task SwitchNewsCategoryAsync(string indexStr)
        {
            if (int.TryParse(indexStr, out int idx))
            {
                NewsCategoryIndex = idx;
            }
        }

        [RelayCommand]
        public async Task RefreshCurrentTabAsync()
        {
            await LoadDataForCurrentTabAsync(forceRefresh: true);
        }

        // =========================================================================
        // Cache Management (News: 24h, Box Office: 7 days, Upcoming: 7 days)
        // =========================================================================
        private void LoadMeta()
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);
                string metaFile = Path.Combine(CacheFolder, "cache_meta.json");
                if (File.Exists(metaFile))
                {
                    string json = File.ReadAllText(metaFile);
                    _meta = JsonSerializer.Deserialize<CinemaHubCacheMeta>(json) ?? new();
                }
            }
            catch { }
        }

        private void SaveMeta()
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);
                string metaFile = Path.Combine(CacheFolder, "cache_meta.json");
                File.WriteAllText(metaFile, JsonSerializer.Serialize(_meta, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void UpdateLastUpdatedDisplay()
        {
            DateTime? dt = null;
            if (SelectedTabIndex == 0)
            {
                dt = NewsCategoryIndex == 0 ? _meta.IranNewsUpdated : _meta.WorldNewsUpdated;
            }
            else if (SelectedTabIndex == 1)
            {
                dt = _meta.BoxOfficeUpdated;
            }
            else if (SelectedTabIndex == 2)
            {
                dt = _meta.UpcomingUpdated;
            }

            if (!dt.HasValue)
            {
                LastUpdatedText = "آخرین بروزرسانی: --";
                return;
            }

            var localDt = dt.Value.ToLocalTime();
            var diff = DateTime.Now - localDt;

            if (diff.TotalMinutes < 2)
            {
                LastUpdatedText = "آخرین بروزرسانی: همین الان";
            }
            else if (diff.TotalHours < 1)
            {
                LastUpdatedText = $"آخرین بروزرسانی: {(int)diff.TotalMinutes} دقیقه پیش";
            }
            else if (localDt.Date == DateTime.Today)
            {
                LastUpdatedText = $"آخرین بروزرسانی: امروز ساعت {localDt:HH:mm}";
            }
            else if (diff.TotalDays < 2)
            {
                LastUpdatedText = $"آخرین بروزرسانی: دیروز ساعت {localDt:HH:mm}";
            }
            else
            {
                var pc = new PersianCalendar();
                LastUpdatedText = $"آخرین بروزرسانی: {pc.GetYear(localDt)}/{pc.GetMonth(localDt):D2}/{pc.GetDayOfMonth(localDt):D2}";
            }
        }

        private async Task<bool> TryLoadFromDiskCacheAsync<T>(string filename, ObservableCollection<T> targetCollection)
        {
            try
            {
                string filePath = Path.Combine(CacheFolder, filename);
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    var items = JsonSerializer.Deserialize<List<T>>(json);
                    if (items != null && items.Count > 0)
                    {
                        targetCollection.Clear();
                        foreach (var it in items) targetCollection.Add(it);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private async Task SaveToDiskCacheAsync<T>(string filename, IEnumerable<T> items)
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);
                string filePath = Path.Combine(CacheFolder, filename);
                string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch { }
        }

        private async Task LoadDataForCurrentTabAsync(bool forceRefresh)
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "در حال بارگذاری اطلاعات موثق و زنده...";

            try
            {
                if (SelectedTabIndex == 0)
                {
                    // ── NEWS (Auto-refresh every 24 hours) ──
                    if (NewsCategoryIndex == 0)
                    {
                        bool isExpired = !_meta.IranNewsUpdated.HasValue || (DateTime.UtcNow - _meta.IranNewsUpdated.Value).TotalHours >= 24;
                        if (!forceRefresh && !isExpired && IranNewsList.Count > 0)
                        {
                            UpdateLastUpdatedDisplay();
                            return;
                        }

                        if (!forceRefresh && await TryLoadFromDiskCacheAsync("iran_news_cache.json", IranNewsList) && !isExpired)
                        {
                            UpdateLastUpdatedDisplay();
                            return;
                        }

                        await LoadIranCinemaNewsAsync();
                        _meta.IranNewsUpdated = DateTime.UtcNow;
                        SaveMeta();
                        await SaveToDiskCacheAsync("iran_news_cache.json", IranNewsList);
                    }
                    else
                    {
                        bool isExpired = !_meta.WorldNewsUpdated.HasValue || (DateTime.UtcNow - _meta.WorldNewsUpdated.Value).TotalHours >= 24;
                        if (!forceRefresh && !isExpired && WorldNewsList.Count > 0)
                        {
                            UpdateLastUpdatedDisplay();
                            return;
                        }

                        if (!forceRefresh && await TryLoadFromDiskCacheAsync("world_news_cache.json", WorldNewsList) && !isExpired)
                        {
                            UpdateLastUpdatedDisplay();
                            return;
                        }

                        await LoadWorldCinemaNewsAsync();
                        _meta.WorldNewsUpdated = DateTime.UtcNow;
                        SaveMeta();
                        await SaveToDiskCacheAsync("world_news_cache.json", WorldNewsList);
                    }
                }
                else if (SelectedTabIndex == 1)
                {
                    // ── BOX OFFICE (Auto-refresh every 7 days) ──
                    bool isExpired = !_meta.BoxOfficeUpdated.HasValue || (DateTime.UtcNow - _meta.BoxOfficeUpdated.Value).TotalDays >= 7;
                    if (!forceRefresh && !isExpired && BoxOfficeList.Count > 0)
                    {
                        UpdateLastUpdatedDisplay();
                        return;
                    }

                    if (!forceRefresh && await TryLoadFromDiskCacheAsync("box_office_cache.json", BoxOfficeList) && !isExpired)
                    {
                        UpdateLastUpdatedDisplay();
                        return;
                    }

                    await LoadRealBoxOfficeAsync();
                    _meta.BoxOfficeUpdated = DateTime.UtcNow;
                    SaveMeta();
                    await SaveToDiskCacheAsync("box_office_cache.json", BoxOfficeList);
                }
                else if (SelectedTabIndex == 2)
                {
                    // ── UPCOMING RELEASES (Auto-refresh every 7 days) ──
                    bool isExpired = !_meta.UpcomingUpdated.HasValue || (DateTime.UtcNow - _meta.UpcomingUpdated.Value).TotalDays >= 7;
                    if (!forceRefresh && !isExpired && UpcomingList.Count > 0)
                    {
                        UpdateLastUpdatedDisplay();
                        return;
                    }

                    if (!forceRefresh && await TryLoadFromDiskCacheAsync("upcoming_cache.json", UpcomingList) && !isExpired)
                    {
                        UpdateLastUpdatedDisplay();
                        return;
                    }

                    await LoadRealUpcomingAsync();
                    _meta.UpcomingUpdated = DateTime.UtcNow;
                    SaveMeta();
                    await SaveToDiskCacheAsync("upcoming_cache.json", UpcomingList);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error loading CinemaHub data", ex);
                StatusMessage = "خطا در برقراری ارتباط با سرور";
            }
            finally
            {
                IsLoading = false;
                StatusMessage = string.Empty;
                UpdateLastUpdatedDisplay();
            }
        }

        // =========================================================================
        // Translate News Item (Google Translate / Customization Settings)
        // =========================================================================
        [RelayCommand]
        public async Task TranslateNewsItemAsync(CinemaNewsItem? item)
        {
            if (item == null || item.IsTranslating) return;

            // Toggle if already translated
            if (item.IsTranslated)
            {
                item.Title = item.OriginalTitle;
                item.Summary = item.OriginalSummary;
                item.IsTranslated = false;
                return;
            }

            if (!string.IsNullOrEmpty(item.TranslatedTitle) && !string.IsNullOrEmpty(item.TranslatedSummary))
            {
                item.Title = item.TranslatedTitle;
                item.Summary = item.TranslatedSummary;
                item.IsTranslated = true;
                return;
            }

            item.IsTranslating = true;
            try
            {
                string targetLang = SettingsManager.LoadSettings().TranslateToLanguage ?? "fa";
                var transTitleTask = TranslationService.TranslateTextAsync(item.OriginalTitle, targetLang);
                var transSummaryTask = TranslationService.TranslateTextAsync(item.OriginalSummary, targetLang);

                await Task.WhenAll(transTitleTask, transSummaryTask);

                string tTitle = await transTitleTask;
                string tSummary = await transSummaryTask;

                if (!string.IsNullOrWhiteSpace(tTitle)) item.TranslatedTitle = tTitle;
                if (!string.IsNullOrWhiteSpace(tSummary)) item.TranslatedSummary = tSummary;

                item.Title = item.TranslatedTitle;
                item.Summary = item.TranslatedSummary;
                item.IsTranslated = true;
            }
            catch (Exception ex)
            {
                LoggerService.Error("[CinemaNews] Translation error", ex);
            }
            finally
            {
                item.IsTranslating = false;
            }
        }

        // =========================================================================
        // 1. Iran Cinema News (سینما پرس و سینما سینما)
        // =========================================================================
        private async Task LoadIranCinemaNewsAsync()
        {
            IranNewsList.Clear();
            var feeds = new[]
            {
                ("https://www.cinemapress.ir/rss", "سینما پرس"),
                ("https://cinemacinema.ir/feed/", "سینما سینما")
            };

            foreach (var (feedUrl, sourceName) in feeds)
            {
                try
                {
                    var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(feedUrl));
                    if (!resp.IsSuccessStatusCode) continue;

                    string content = await resp.Content.ReadAsStringAsync();
                    var items = Regex.Matches(content, @"<item>(.*?)</item>", RegexOptions.Singleline);

                    foreach (Match it in items.Take(12))
                    {
                        var tMatch = Regex.Match(it.Value, @"<title>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</title>", RegexOptions.Singleline);
                        var dMatch = Regex.Match(it.Value, @"<description>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</description>", RegexOptions.Singleline);
                        var pMatch = Regex.Match(it.Value, @"<pubDate>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</pubDate>", RegexOptions.Singleline);
                        var lMatch = Regex.Match(it.Value, @"<link>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</link>", RegexOptions.Singleline);
                        var imgMatch = Regex.Match(it.Value, @"<enclosure[^>]+url=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!imgMatch.Success) imgMatch = Regex.Match(it.Value, @"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase);

                        string title = tMatch.Success ? System.Net.WebUtility.HtmlDecode(tMatch.Groups[1].Value.Trim()) : "";
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        string rawDesc = dMatch.Success ? dMatch.Groups[1].Value : "";
                        string cleanDesc = Regex.Replace(System.Net.WebUtility.HtmlDecode(rawDesc), @"<[^>]+>", "").Trim();
                        cleanDesc = Regex.Replace(cleanDesc, @"\s+", " ");

                        string rawDate = pMatch.Success ? pMatch.Groups[1].Value.Trim() : "";
                        string displayDate = FormatPublishedDate(rawDate);

                        string link = lMatch.Success ? lMatch.Groups[1].Value.Trim() : "";
                        string imgUrl = imgMatch.Success ? imgMatch.Groups[1].Value.Trim() : "";

                        if (string.IsNullOrEmpty(imgUrl))
                        {
                            imgUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&auto=format&fit=crop&q=80";
                        }

                        IranNewsList.Add(new CinemaNewsItem
                        {
                            Title = title,
                            Summary = cleanDesc,
                            OriginalTitle = title,
                            OriginalSummary = cleanDesc,
                            Source = sourceName,
                            PublishedAt = displayDate,
                            ImageUrl = SettingsManager.WrapUrlWithProxy(imgUrl),
                            Url = link,
                            IsGlobalNews = false
                        });
                    }

                    if (IranNewsList.Count >= 10) break;
                }
                catch (Exception ex)
                {
                    LoggerService.Error($"[IranNews] Error fetching {sourceName}: {ex.Message}");
                }
            }
        }

        // =========================================================================
        // 2. World Cinema News (ورایتی و ددلاین)
        // =========================================================================
        private async Task LoadWorldCinemaNewsAsync()
        {
            WorldNewsList.Clear();
            var feeds = new[]
            {
                ("https://variety.com/v/film/feed/", "ورایتی (Variety)"),
                ("https://deadline.com/v/film/feed/", "ددلاین (Deadline)")
            };

            foreach (var (feedUrl, sourceName) in feeds)
            {
                try
                {
                    var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(feedUrl));
                    if (!resp.IsSuccessStatusCode) continue;

                    string content = await resp.Content.ReadAsStringAsync();
                    var items = Regex.Matches(content, @"<item>(.*?)</item>", RegexOptions.Singleline);

                    foreach (Match it in items.Take(12))
                    {
                        var tMatch = Regex.Match(it.Value, @"<title>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</title>", RegexOptions.Singleline);
                        var dMatch = Regex.Match(it.Value, @"<description>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</description>", RegexOptions.Singleline);
                        var pMatch = Regex.Match(it.Value, @"<pubDate>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</pubDate>", RegexOptions.Singleline);
                        var lMatch = Regex.Match(it.Value, @"<link>(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?</link>", RegexOptions.Singleline);
                        var mediaMatch = Regex.Match(it.Value, @"<media:content[^>]+url=""([^""]+)""", RegexOptions.IgnoreCase);
                        var enclMatch = Regex.Match(it.Value, @"<enclosure[^>]+url=""([^""]+)""", RegexOptions.IgnoreCase);
                        var imgMatch = Regex.Match(it.Value, @"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase);

                        string title = tMatch.Success ? System.Net.WebUtility.HtmlDecode(tMatch.Groups[1].Value.Trim()) : "";
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        string rawDesc = dMatch.Success ? dMatch.Groups[1].Value : "";
                        string cleanDesc = Regex.Replace(System.Net.WebUtility.HtmlDecode(rawDesc), @"<[^>]+>", "").Trim();
                        cleanDesc = Regex.Replace(cleanDesc, @"\s+", " ");

                        string rawDate = pMatch.Success ? pMatch.Groups[1].Value.Trim() : "";
                        string displayDate = FormatPublishedDate(rawDate);

                        string link = lMatch.Success ? lMatch.Groups[1].Value.Trim() : "";
                        string imgUrl = mediaMatch.Success ? mediaMatch.Groups[1].Value :
                                        (enclMatch.Success ? enclMatch.Groups[1].Value :
                                        (imgMatch.Success ? imgMatch.Groups[1].Value : ""));

                        if (string.IsNullOrEmpty(imgUrl))
                        {
                            imgUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&auto=format&fit=crop&q=80";
                        }

                        WorldNewsList.Add(new CinemaNewsItem
                        {
                            Title = title,
                            Summary = cleanDesc,
                            OriginalTitle = title,
                            OriginalSummary = cleanDesc,
                            Source = sourceName,
                            PublishedAt = displayDate,
                            ImageUrl = SettingsManager.WrapUrlWithProxy(imgUrl),
                            Url = link,
                            IsGlobalNews = true
                        });
                    }

                    if (WorldNewsList.Count >= 10) break;
                }
                catch (Exception ex)
                {
                    LoggerService.Error($"[WorldNews] Error fetching {sourceName}: {ex.Message}");
                }
            }
        }

        private static string FormatPublishedDate(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return "امروز";
            if (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                var diff = DateTime.UtcNow - dt.ToUniversalTime();
                if (diff.TotalHours < 1) return "دقایقی پیش";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ساعت پیش";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} روز پیش";

                var pc = new PersianCalendar();
                return $"{pc.GetYear(dt)}/{pc.GetMonth(dt):D2}/{pc.GetDayOfMonth(dt):D2}";
            }
            return rawDate;
        }

        // =========================================================================
        // 3. Real Box Office (Box Office Mojo Official Live Weekly Grosses)
        // =========================================================================
        private async Task LoadRealBoxOfficeAsync()
        {
            BoxOfficeList.Clear();
            string apiKey = SettingsManager.GetTmdbApiKey();
            bool loadedFromMojo = false;

            try
            {
                string mojoUrl = "https://www.boxofficemojo.com/weekend/chart/";
                var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(mojoUrl));
                if (resp.IsSuccessStatusCode)
                {
                    string html = await resp.Content.ReadAsStringAsync();
                    var rows = Regex.Matches(html, @"<tr[^>]*>.*?</tr>", RegexOptions.Singleline);

                    var parsedMojoItems = new List<(int rank, string title, string weekendGross, string totalGross, int weeks)>();

                    foreach (Match m in rows)
                    {
                        var rankM = Regex.Match(m.Value, @"class=""[^""]*mojo-field-type-rank[^""]*""[^>]*>(\d+)<");
                        var titleM = Regex.Match(m.Value, @"class=""[^""]*mojo-field-type-release[^""]*""[^>]*><a[^>]*>([^<]+)</a>");
                        var moneyMatches = Regex.Matches(m.Value, @"class=""[^""]*mojo-field-type-money[^""]*""[^>]*>(\$[\d,]+)<");
                        var weeksM = Regex.Match(m.Value, @"<td class=""a-text-right mojo-field-type-positive_integer"">(\d+)</td>");

                        if (rankM.Success && titleM.Success && moneyMatches.Count >= 2)
                        {
                            int rank = int.Parse(rankM.Groups[1].Value);
                            string title = System.Net.WebUtility.HtmlDecode(titleM.Groups[1].Value.Trim());
                            string weekend = moneyMatches[0].Groups[1].Value.Trim();
                            string total = moneyMatches[moneyMatches.Count - 1].Groups[1].Value.Trim();
                            int weeks = weeksM.Success ? int.Parse(weeksM.Groups[1].Value) : 1;

                            parsedMojoItems.Add((rank, title, weekend, total, weeks));
                            if (parsedMojoItems.Count >= 10) break;
                        }
                    }

                    if (parsedMojoItems.Count > 0)
                    {
                        foreach (var item in parsedMojoItems)
                        {
                            string posterUrl = "";
                            string faTitle = item.title;
                            double rating = 0;
                            int tmdbId = 0;

                            try
                            {
                                string searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(item.title)}&language=fa-IR";
                                var tmdbResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(searchUrl));
                                if (tmdbResp.IsSuccessStatusCode)
                                {
                                    string sJson = await tmdbResp.Content.ReadAsStringAsync();
                                    using var doc = JsonDocument.Parse(sJson);
                                    if (doc.RootElement.TryGetProperty("results", out var resArr) && resArr.ValueKind == JsonValueKind.Array && resArr.GetArrayLength() > 0)
                                    {
                                        var firstMatch = resArr[0];
                                        tmdbId = firstMatch.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                                        string pPath = firstMatch.TryGetProperty("poster_path", out var pProp) ? pProp.GetString() ?? "" : "";
                                        rating = firstMatch.TryGetProperty("vote_average", out var vProp) ? vProp.GetDouble() : 0;
                                        string tmdbFaTitle = firstMatch.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";

                                        if (!string.IsNullOrWhiteSpace(tmdbFaTitle)) faTitle = tmdbFaTitle;
                                        if (!string.IsNullOrWhiteSpace(pPath)) posterUrl = SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w500{pPath}");
                                    }
                                }
                            }
                            catch { }

                            BoxOfficeList.Add(new BoxOfficeItem
                            {
                                Rank = item.rank,
                                Title = faTitle,
                                OriginalTitle = item.title,
                                WeekendGross = item.weekendGross,
                                TotalGross = item.totalGross,
                                WeeksOut = item.weeks,
                                Rating = Math.Round(rating, 1),
                                PosterUrl = posterUrl,
                                TmdbId = tmdbId
                            });
                        }

                        loadedFromMojo = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[BoxOffice] Error scraping Box Office Mojo: {ex.Message}");
            }

            // Fallback: Query TMDB Now Playing and fetch actual recorded lifetime revenue
            if (!loadedFromMojo || BoxOfficeList.Count == 0)
            {
                try
                {
                    string url = $"https://api.themoviedb.org/3/movie/now_playing?api_key={apiKey}&language=fa-IR&page=1";
                    var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("results", out var results))
                        {
                            int rank = 1;
                            foreach (var item in results.EnumerateArray().Take(10))
                            {
                                int id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                                string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                string origTitle = item.TryGetProperty("original_title", out var ot) ? ot.GetString() ?? "" : "";
                                string poster = item.TryGetProperty("poster_path", out var p) ? p.GetString() ?? "" : "";
                                double rating = item.TryGetProperty("vote_average", out var r) ? r.GetDouble() : 0;

                                string totalGrossStr = "در حال ثبت";
                                string weekendStr = "گیشه روز";
                                try
                                {
                                    string detailUrl = $"https://api.themoviedb.org/3/movie/{id}?api_key={apiKey}&language=en-US";
                                    var detResp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(detailUrl));
                                    if (detResp.IsSuccessStatusCode)
                                    {
                                        string detJson = await detResp.Content.ReadAsStringAsync();
                                        using var detDoc = JsonDocument.Parse(detJson);
                                        long rev = detDoc.RootElement.TryGetProperty("revenue", out var revProp) ? revProp.GetInt64() : 0;
                                        if (rev > 0)
                                        {
                                            totalGrossStr = $"${rev:N0}";
                                            weekendStr = $"${(rev / 6):N0}";
                                        }
                                    }
                                }
                                catch { }

                                BoxOfficeList.Add(new BoxOfficeItem
                                {
                                    Rank = rank++,
                                    TmdbId = id,
                                    Title = title,
                                    OriginalTitle = origTitle,
                                    WeekendGross = weekendStr,
                                    TotalGross = totalGrossStr,
                                    WeeksOut = 1,
                                    Rating = Math.Round(rating, 1),
                                    PosterUrl = !string.IsNullOrEmpty(poster) ? SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w500{poster}") : ""
                                });
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // =========================================================================
        // 4. Real Upcoming Releases (Strictly Future: DaysRemaining > 0 up to 1 year)
        // =========================================================================
        private async Task LoadRealUpcomingAsync()
        {
            UpcomingList.Clear();
            string apiKey = SettingsManager.GetTmdbApiKey();
            var pc = new PersianCalendar();
            var list = new List<UpcomingItem>();
            var seenIds = new HashSet<int>();

            for (int page = 1; page <= 2; page++)
            {
                try
                {
                    string url = $"https://api.themoviedb.org/3/movie/upcoming?api_key={apiKey}&language=fa-IR&page={page}";
                    var resp = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url));
                    if (!resp.IsSuccessStatusCode) break;

                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var item in results.EnumerateArray())
                        {
                            int id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                            if (id == 0 || seenIds.Contains(id)) continue;

                            string releaseDate = item.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "";
                            if (!DateTime.TryParse(releaseDate, out var dt)) continue;

                            // STRICT FILTER: Only strictly future dates (tomorrow onwards up to 365 days)
                            int days = (int)(dt.Date - DateTime.Today).TotalDays;
                            if (days <= 0 || days > 365) continue;

                            seenIds.Add(id);
                            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                            string overview = item.TryGetProperty("overview", out var o) ? o.GetString() ?? "" : "";
                            string poster = item.TryGetProperty("poster_path", out var p) ? p.GetString() ?? "" : "";
                            string backdrop = item.TryGetProperty("backdrop_path", out var b) ? b.GetString() ?? "" : "";

                            string jalaliDate = $"{pc.GetYear(dt)}/{pc.GetMonth(dt):D2}/{pc.GetDayOfMonth(dt):D2}";

                            list.Add(new UpcomingItem
                            {
                                TmdbId = id,
                                Title = title,
                                ReleaseDate = jalaliDate,
                                JalaliReleaseDate = jalaliDate,
                                DaysRemaining = days,
                                PosterUrl = !string.IsNullOrEmpty(poster) ? SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w500{poster}") : "",
                                BackdropUrl = !string.IsNullOrEmpty(backdrop) ? SettingsManager.WrapUrlWithProxy($"https://image.tmdb.org/t/p/w780{backdrop}") : "",
                                Overview = !string.IsNullOrWhiteSpace(overview) ? overview : "به زودی در سینماهای سراسر جهان اکران خواهد شد."
                            });
                        }
                    }
                }
                catch { }
            }

            foreach (var up in list.OrderBy(u => u.DaysRemaining))
            {
                UpcomingList.Add(up);
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel()));
        }
    }
}
