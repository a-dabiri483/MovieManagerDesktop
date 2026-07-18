using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class OrganizerProgressReport
    {
        public OrganizerItem Item { get; init; } = null!;
        public OrganizerStatus Status { get; init; }
        public string? ErrorMessage { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public class SeriesOrganizerService
    {
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv",
            ".m4v", ".ts", ".m2ts", ".webm", ".rmvb", ".rm"
        };

        private static readonly Regex[] SeriesPatterns =
        {
            new Regex(@"^(.+?)[.\s_\-]+[Ss]\d{1,2}[Ee]\d{1,2}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(.+?)[.\s_\-]+\d{1,2}[xX]\d{2}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(.+?)[.\s_\-]+[Ss]eason[.\s_\-]*\d{1,2}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(.+?)[.\s_\-]+[Ee][Pp]?\d{2,3}(?:[.\s_\-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex QualityPattern = new Regex(
            @"\s*(1080p|720p|480p|4K|2160p|BluRay|BRRip|WEBRip|WEB[\-\.]DL|HDTV|DVDRip|" +
            @"x264|x265|HEVC|H\.264|H\.265|AAC|AC3|DTS|Remux|PROPER|REPACK|" +
            @"YIFY|YTS|RARBG|EZTV|TGx|GalaxyTV|FGT|NTG|Pahe|ION10|NF|AMZN|DSNP).*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Task<List<OrganizerItem>> ScanFolderAsync(string folderPath)
        {
            return Task.Run(() =>
            {
                var result = new List<OrganizerItem>();
                if (!Directory.Exists(folderPath)) return result;

                var files = Directory.GetFiles(folderPath)
                    .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f);

                foreach (var file in files)
                {
                    var (seriesName, season) = DetectSeriesName(Path.GetFileNameWithoutExtension(file));
                    result.Add(new OrganizerItem
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        SeriesName = seriesName,
                        SeasonNumber = season
                    });
                }
                return result;
            });
        }

        public Task OrganizeAsync(IEnumerable<OrganizerItem> items, string baseFolderPath,
                                  IProgress<OrganizerProgressReport>? progress = null)
        {
            return Task.Run(() =>
            {
                foreach (var item in items.Where(i => i.IsSeries))
                {
                    try
                    {
                        string targetDir = Path.Combine(baseFolderPath, item.TargetFolder);
                        Directory.CreateDirectory(targetDir);

                        string dest = Path.Combine(targetDir, item.FileName);

                        if (File.Exists(dest))
                        {
                            progress?.Report(new OrganizerProgressReport
                            {
                                Item = item,
                                Status = OrganizerStatus.Skipped,
                                Message = $"⏭️ رد شد (از قبل وجود دارد): {item.FileName}"
                            });
                        }
                        else
                        {
                            File.Move(item.FullPath, dest);
                            progress?.Report(new OrganizerProgressReport
                            {
                                Item = item,
                                Status = OrganizerStatus.Moved,
                                Message = $"✅ منتقل شد: {item.FileName} → {item.SeriesName}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report(new OrganizerProgressReport
                        {
                            Item = item,
                            Status = OrganizerStatus.Error,
                            ErrorMessage = ex.Message,
                            Message = $"❌ خطا: {item.FileName} — {ex.Message}"
                        });
                    }
                }
            });
        }

        private (string Name, int? Season) DetectSeriesName(string stem)
        {
            try
            {
                var detector = new MovieFileLibrary.MovieDetector();
                var movie = detector.GetInfo(stem);
                if (movie != null && !string.IsNullOrWhiteSpace(movie.Title))
                {
                    if (movie.IsSeries || (movie.Season != null && movie.Season > 0) || (movie.Episode != null && movie.Episode > 0))
                    {
                        return (CleanName(movie.Title), movie.Season > 0 ? movie.Season : null);
                    }
                }
            }
            catch { }

            foreach (var pattern in SeriesPatterns)
            {
                var m = pattern.Match(stem);
                if (m.Success)
                {
                    string cleaned = CleanName(m.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        // Try to extract season number from regex match if possible
                        int? season = null;
                        var seasonMatch = Regex.Match(stem, @"[Ss]([0-9]{1,2})[Ee][0-9]{1,2}", RegexOptions.IgnoreCase);
                        if (seasonMatch.Success && int.TryParse(seasonMatch.Groups[1].Value, out int s))
                        {
                            season = s;
                        }
                        else
                        {
                            var seasonWordMatch = Regex.Match(stem, @"[Ss]eason[.\s_\-]*([0-9]{1,2})", RegexOptions.IgnoreCase);
                            if (seasonWordMatch.Success && int.TryParse(seasonWordMatch.Groups[1].Value, out int s2))
                            {
                                season = s2;
                            }
                            else
                            {
                                var xMatch = Regex.Match(stem, @"([0-9]{1,2})[xX][0-9]{2}", RegexOptions.IgnoreCase);
                                if (xMatch.Success && int.TryParse(xMatch.Groups[1].Value, out int s3))
                                {
                                    season = s3;
                                }
                            }
                        }

                        return (cleaned, season);
                    }
                }
            }
            return (string.Empty, null);
        }

        private string CleanName(string raw)
        {
            string name = Regex.Replace(raw, @"[._]+", " ");
            name = QualityPattern.Replace(name, "");
            name = name.TrimEnd(' ', '-', '_', '.');
            name = Regex.Replace(name, @"\s+", " ").Trim();
            return name;
        }
    }
}
