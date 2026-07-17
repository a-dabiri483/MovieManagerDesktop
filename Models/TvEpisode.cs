using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class TvEpisode : ObservableObject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int TmdbSeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        
        public string? Name { get; set; }
        public string? Overview { get; set; }
        public string? StillPath { get; set; }
        public string? AirDate { get; set; }
        public double VoteAverage { get; set; }

        [ObservableProperty]
        private bool _isWatched;

        public string StillUrl => string.IsNullOrEmpty(StillPath) ? "" : $"https://image.tmdb.org/t/p/w500{StillPath}";
    }
}
