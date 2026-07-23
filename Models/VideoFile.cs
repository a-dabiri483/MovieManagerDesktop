using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class VideoFile : ObservableObject
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FormattedTitle { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string? Resolution { get; set; } // e.g. 1080p, 720p
        public string? MediaType { get; set; } // Movie, Series
        public DateTime DateAdded { get; set; }
        public long FileSizeBytes { get; set; }
        
        // TMDb / OMDb Metadata
        public int? TmdbId { get; set; }
        public string? PosterUrl { get; set; }
        public double? Rating { get; set; }
        public string? Overview { get; set; }
        public string? Genres { get; set; }
        public string? Actors { get; set; }
        public string? Director { get; set; }
        
        // Added Fields
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string? Quality { get; set; }
        public int? NumberOfSeasons { get; set; }
        public int? NumberOfEpisodes { get; set; }
        
        public string? NextEpisodeDate { get; set; }
        public int? NextEpisodeSeason { get; set; }
        public int? NextEpisodeNumber { get; set; }

        [ObservableProperty]
        private bool _isWatched = false;

        [ObservableProperty]
        private bool _isFavorite = false;

        [ObservableProperty]
        private bool _isWatchlist = false;

        [ObservableProperty]
        private double _watchProgressPercent = 0; // 0 to 100

        [ObservableProperty]
        private long _watchProgressSeconds = 0; // Exact resume position

        [ObservableProperty]
        private long _totalDurationSeconds = 0;
        
        public string? BackdropUrl { get; set; }
        public string? CollectionName { get; set; }

        public bool IsIdentified { get; set; } = false;
        
        // Series Tracking
        public bool IsTracked { get; set; } = false;
        public string? SeriesStatus { get; set; }
        public int? LastAiredSeason { get; set; }
        public bool HasNewEpisode { get; set; } = false;
        
        // Advanced Tracker Fields
        public DateTime? FirstAirDate { get; set; }
        public DateTime? LastAirDate { get; set; }
        public string? NetworkName { get; set; }
        public string? AirDay { get; set; }
        public string? AirTime { get; set; }
        public int? TotalSeasonsCount { get; set; }
        public int? TotalEpisodesCount { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? NextEpisodeToAirInfo { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public System.Collections.Generic.List<TvSeason> Seasons { get; set; } = new System.Collections.Generic.List<TvSeason>();
    }
}
