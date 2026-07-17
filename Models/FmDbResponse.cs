using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MovieManagerDesktop.Models
{
    public class FmDbResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("description")]
        public List<FmDbMovieDto> Description { get; set; }
    }

    public class FmDbMovieDto
    {
        [JsonPropertyName("#TITLE")]
        public string Title { get; set; }

        [JsonPropertyName("#YEAR")]
        public int? Year { get; set; }

        [JsonPropertyName("#IMDB_ID")]
        public string ImdbId { get; set; }

        [JsonPropertyName("#ACTORS")]
        public string Actors { get; set; }

        [JsonPropertyName("#IMG_POSTER")]
        public string ImgPoster { get; set; }
    }
}
