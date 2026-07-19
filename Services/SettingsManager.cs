using System;
using System.IO;
using System.Text.Json;

namespace MovieManagerDesktop.Services
{
    public class SettingsModel
    {
        public string SelectedDataSource { get; set; } = "FM_DB"; // FM_DB, TMDB_ONLY, OMDB_ONLY
        public string TmdbApiKey { get; set; } = string.Empty;
        public string OmdbApiKey { get; set; } = string.Empty;
        public string TmdbLanguage { get; set; } = "fa-IR"; // fa-IR or en-US
        public int PosterSize { get; set; } = 220;
        public string Theme { get; set; } = "DeepPurple"; // DeepPurple, MidnightBlue, OLEDBlack
        public bool IsDarkTheme { get; set; } = true;
        public int MediaTypeFilterIndex { get; set; } = 0;
        public int WatchedFilterIndex { get; set; } = 0;
        public int ListFilterIndex { get; set; } = 0;
        public int SortIndex { get; set; } = 0;
        public int SortDirectionIndex { get; set; } = 0;
        public int SelectedGenreIndex { get; set; } = 0;
        public bool IsQuickFilterMovies { get; set; } = false;
        public bool IsQuickFilterSeries { get; set; } = false;
        public bool IsQuickFilterUnwatched { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static SettingsModel LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                }
                catch
                {
                    return new SettingsModel();
                }
            }
            return new SettingsModel();
        }

        public static void SaveSettings(SettingsModel settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
