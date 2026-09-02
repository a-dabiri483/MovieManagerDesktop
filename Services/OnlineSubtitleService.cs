using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class OnlineSubtitleItem
    {
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = "🇮🇷 فارسی";
        public string LanguageCode { get; set; } = "fa";
        public string DownloadUrl { get; set; } = string.Empty;
        public string Source { get; set; } = "SubDL";
        public string? ReleaseInfo { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public bool IsSeasonPack { get; set; }
        public int? OpenSubFileId { get; set; }

        public string DisplayText => $"{Language} | {Title} ({Source})";
    }

    public static class OnlineSubtitleService
    {
        public const string SUBDL_DEFAULT_KEY = "subdl_HHtBliLNdNumqWs29n7Z4E9GLQwyX0bL9MDFc6RTy34";
        public const string SUBSOURCE_DEFAULT_KEY = "sk_68d68b32ef82a0a168e243815c66d85ca5ecfe2909507245e8ff695b27c10025";
        public const string OPENSUBTITLES_DEFAULT_KEY = "tf6Ebu6rUqT662SZlDWYWw5yJkS9Gz2g";

        private static readonly HttpClient _httpClient = new(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static OnlineSubtitleService()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "MovieManagerDesktop/2.5");
            }
        }

        public static (string cleanTitle, int? season, int? episode) ParseSearchQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return ("", null, null);

            int? season = null;
            int? episode = null;

            // Match S01E01, S1E1, 1x01, Season 1 Episode 1, فصل 1 قسمت 1
            var sEpMatch = Regex.Match(input, @"(?:[sS](\d+)\s*[eE](\d+)|(?:(\d+)x(\d+))|(?:فصل\s*(\d+)\s*قسمت\s*(\d+))|(?:Season\s*(\d+)\s*Episode\s*(\d+)))", RegexOptions.IgnoreCase);
            if (sEpMatch.Success)
            {
                if (int.TryParse(sEpMatch.Groups[1].Value, out int s)) season = s;
                else if (int.TryParse(sEpMatch.Groups[3].Value, out int s2)) season = s2;
                else if (int.TryParse(sEpMatch.Groups[5].Value, out int s3)) season = s3;
                else if (int.TryParse(sEpMatch.Groups[7].Value, out int s4)) season = s4;

                if (int.TryParse(sEpMatch.Groups[2].Value, out int e)) episode = e;
                else if (int.TryParse(sEpMatch.Groups[4].Value, out int e2)) episode = e2;
                else if (int.TryParse(sEpMatch.Groups[6].Value, out int e3)) episode = e3;
                else if (int.TryParse(sEpMatch.Groups[8].Value, out int e4)) episode = e4;
            }
            else
            {
                var sOnly = Regex.Match(input, @"(?:[sS]|Season\s*|فصل\s*)(\d+)", RegexOptions.IgnoreCase);
                if (sOnly.Success && int.TryParse(sOnly.Groups[1].Value, out int sVal)) season = sVal;

                var eOnly = Regex.Match(input, @"(?:[eE]|Episode\s*|قسمت\s*)(\d+)", RegexOptions.IgnoreCase);
                if (eOnly.Success && int.TryParse(eOnly.Groups[1].Value, out int eVal)) episode = eVal;
            }

            // Strip video quality, codecs, release groups
            string cleaned = Regex.Replace(input, @"(?i)\b(?:1080p|720p|480p|2160p|4k|uhd|bluray|bdrip|brrip|web-dl|webrip|web|hdtv|dvdrip|x264|x265|hevc|h264|h265|aac|dts|ac3|yify|pahe|psa|rarbg|eztv|galaxytv|amzn|nf|dsnp|proper|repack|remux|hdr|10bit|60fps|dual-audio|dubbed|farsi|persian|sub|softsub)\b", " ");
            cleaned = Regex.Replace(cleaned, @"(?i)(?:[sS]\d+\s*[eE]\d+|\d+x\d+|Season\s*\d+|Episode\s*\d+|فصل\s*\d+|قسمت\s*\d+)", " ");
            cleaned = Regex.Replace(cleaned, @"[\.\[\]\(\)\-_]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return (cleaned, season, episode);
        }

        public static async Task<List<OnlineSubtitleItem>> SearchSubtitlesAsync(
            string rawQuery, 
            string language = "ALL", 
            CancellationToken ct = default)
        {
            var results = new List<OnlineSubtitleItem>();
            if (string.IsNullOrWhiteSpace(rawQuery)) return results;

            var (cleanTitle, season, episode) = ParseSearchQuery(rawQuery);
            if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = rawQuery.Trim();

            string subdlLangs = language switch
            {
                "FA" => "FA",
                "EN" => "EN",
                "AR" => "AR",
                _ => "FA,EN"
            };

            // ==========================================
            // 1. SubDL API (Search by Show Name + S/E)
            // ==========================================
            try
            {
                var subdlQueries = new List<string>();
                string encTitle = Uri.EscapeDataString(cleanTitle);

                // Query 1: Exact Episode
                if (season != null && episode != null)
                {
                    subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&type=tv&season_number={season}&episode_number={episode}&languages={subdlLangs}&api_key={SUBDL_DEFAULT_KEY}&subs_per_page=30");
                }
                // Query 2: Season Pack / General Show
                if (season != null)
                {
                    subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&type=tv&season_number={season}&languages={subdlLangs}&api_key={SUBDL_DEFAULT_KEY}&subs_per_page=30");
                }
                // Query 3: Pure Title
                subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&languages={subdlLangs}&api_key={SUBDL_DEFAULT_KEY}&subs_per_page=30");

                foreach (var url in subdlQueries)
                {
                    if (results.Count >= 25) break;

                    try
                    {
                        var response = await _httpClient.GetAsync(SettingsManager.WrapUrlWithProxy(url), ct);
                        if (!response.IsSuccessStatusCode) continue;

                        string json = await response.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("status", out var st) && st.GetBoolean() &&
                            root.TryGetProperty("subtitles", out var subsArray) && subsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var sub in subsArray.EnumerateArray())
                            {
                                string releaseName = sub.TryGetProperty("release_name", out var rn) ? rn.GetString() ?? "" : "";
                                string langCode = sub.TryGetProperty("lang", out var l) ? l.GetString() ?? "FA" : "FA";
                                string urlPath = sub.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                                if (string.IsNullOrEmpty(urlPath)) continue;

                                string fullDownloadUrl = urlPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                    ? urlPath
                                    : $"https://dl.subdl.com{urlPath}";

                                if (results.Any(r => r.DownloadUrl == fullDownloadUrl)) continue;

                                int? epNum = sub.TryGetProperty("episode", out var epP) && epP.ValueKind == JsonValueKind.Number ? epP.GetInt32() : null;
                                int? sNum = sub.TryGetProperty("season", out var sP) && sP.ValueKind == JsonValueKind.Number ? sP.GetInt32() : null;

                                string langLabel = langCode.Equals("FA", StringComparison.OrdinalIgnoreCase) || langCode.Equals("Farsi", StringComparison.OrdinalIgnoreCase) || langCode.Equals("Persian", StringComparison.OrdinalIgnoreCase)
                                    ? "🇮🇷 فارسی"
                                    : (langCode.Equals("EN", StringComparison.OrdinalIgnoreCase) ? "🇬🇧 English" : langCode);

                                results.Add(new OnlineSubtitleItem
                                {
                                    Title = string.IsNullOrWhiteSpace(releaseName) ? cleanTitle : releaseName,
                                    Language = langLabel,
                                    LanguageCode = langCode.ToLowerInvariant(),
                                    DownloadUrl = fullDownloadUrl,
                                    Source = "SubDL",
                                    Season = sNum,
                                    Episode = epNum,
                                    ReleaseInfo = releaseName
                                });
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[Subtitles] Error querying SubDL: {ex.Message}");
            }

            // ==========================================
            // 2. SubSource API
            // ==========================================
            if (results.Count < 20)
            {
                try
                {
                    string ssLang = language == "EN" ? "english" : "persian";
                    string searchUrl = $"https://api.subsource.net/api/v1/movies/search?searchType=text&q={Uri.EscapeDataString(cleanTitle)}";

                    using var req = new HttpRequestMessage(HttpMethod.Get, SettingsManager.WrapUrlWithProxy(searchUrl));
                    req.Headers.Add("X-API-Key", SUBSOURCE_DEFAULT_KEY);
                    req.Headers.Add("Accept", "application/json");

                    var response = await _httpClient.SendAsync(req, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                        {
                            var movies = dataArr.EnumerateArray().ToList();
                            if (movies.Any())
                            {
                                int movieId = movies[0].TryGetProperty("movieId", out var mId) ? mId.GetInt32() : -1;
                                if (movieId > 0)
                                {
                                    string subUrl = $"https://api.subsource.net/api/v1/subtitles?movieId={movieId}&language={ssLang}";
                                    using var subReq = new HttpRequestMessage(HttpMethod.Get, SettingsManager.WrapUrlWithProxy(subUrl));
                                    subReq.Headers.Add("X-API-Key", SUBSOURCE_DEFAULT_KEY);
                                    subReq.Headers.Add("Accept", "application/json");

                                    var subResp = await _httpClient.SendAsync(subReq, ct);
                                    if (subResp.IsSuccessStatusCode)
                                    {
                                        string subJson = await subResp.Content.ReadAsStringAsync(ct);
                                        using var subDoc = JsonDocument.Parse(subJson);
                                        if (subDoc.RootElement.TryGetProperty("data", out var subItems) && subItems.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var subItem in subItems.EnumerateArray().Take(20))
                                            {
                                                int subtitleId = subItem.TryGetProperty("subtitleId", out var sid) ? sid.GetInt32() : -1;
                                                if (subtitleId <= 0) continue;

                                                string releaseName = "SubSource Subtitle";
                                                if (subItem.TryGetProperty("releaseInfo", out var relArr) && relArr.ValueKind == JsonValueKind.Array)
                                                {
                                                    var firstRel = relArr.EnumerateArray().FirstOrDefault();
                                                    if (firstRel.ValueKind == JsonValueKind.String)
                                                        releaseName = firstRel.GetString() ?? releaseName;
                                                }

                                                string dlUrl = $"https://api.subsource.net/api/v1/subtitles/{subtitleId}/download";
                                                string langLabel = ssLang == "persian" ? "🇮🇷 فارسی" : "🇬🇧 English";

                                                if (results.All(r => r.DownloadUrl != dlUrl))
                                                {
                                                    results.Add(new OnlineSubtitleItem
                                                    {
                                                        Title = releaseName,
                                                        Language = langLabel,
                                                        LanguageCode = ssLang == "persian" ? "fa" : "en",
                                                        DownloadUrl = dlUrl,
                                                        Source = "SubSource",
                                                        ReleaseInfo = releaseName
                                                    });
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
                    LoggerService.Error($"[Subtitles] Error querying SubSource: {ex.Message}");
                }
            }

            // ==========================================
            // 3. OpenSubtitles.com API (Worldwide DB)
            // ==========================================
            if (results.Count < 20)
            {
                try
                {
                    string osLang = language == "FA" ? "fa" : (language == "EN" ? "en" : "fa,en");
                    string osUrl = $"https://api.opensubtitles.com/api/v1/subtitles?query={Uri.EscapeDataString(cleanTitle)}&languages={osLang}";
                    if (season != null) osUrl += $"&season_number={season}";
                    if (episode != null) osUrl += $"&episode_number={episode}";

                    using var osReq = new HttpRequestMessage(HttpMethod.Get, SettingsManager.WrapUrlWithProxy(osUrl));
                    osReq.Headers.Add("Api-Key", OPENSUBTITLES_DEFAULT_KEY);
                    osReq.Headers.Add("User-Agent", "MovieManagerDesktop v2.5");

                    var osResp = await _httpClient.SendAsync(osReq, ct);
                    if (osResp.IsSuccessStatusCode)
                    {
                        string osJson = await osResp.Content.ReadAsStringAsync(ct);
                        using var osDoc = JsonDocument.Parse(osJson);
                        if (osDoc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var osItem in dataArr.EnumerateArray().Take(15))
                            {
                                if (osItem.TryGetProperty("attributes", out var attr))
                                {
                                    string langCode = attr.TryGetProperty("language", out var l) ? l.GetString() ?? "fa" : "fa";
                                    string releaseName = attr.TryGetProperty("release", out var r) ? r.GetString() ?? "" : "";
                                    
                                    int fileId = -1;
                                    if (attr.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array && filesArr.GetArrayLength() > 0)
                                    {
                                        fileId = filesArr[0].TryGetProperty("file_id", out var fId) ? fId.GetInt32() : -1;
                                    }

                                    if (fileId > 0)
                                    {
                                        string langLabel = langCode.Equals("fa", StringComparison.OrdinalIgnoreCase) ? "🇮🇷 فارسی" : "🇬🇧 English";
                                        string titleText = string.IsNullOrWhiteSpace(releaseName) ? cleanTitle : releaseName;

                                        results.Add(new OnlineSubtitleItem
                                        {
                                            Title = titleText,
                                            Language = langLabel,
                                            LanguageCode = langCode.ToLowerInvariant(),
                                            DownloadUrl = $"https://api.opensubtitles.com/api/v1/download",
                                            OpenSubFileId = fileId,
                                            Source = "OpenSubtitles",
                                            Season = season,
                                            Episode = episode,
                                            ReleaseInfo = releaseName
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // ==========================================
            // Intelligent Ranking & Sorting
            // ==========================================
            return results
                .OrderByDescending(r => r.Language.Contains("فارسی"))
                .ThenByDescending(r => episode != null && r.Episode == episode)
                .ThenByDescending(r => episode != null && (r.ReleaseInfo?.Contains($"E{episode:D2}", StringComparison.OrdinalIgnoreCase) == true || r.ReleaseInfo?.Contains($"E{episode}", StringComparison.OrdinalIgnoreCase) == true))
                .ToList();
        }

        public static async Task<string> DownloadAndSaveSubtitleAsync(
            OnlineSubtitleItem item, 
            string targetVideoPath, 
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(item.DownloadUrl))
                throw new ArgumentException("آدرس دانلود زیرنویس نامعتبر است.");

            string videoDir = Path.GetDirectoryName(targetVideoPath)!;
            string videoNameWithoutExt = Path.GetFileNameWithoutExtension(targetVideoPath);
            string targetSrtPath = Path.Combine(videoDir, $"{videoNameWithoutExt}.{(item.LanguageCode.Contains("fa") ? "fa" : item.LanguageCode)}.srt");

            byte[] rawBytes;

            if (item.Source == "OpenSubtitles" && item.OpenSubFileId.HasValue)
            {
                using var osReq = new HttpRequestMessage(HttpMethod.Post, SettingsManager.WrapUrlWithProxy("https://api.opensubtitles.com/api/v1/download"));
                osReq.Headers.Add("Api-Key", OPENSUBTITLES_DEFAULT_KEY);
                osReq.Headers.Add("User-Agent", "MovieManagerDesktop v2.5");
                osReq.Content = new StringContent(JsonSerializer.Serialize(new { file_id = item.OpenSubFileId.Value }), Encoding.UTF8, "application/json");

                var osResp = await _httpClient.SendAsync(osReq, ct);
                osResp.EnsureSuccessStatusCode();

                string osJson = await osResp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(osJson);
                string directDlLink = doc.RootElement.GetProperty("link").GetString()!;

                var dlResp = await _httpClient.GetAsync(directDlLink, ct);
                dlResp.EnsureSuccessStatusCode();
                rawBytes = await dlResp.Content.ReadAsByteArrayAsync(ct);
            }
            else
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, item.DownloadUrl);
                if (item.Source == "SubSource")
                {
                    req.Headers.Add("X-API-Key", SUBSOURCE_DEFAULT_KEY);
                }

                var response = await _httpClient.SendAsync(req, ct);
                response.EnsureSuccessStatusCode();
                rawBytes = await response.Content.ReadAsByteArrayAsync(ct);
            }

            byte[] srtBytes = ExtractSubtitleBytes(rawBytes);
            string cleanSubtitleText = FixEncodingToPersianUtf8(srtBytes);
            await File.WriteAllTextAsync(targetSrtPath, cleanSubtitleText, Encoding.UTF8, ct);

            return targetSrtPath;
        }

        private static byte[] ExtractSubtitleBytes(byte[] rawData)
        {
            if (rawData.Length > 4 && rawData[0] == 0x50 && rawData[1] == 0x4B)
            {
                using var ms = new MemoryStream(rawData);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                                                                e.FullName.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
                                                                e.FullName.EndsWith(".ass", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    using var entryStream = entry.Open();
                    using var outMs = new MemoryStream();
                    entryStream.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }

            if (rawData.Length > 2 && rawData[0] == 0x1F && rawData[1] == 0x8B)
            {
                using var ms = new MemoryStream(rawData);
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                gzip.CopyTo(outMs);
                return outMs.ToArray();
            }

            return rawData;
        }

        public static string FixEncodingToPersianUtf8(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            string utf8Text = Encoding.UTF8.GetString(bytes);
            if (!utf8Text.Contains('\uFFFD') && Regex.IsMatch(utf8Text, @"[\u0600-\u06FF]"))
            {
                return utf8Text;
            }

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var win1256 = Encoding.GetEncoding(1256);
                string win1256Text = win1256.GetString(bytes);

                if (Regex.IsMatch(win1256Text, @"[\u0600-\u06FF]"))
                {
                    return win1256Text;
                }
            }
            catch { }

            return utf8Text;
        }
    }
}
