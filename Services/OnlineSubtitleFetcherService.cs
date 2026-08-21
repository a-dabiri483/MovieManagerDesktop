using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class OnlineSubtitleResultModel
    {
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Source { get; set; } = "SubDL";
        public bool IsSeasonPack { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string Author { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public bool IsPersian => Language.Contains("فارسی") || Language.ToLowerInvariant().Contains("fa") || Language.ToLowerInvariant().Contains("persian");
    }

    public static class OnlineSubtitleFetcherService
    {
        public const string DEFAULT_SUBDL_KEY = "subdl_HHtBliLNdNumqWs29n7Z4E9GLQwyX0bL9MDFc6RTy34";
        public const string DEFAULT_SUBSOURCE_KEY = "sk_68d68b32ef82a0a168e243815c66d85ca5ecfe2909507245e8ff695b27c10025";

        private static readonly HttpClient _httpClient = new HttpClient(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static async Task<List<OnlineSubtitleResultModel>> SearchOnlineSubtitlesAsync(
            string query,
            string? videoPath = null,
            string language = "ALL")
        {
            var results = new List<OnlineSubtitleResultModel>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            // Extract Season & Episode if present (e.g. S01E03 or S1 E3)
            var sEpMatch = Regex.Match(query, @"(?i)s(\d+)\s*e(\d+)");
            int? season = sEpMatch.Success && int.TryParse(sEpMatch.Groups[1].Value, out int sVal) ? sVal : null;
            int? episode = sEpMatch.Success && int.TryParse(sEpMatch.Groups[2].Value, out int eVal) ? eVal : null;

            string cleanTitle = Regex.Replace(query, @"(?i)s\d+\s*e\d+", "")
                                     .Replace(".", " ")
                                     .Replace("-", " ")
                                     .Replace("_", " ")
                                     .Trim();

            string encodedTitle = Uri.EscapeDataString(cleanTitle);

            string subdlLangs = language switch
            {
                "FA" => "FA",
                "EN" => "EN",
                "AR" => "AR",
                "TR" => "TR",
                "AZ" => "AZ",
                _ => "FA,EN,AR,TR,AZ"
            };

            // ══════════════ 1. SOURCE 1: SubDL API ══════════════
            try
            {
                var sb = new StringBuilder($"https://api.subdl.com/api/v1/subtitles?film_name={encodedTitle}&languages={subdlLangs}&api_key={DEFAULT_SUBDL_KEY}&subs_per_page=30");
                if (season != null)
                {
                    sb.Append($"&season_number={season}&type=tv");
                }
                if (episode != null)
                {
                    sb.Append($"&episode_number={episode}");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, sb.ToString());
                request.Headers.Add("User-Agent", "MovieManagerDesktop/1.2.0 Windows");

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string jsonStr = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var statusProp) && statusProp.GetBoolean())
                    {
                        if (root.TryGetProperty("subtitles", out var subsArray) && subsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in subsArray.EnumerateArray())
                            {
                                string releaseName = item.TryGetProperty("release_name", out var rel) && rel.ValueKind == JsonValueKind.String ? rel.GetString() ?? "" : "";
                                string name = item.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String ? nm.GetString() ?? releaseName : releaseName;
                                string lang = item.TryGetProperty("lang", out var lg) && lg.ValueKind == JsonValueKind.String ? lg.GetString() ?? "EN" : "EN";
                                string urlPath = item.TryGetProperty("url", out var ul) && ul.ValueKind == JsonValueKind.String ? ul.GetString() ?? "" : "";
                                int? subSeason = item.TryGetProperty("season", out var sn) && sn.ValueKind == JsonValueKind.Number && sn.GetInt32() > 0 ? sn.GetInt32() : (int?)null;
                                int? subEpisode = item.TryGetProperty("episode", out var ep) && ep.ValueKind == JsonValueKind.Number && ep.GetInt32() > 0 ? ep.GetInt32() : (int?)null;
                                bool hi = item.TryGetProperty("hi", out var hiProp) && hiProp.ValueKind == JsonValueKind.True;
                                string author = item.TryGetProperty("author", out var auth) && auth.ValueKind == JsonValueKind.String ? auth.GetString() ?? "" : "";

                                if (!string.IsNullOrEmpty(urlPath))
                                {
                                    string fullUrl = urlPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? urlPath : $"https://dl.subdl.com{urlPath}";
                                    bool isSeasonPack = subEpisode == null && subSeason != null;

                                    string langLower = lang.ToLowerInvariant();
                                    string langLabel = (langLower.Contains("fa") || langLower.Contains("per") || langLower.Contains("farsi"))
                                        ? "🇮🇷 فارسی"
                                        : (langLower.Contains("en") || langLower.Contains("eng"))
                                            ? "🇬🇧 English"
                                            : lang;

                                    string displayTitle = (string.IsNullOrWhiteSpace(releaseName) ? name : releaseName).Trim();
                                    if (hi) displayTitle += " [HI]";
                                    if (isSeasonPack) displayTitle += " [پک کامل فصل]";
                                    if (!string.IsNullOrWhiteSpace(author)) displayTitle += $" — {author}";

                                    if (!results.Any(r => r.DownloadUrl == fullUrl))
                                    {
                                        results.Add(new OnlineSubtitleResultModel
                                        {
                                            Title = displayTitle,
                                            Language = langLabel,
                                            DownloadUrl = fullUrl,
                                            Source = "SubDL",
                                            IsSeasonPack = isSeasonPack,
                                            Season = subSeason,
                                            Episode = subEpisode,
                                            Author = author,
                                            ReleaseName = releaseName
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("SubDL search failed", ex);
            }

            // ══════════════ 2. SOURCE 2: SubSource REST API ══════════════
            if (results.Count < 10)
            {
                try
                {
                    string ssLang = language switch
                    {
                        "FA" => "persian",
                        "EN" => "english",
                        "AR" => "arabic",
                        "TR" => "turkish",
                        "AZ" => "azerbaijani",
                        _ => "persian"
                    };

                    string searchUrl = $"https://api.subsource.net/api/v1/movies/search?searchType=text&q={encodedTitle}";
                    using var ssSearchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    ssSearchReq.Headers.Add("X-API-Key", DEFAULT_SUBSOURCE_KEY);
                    ssSearchReq.Headers.Add("Accept", "application/json");

                    using var ssSearchRes = await _httpClient.SendAsync(ssSearchReq);
                    if (ssSearchRes.IsSuccessStatusCode)
                    {
                        string ssSearchJson = await ssSearchRes.Content.ReadAsStringAsync();
                        using var ssSearchDoc = JsonDocument.Parse(ssSearchJson);
                        if (ssSearchDoc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array && dataArr.GetArrayLength() > 0)
                        {
                            var firstMovie = dataArr[0];
                            if (firstMovie.TryGetProperty("movieId", out var movieIdProp) && movieIdProp.GetInt32() > 0)
                            {
                                int movieId = movieIdProp.GetInt32();
                                string subUrl = $"https://api.subsource.net/api/v1/subtitles?movieId={movieId}&language={ssLang}";
                                using var subReq = new HttpRequestMessage(HttpMethod.Get, subUrl);
                                subReq.Headers.Add("X-API-Key", DEFAULT_SUBSOURCE_KEY);
                                subReq.Headers.Add("Accept", "application/json");

                                using var subRes = await _httpClient.SendAsync(subReq);
                                if (subRes.IsSuccessStatusCode)
                                {
                                    string subJson = await subRes.Content.ReadAsStringAsync();
                                    using var subDoc = JsonDocument.Parse(subJson);
                                    if (subDoc.RootElement.TryGetProperty("data", out var subsList) && subsList.ValueKind == JsonValueKind.Array)
                                    {
                                        int count = 0;
                                        foreach (var subObj in subsList.EnumerateArray())
                                        {
                                            if (count++ >= 15) break;
                                            if (subObj.TryGetProperty("subtitleId", out var subIdProp) && subIdProp.GetInt32() > 0)
                                            {
                                                int subtitleId = subIdProp.GetInt32();
                                                string relInfo = "Subtitle";
                                                if (subObj.TryGetProperty("releaseInfo", out var relArr) && relArr.ValueKind == JsonValueKind.Array && relArr.GetArrayLength() > 0)
                                                {
                                                    relInfo = relArr[0].GetString() ?? "Subtitle";
                                                }

                                                string langLabel = ssLang == "persian" ? "🇮🇷 فارسی" : "🇬🇧 English";
                                                string dlUrl = $"https://api.subsource.net/api/v1/subtitles/{subtitleId}/download";

                                                if (!results.Any(r => r.DownloadUrl == dlUrl))
                                                {
                                                    results.Add(new OnlineSubtitleResultModel
                                                    {
                                                        Title = relInfo,
                                                        Language = langLabel,
                                                        DownloadUrl = dlUrl,
                                                        Source = "SubSource",
                                                        ReleaseName = relInfo
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
                    LoggerService.Error("SubSource search failed", ex);
                }
            }

            // Sort: Persian first, then episode match, then standard releases
            return results
                .OrderByDescending(r => r.IsPersian)
                .ThenByDescending(r => episode.HasValue && r.Episode == episode)
                .ThenBy(r => r.IsSeasonPack)
                .ToList();
        }

        public static async Task<(bool success, string? filePath, string message)> DownloadSubtitleAsync(
            OnlineSubtitleResultModel item,
            string? videoPath = null)
        {
            try
            {
                string currentUrl = item.DownloadUrl;
                byte[]? rawBytes = null;
                int redirectCount = 0;

                while (redirectCount < 5)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    if (currentUrl.StartsWith("https://api.subsource.net/", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Headers.Add("X-API-Key", DEFAULT_SUBSOURCE_KEY);
                    }

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (response.StatusCode == HttpStatusCode.Moved ||
                        response.StatusCode == HttpStatusCode.MovedPermanently ||
                        response.StatusCode == HttpStatusCode.Redirect ||
                        (int)response.StatusCode == 307 ||
                        (int)response.StatusCode == 308)
                    {
                        var location = response.Headers.Location;
                        if (location != null)
                        {
                            currentUrl = location.IsAbsoluteUri ? location.AbsoluteUri : new Uri(new Uri(currentUrl), location).AbsoluteUri;
                            redirectCount++;
                            continue;
                        }
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        rawBytes = await response.Content.ReadAsByteArrayAsync();
                        break;
                    }
                    else
                    {
                        return (false, null, $"خطا در اتصال به سرور دانلود (کد {(int)response.StatusCode})");
                    }
                }

                if (rawBytes == null || rawBytes.Length == 0)
                {
                    return (false, null, "فایل زیرنویس خالی یا نامعتبر بود.");
                }

                // Determine destination directory and standard naming
                string cacheDir = Path.Combine(Path.GetTempPath(), "MovieManagerDesktop", "OnlineSubtitles");
                Directory.CreateDirectory(cacheDir);

                string? videoDir = !string.IsNullOrEmpty(videoPath) && File.Exists(videoPath)
                    ? Path.GetDirectoryName(videoPath)
                    : null;

                string targetDir = videoDir ?? cacheDir;
                string videoBaseName = !string.IsNullOrEmpty(videoPath) && File.Exists(videoPath)
                    ? Path.GetFileNameWithoutExtension(videoPath)
                    : "Subtitle";

                string langTag = item.IsPersian ? "fa" : (item.Language.ToLowerInvariant().Contains("en") ? "en" : "sub");
                string cleanSubTitle = Regex.Replace(item.Title, @"[^a-zA-Z0-9_\u0600-\u06FF\.-]", "_").Trim('_');
                if (cleanSubTitle.Length > 40) cleanSubTitle = cleanSubTitle.Substring(0, 40);

                string targetSrtFile = Path.Combine(targetDir, $"{videoBaseName}.{langTag}_{item.Source}_{cleanSubTitle}.srt");

                // 1. GZIP Archive (0x1f 0x8b)
                if (rawBytes.Length > 2 && rawBytes[0] == 0x1f && rawBytes[1] == 0x8b)
                {
                    byte[] decompressed = DecompressGzip(rawBytes);
                    string utf8Text = SubtitleTranslatorService.DecodeBytesToUtf8(decompressed);
                    await File.WriteAllTextAsync(targetSrtFile, utf8Text, Encoding.UTF8);
                    return (true, targetSrtFile, "زیرنویس با موفقیت استخراج و فعال شد.");
                }

                // 2. ZIP Archive ('P' 'K')
                if (rawBytes.Length > 4 && rawBytes[0] == (byte)'P' && rawBytes[1] == (byte)'K')
                {
                    var zipResult = ExtractZip(rawBytes, targetDir, videoBaseName, cleanSubTitle, langTag, item.Episode);
                    if (zipResult.success && !string.IsNullOrEmpty(zipResult.filePath))
                    {
                        return (true, zipResult.filePath, "زیرنویس با موفقیت از فایل ZIP استخراج و فعال شد.");
                    }
                    return (false, null, zipResult.message);
                }

                // 3. Plain Text Subtitle (.srt / .vtt / .ass)
                string plainText = SubtitleTranslatorService.DecodeBytesToUtf8(rawBytes);
                await File.WriteAllTextAsync(targetSrtFile, plainText, Encoding.UTF8);
                return (true, targetSrtFile, "زیرنویس با موفقیت ذخیره و فعال شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to download online subtitle", ex);
                return (false, null, $"خطا در دانلود زیرنویس: {ex.Message}");
            }
        }

        private static byte[] DecompressGzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static (bool success, string? filePath, string message) ExtractZip(
            byte[] zipBytes,
            string targetDir,
            string videoBaseName,
            string cleanSubTitle,
            string langTag,
            int? targetEpisode)
        {
            try
            {
                using var stream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                var subtitleEntries = archive.Entries
                    .Where(e => !string.IsNullOrEmpty(e.Name) &&
                               (e.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                                e.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
                                e.Name.EndsWith(".ass", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (subtitleEntries.Count == 0)
                {
                    return (false, null, "هیچ فایل زیرنویسی در فایل ZIP یافت نشد.");
                }

                // Single Subtitle in ZIP
                if (subtitleEntries.Count == 1)
                {
                    var entry = subtitleEntries[0];
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);
                    string utf8Text = SubtitleTranslatorService.DecodeBytesToUtf8(ms.ToArray());
                    string outFile = Path.Combine(targetDir, $"{videoBaseName}.{langTag}_{cleanSubTitle}.srt");
                    File.WriteAllText(outFile, utf8Text, Encoding.UTF8);
                    return (true, outFile, "زیرنویس استخراج شد.");
                }

                // Season Pack (Multiple Subtitles)
                string seasonPackDir = Path.Combine(targetDir, $"{videoBaseName}_SeasonPack");
                Directory.CreateDirectory(seasonPackDir);

                var extractedFiles = new List<string>();
                foreach (var entry in subtitleEntries)
                {
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);
                    string utf8Text = SubtitleTranslatorService.DecodeBytesToUtf8(ms.ToArray());
                    string entryFileName = Path.GetFileName(entry.Name);
                    string outFile = Path.Combine(seasonPackDir, entryFileName);
                    File.WriteAllText(outFile, utf8Text, Encoding.UTF8);
                    extractedFiles.Add(outFile);
                }

                // Auto-match current episode if known
                if (targetEpisode.HasValue)
                {
                    string padEp = targetEpisode.Value.ToString("D2");
                    string rawEp = targetEpisode.Value.ToString();
                    var matched = extractedFiles.FirstOrDefault(f =>
                    {
                        string nameLower = Path.GetFileName(f).ToLowerInvariant();
                        return nameLower.Contains($"e{padEp}") ||
                               nameLower.Contains($"e{rawEp}") ||
                               nameLower.Contains($"ep{padEp}") ||
                               nameLower.Contains($"episode.{padEp}") ||
                               nameLower.Contains($"episode.{rawEp}");
                    });

                    if (matched != null)
                    {
                        // Also copy as direct episode sub beside video
                        string episodeDirectSub = Path.Combine(targetDir, $"{videoBaseName}.{langTag}.srt");
                        try { File.Copy(matched, episodeDirectSub, true); return (true, episodeDirectSub, $"پک فصل استخراج شد و قسمت {targetEpisode.Value} فعال گردید."); }
                        catch { return (true, matched, $"پک فصل استخراج شد و قسمت {targetEpisode.Value} فعال گردید."); }
                    }
                }

                return (true, extractedFiles.First(), "پک فصل استخراج و اولین قسمت فعال شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to extract ZIP archive", ex);
                return (false, null, $"خطا در استخراج فایل فشرده: {ex.Message}");
            }
        }
    }
}
