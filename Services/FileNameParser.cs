using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MovieFileLibrary;

namespace MovieManagerDesktop.Services
{
    public class ParsedMediaInfo
    {
        public string RawFileName { get; set; }
        public string ParsedTitle { get; set; }
        public int? Year { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string MediaType { get; set; } // "Movie" or "Series"
        public string Quality { get; set; }
    }

    public class FileNameParser
    {
        private readonly MovieDetector _detector;

        public FileNameParser()
        {
            _detector = new MovieDetector();
        }

        public ParsedMediaInfo Parse(string fileName, string fullPath = "")
        {
            try
            {
                var info = _detector.GetInfo(fileName);
                
                string parsedTitle = info.Title;
                
                if (string.IsNullOrWhiteSpace(parsedTitle))
                {
                     parsedTitle = Path.GetFileNameWithoutExtension(fileName);
                     parsedTitle = System.Text.RegularExpressions.Regex.Replace(parsedTitle, @"[Ss]\d{1,2}[Ee]\d{1,2}(?:-[Ee]?\d{1,2})?", "");
                }
                
                // Normalize Title to prevent splitting (e.g. "Turner & Hooch" vs "Turner And Hooch")
                parsedTitle = parsedTitle.Replace("&", "and", StringComparison.OrdinalIgnoreCase);
                parsedTitle = parsedTitle.Replace(".", " ").Replace("_", " ").Replace("-", " ");
                
                // Remove quality and release group tags from title
                var tagsToRemove = new[] { 
                    "1080p", "720p", "480p", "2160p", "4k", "uhd", "bluray", "blu-ray", "brrip", "bdrip",
                    "web-dl", "webrip", "hdrip", "hdtv", "dvdrip", "x264", "x265", "h264", "h265", "hevc",
                    "10bit", "aac", "dts", "ac3", "yify", "psa", "pahe", "rarbg", "tigole", "qxr" 
                };
                
                var words = parsedTitle.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var cleanWords = words.Where(w => !tagsToRemove.Contains(w.ToLowerInvariant())).ToList();
                parsedTitle = string.Join(" ", cleanWords).Trim();

                int? yearValue = null;
                if (!string.IsNullOrWhiteSpace(info.Year) && int.TryParse(info.Year, out int y))
                {
                    yearValue = y;
                }

                bool isSeries = info.IsSeries || info.Season > 0 || info.Episode > 0;
                
                if (!isSeries && !string.IsNullOrWhiteSpace(fullPath))
                {
                    string lowerPath = fullPath.ToLowerInvariant();
                    if (lowerPath.Contains(@"\series\") || lowerPath.Contains(@"\serial\") || 
                        lowerPath.Contains("/series/") || lowerPath.Contains("/serial/") ||
                        System.Text.RegularExpressions.Regex.IsMatch(lowerPath, @"[\\/](season|fasl|f)\s*\d+"))
                    {
                        isSeries = true;
                    }
                }

                return new ParsedMediaInfo
                {
                    RawFileName = fileName,
                    ParsedTitle = string.IsNullOrWhiteSpace(parsedTitle) ? "ناشناس" : parsedTitle,
                    Year = yearValue,
                    Season = info.Season,
                    Episode = info.Episode,
                    MediaType = isSeries ? "Series" : "Movie",
                    Quality = ExtractQuality(fileName)
                };
            }
            catch
            {
                return new ParsedMediaInfo
                {
                    RawFileName = fileName,
                    ParsedTitle = Path.GetFileNameWithoutExtension(fileName),
                    MediaType = "Movie",
                    Quality = ExtractQuality(fileName)
                };
            }
        }

        private string ExtractQuality(string stem)
        {
            var lower = stem.ToLowerInvariant();
            var foundTags = new List<string>();

            if (lower.Contains("2160p") || lower.Contains("4k") || lower.Contains("uhd")) foundTags.Add("4K UHD");
            else if (lower.Contains("1080p")) foundTags.Add("1080p");
            else if (lower.Contains("720p")) foundTags.Add("720p");

            if (lower.Contains("x265") || lower.Contains("hevc") || lower.Contains("h265") || lower.Contains("h.265")) foundTags.Add("x265");
            if (lower.Contains("hdr")) foundTags.Add("HDR");
            if (lower.Contains("10bit")) foundTags.Add("10-bit");

            return foundTags.Count > 0 ? string.Join(" ", foundTags) : null;
        }
    }
}
