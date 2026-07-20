using System;
using System.Text.RegularExpressions;

namespace MovieManagerDesktop.Services
{
    public class ParsedSeriesInfo
    {
        public string CleanName { get; set; } = string.Empty;
        public string SeasonEpisode { get; set; } = string.Empty; // e.g. S01E05
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

            // 1. Find Season/Episode (S01E05, S1E5, S01E05-06, etc)
            var seRegex = new Regex(@"S\d{1,2}E\d{1,2}(?:-E?\d{1,2})?", RegexOptions.IgnoreCase);
            var seMatch = seRegex.Match(workingName);
            
            if (seMatch.Success)
            {
                result.SeasonEpisode = seMatch.Value.ToUpper();
                // Extract clean name (everything before the Season/Episode)
                result.CleanName = workingName.Substring(0, seMatch.Index).Trim();
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
            var qualityRegex = new Regex(@"\b(480p|720p|1080p|1440p|2160p|4K|8K)\b", RegexOptions.IgnoreCase);
            var qualityMatch = qualityRegex.Match(workingName);
            if (qualityMatch.Success)
            {
                result.Quality = qualityMatch.Value.ToLower(); // 1080p
            }

            // 3. Find Source
            var sourceRegex = new Regex(@"\b(WEB-DL|WEBRip|BluRay|HDTV|BDRip|BRRip|DVD|DVDRip)\b", RegexOptions.IgnoreCase);
            var sourceMatch = sourceRegex.Match(nameWithoutExt); // Use original to keep dashes (WEB-DL)
            if (sourceMatch.Success)
            {
                result.Source = sourceMatch.Value.ToUpper();
            }

            // 4. Find Extras (Dubbed, Subbed, etc)
            string extras = "";
            
            if (Regex.IsMatch(nameWithoutExt, @"\b(Dubbed|دوبله|Duble|DUBLE)\b", RegexOptions.IgnoreCase) || nameWithoutExt.Contains("دوبله") || nameWithoutExt.Contains("DUBLE"))
            {
                extras += "DUBLE";
            }
            
            if (Regex.IsMatch(nameWithoutExt, @"\b(HardSub|Sub|زیرنویس)\b", RegexOptions.IgnoreCase) || nameWithoutExt.Contains("زیرنویس") || nameWithoutExt.Contains("HardSub"))
            {
                if (extras.Length > 0) extras += " ";
                extras += "زیرنویس";
            }
            
            if (Regex.IsMatch(nameWithoutExt, @"\b(x265|HEVC)\b", RegexOptions.IgnoreCase))
            {
                if (extras.Length > 0) extras += " ";
                extras += "x265";
            }

            result.Extras = extras;

            return result;
        }
    }
}
