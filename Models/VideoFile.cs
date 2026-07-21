using System;

namespace MovieManagerDesktop.Models
{
    public class VideoFile
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

        public bool IsWatched { get; set; } = false;
        public bool IsFavorite { get; set; } = false;
        public bool IsWatchlist { get; set; } = false;
        public double WatchProgressPercent { get; set; } = 0; // 0 to 100
        public double LastWatchPosition { get; set; } = 0; // in seconds
        
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
