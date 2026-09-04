using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Models
{
    public class MissingEpisodeInfo
    {
        public string? SeriesTitle { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeCode => SeasonNumber > 0 && EpisodeNumber > 0 ? $"S{SeasonNumber:D2}E{EpisodeNumber:D2}" : (SeasonNumber > 0 ? $"فصل {SeasonNumber}" : "کسری");
        public string? EpisodeName { get; set; }
        public string? AirDate { get; set; }
        public string? StillUrl { get; set; }
        public string? Overview { get; set; }

        public string FormattedLabel => SeasonNumber > 0 && EpisodeNumber > 0 ? $"فصل {SeasonNumber} - قسمت {EpisodeNumber}" : (SeasonNumber > 0 ? $"فصل {SeasonNumber}" : "کسری");
        public string FormattedAirDate => !string.IsNullOrEmpty(AirDate) ? DateTimeFormatterService.FormatDate(AirDate) : "تاریخ نامشخص";
    }

    public partial class MissingSeriesGroup : ObservableObject
    {
        public int SeriesId { get; set; }
        public int? TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string? PosterUrl { get; set; }
        public string? BackdropUrl { get; set; }
        public string? Genres { get; set; }
        public string? SeriesStatus { get; set; }

        public string FormattedGenres { get => GenreTranslatorService.TranslateList(Genres); set { } }
        public string FormattedYear { get => !string.IsNullOrEmpty(Year) ? DateTimeFormatterService.FormatYear(Year) : string.Empty; set { } }

        public int TotalAiredEpisodes { get; set; }
        public int TotalLocalEpisodes { get; set; }
        public int MissingCount { get => Math.Max(0, TotalAiredEpisodes - TotalLocalEpisodes); set { } }

        public double ProgressPercent
        {
            get => TotalAiredEpisodes > 0 
                ? Math.Min(100.0, Math.Round((double)TotalLocalEpisodes / TotalAiredEpisodes * 100.0, 1)) 
                : 0;
            set { }
        }

        public string StatusColor
        {
            get
            {
                var s = (SeriesStatus ?? string.Empty).ToLowerInvariant();
                if (s.Contains("return") || s.Contains("continu") || s.Contains("in production"))
                    return "#00D2D3"; // Teal / In production
                if (s.Contains("end"))
                    return "#A4B0BE"; // Gray / Ended
                if (s.Contains("cancel"))
                    return "#FF4757"; // Red / Canceled
                return "#FFA502";
            }
            set { }
        }

        public string StatusText
        {
            get
            {
                var s = (SeriesStatus ?? string.Empty).ToLowerInvariant();
                if (s.Contains("return") || s.Contains("continu") || s.Contains("in production"))
                    return "در حال پخش";
                if (s.Contains("end"))
                    return "پایان یافته";
                if (s.Contains("cancel"))
                    return "کنسل شده";
                return !string.IsNullOrEmpty(SeriesStatus) ? SeriesStatus : "نامشخص";
            }
            set { }
        }

        public bool IsOngoing { get => StatusText == "در حال پخش"; set { } }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isUpdating;

        [ObservableProperty]
        private bool _needsOnlineSync;

        public ObservableCollection<MissingEpisodeInfo> MissingEpisodes { get; set; } = new();

        public string MissingSummaryText
        {
            get
            {
                if (MissingEpisodes.Count == 0) return "هیچ قسمتی کسری ندارد";
                if (MissingEpisodes.Count <= 5)
                {
                    var codes = new List<string>();
                    foreach (var ep in MissingEpisodes) codes.Add(ep.EpisodeCode);
                    return string.Join("، ", codes);
                }
                return $"{MissingEpisodes.Count} قسمت پخش‌شده کسری است";
            }
            set { }
        }
    }
}
