using System;
using System.Text.RegularExpressions;

namespace MovieManagerDesktop.Services
{
    public class ParsedSeriesInfo
    {
        public string CleanName { get; set; } = string.Empty;
        public string SeasonEpisode { get; set; } = string.Empty; // e.g. S01E05
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string Quality { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Extras { get; set; } = string.Empty; // e.g. Dubbed, HardSub
    }

    public class RegexParserService
    {
        public ParsedSeriesInfo ParseVideoFileName(string fileName)
        {
            var result = new ParsedSeriesInfo();
            
            // Remove extension
            int extIndex = fileName.LastIndexOf('.');
            string nameWithoutExt = extIndex > 0 ? fileName.Substring(0, extIndex) : fileName;
            
            // Convert to a working string replacing dots and underscores with spaces for easier word matching
            string workingName = nameWithoutExt.Replace(".", " ").Replace("_", " ");

            // 0. Strip leading numbers (often used for sorting like "01 Iron Man" or "18Black Panther")
            // Only strip if there are actual letters following it so we don't break movies like "300"
            string strippedPrefix = Regex.Replace(workingName, @"^\d{1,3}(?!\d)\s*", "");
            if (Regex.IsMatch(strippedPrefix, @"[a-zA-Z]"))
            {
                workingName = strippedPrefix.Trim();
            }

            // 1. Find Season/Episode (S01E05, S1E5, S01E05-06, 1x05, etc)
            var seRegex = new Regex(@"(?:S\d{1,2}E\d{1,2}(?:-E?\d{1,2})?)|(?:\b\d{1,2}[xX]\d{2,3}\b)", RegexOptions.IgnoreCase);
            var seMatch = seRegex.Match(workingName);
            
            if (seMatch.Success)
            {
                result.SeasonEpisode = seMatch.Value.ToUpper();
                // Extract clean name (everything before the Season/Episode)
                result.CleanName = workingName.Substring(0, seMatch.Index).Trim();
                
                var sMatch = Regex.Match(seMatch.Value, @"S(\d{1,2})", RegexOptions.IgnoreCase);
                var eMatch = Regex.Match(seMatch.Value, @"E(\d{1,2})", RegexOptions.IgnoreCase);
                var xMatch = Regex.Match(seMatch.Value, @"(\d{1,2})[xX](\d{2,3})", RegexOptions.IgnoreCase);

                if (sMatch.Success && int.TryParse(sMatch.Groups[1].Value, out int s)) result.SeasonNumber = s;
                else if (xMatch.Success && int.TryParse(xMatch.Groups[1].Value, out int xs)) result.SeasonNumber = xs;

                if (eMatch.Success && int.TryParse(eMatch.Groups[1].Value, out int e)) result.EpisodeNumber = e;
                else if (xMatch.Success && int.TryParse(xMatch.Groups[2].Value, out int xe)) result.EpisodeNumber = xe;
            }
            else
            {
                // Fallback: look for generic episode pattern like "Ep 5" or just assume the whole thing is name if no season
                result.CleanName = workingName.Trim(); // Will need TMDB to resolve
            }

            // Clean up the name (remove year if it's right before S01E05)
            result.CleanName = Regex.Replace(result.CleanName, @"\b(?:19|20)\d{2}\b$", "").Trim();
            // Remove trailing hyphens or extra spaces
            result.CleanName = result.CleanName.TrimEnd('-', ' ');

            // 2. Find Quality
            var qualityRegex = new Regex(@"\b(480|480p|720|720p|1080|1080p|1440|1440p|2160|2160p|4K|8K)\b", RegexOptions.IgnoreCase);
            var qualityMatch = qualityRegex.Match(workingName);
            if (qualityMatch.Success)
            {
                result.Quality = qualityMatch.Value.ToLower(); // e.g. 1080p or 1080
            }

            // 3. Find Source
            var sourceRegex = new Regex(@"\b(WEB-DL|WEBRip|BluRay|HDTV|BDRip|BRRip|DVD|DVDRip|WEB DL|WEB)\b", RegexOptions.IgnoreCase);
            var sourceMatch = sourceRegex.Match(workingName); // Use workingName for easier matching
            if (sourceMatch.Success)
            {
                result.Source = sourceMatch.Value.ToUpper();
            }

            // 4. Find Extras (Dubbed, Subbed, etc)
            string extras = "";
            
            if (Regex.IsMatch(workingName, @"\b(Dubbed|دوبله|Duble|DUBLE)\b", RegexOptions.IgnoreCase) || workingName.Contains("دوبله") || workingName.Contains("DUBLE"))
            {
                extras += "DUBLE";
            }
            
            if (Regex.IsMatch(workingName, @"\b(HardSub|Sub|زیرنویس)\b", RegexOptions.IgnoreCase) || workingName.Contains("زیرنویس") || workingName.Contains("HardSub"))
            {
                if (extras.Length > 0) extras += " ";
                extras += "زیرنویس";
            }
            
            if (Regex.IsMatch(workingName, @"\b(x265|HEVC|x264|10bit)\b", RegexOptions.IgnoreCase))
            {
                if (extras.Length > 0) extras += " ";
                extras += "x265";
            }

            result.Extras = extras;

            // Aggressive Cleanup of CleanName
            // We strip out all the known garbage tags from CleanName to leave only the real movie/series name
            string badWordsPattern = @"\b(480|480p|720|720p|1080|1080p|1440|1440p|2160|2160p|4K|8K|WEB-DL|WEBRip|BluRay|HDTV|BDRip|BRRip|DVD|DVDRip|WEB DL|WEB|Dubbed|دوبله|Duble|DUBLE|HardSub|Sub|زیرنویس|x265|HEVC|x264|10bit|Golchindl|AvaMovie|Mobomovie|DibaMovie|FilmKio|ZarFilm|SoftSub|S\d{2,3})\b";
            
            result.CleanName = Regex.Replace(result.CleanName, badWordsPattern, " ", RegexOptions.IgnoreCase);
            
            // Remove Persian text that might have been missed by word boundaries
            result.CleanName = result.CleanName.Replace("دوبله", "").Replace("زیرنویس", "").Replace("هاردساب", "");
            
            // Clean up S050 or similar orphaned season tags
            result.CleanName = Regex.Replace(result.CleanName, @"\bS\d{2,4}\b", " ", RegexOptions.IgnoreCase);
            
            // Final cleanup of extra spaces or dashes left behind
            result.CleanName = result.CleanName.Replace("-", " ").Trim();
            result.CleanName = Regex.Replace(result.CleanName, @"\s+", " ").Trim();

            return result;
        }
    }
}
