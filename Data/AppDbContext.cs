using Microsoft.EntityFrameworkCore;
using MovieManagerDesktop.Models;
using System;
using System.IO;

namespace MovieManagerDesktop.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<VideoFile> VideoFiles { get; set; }
        public DbSet<TvSeason> TvSeasons { get; set; }
        public DbSet<TvEpisode> TvEpisodes { get; set; }

        public AppDbContext()
        {
        }

        private static bool _isInitialized = false;
        private static readonly object _initLock = new();

        public static void InitializeDatabase()
        {
            if (_isInitialized) return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                try
                {
                    using var db = new AppDbContext();
                    db.Database.EnsureCreated();

                    string[] alterCommands = new[]
                    {
                        "ALTER TABLE VideoFiles ADD COLUMN FirstAirDate TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN LastAirDate TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN NetworkName TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN AirDay TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN AirTime TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN TotalSeasonsCount INTEGER;",
                        "ALTER TABLE VideoFiles ADD COLUMN TotalEpisodesCount INTEGER;",
                        "ALTER TABLE VideoFiles ADD COLUMN IsWatched INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN IsWatchlist INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN WatchProgressPercent REAL NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN WatchProgressSeconds INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN TotalDurationSeconds INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN CollectionName TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN CustomTags TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN HasDubbing INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN HasSubtitle INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN ContentRating TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN LastPlayedEpisode INTEGER;",
                        "ALTER TABLE VideoFiles ADD COLUMN LastPlayedAt TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN IsTracked INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN SeriesStatus TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN LastAiredSeason INTEGER;",
                        "ALTER TABLE VideoFiles ADD COLUMN HasNewEpisode INTEGER NOT NULL DEFAULT 0;",
                        "ALTER TABLE VideoFiles ADD COLUMN NextEpisodeDate TEXT;",
                        "ALTER TABLE VideoFiles ADD COLUMN NextEpisodeSeason INTEGER;",
                        "ALTER TABLE VideoFiles ADD COLUMN NextEpisodeNumber INTEGER;"
                    };

                    foreach (var cmd in alterCommands)
                    {
                        try { db.Database.ExecuteSqlRaw(cmd); } catch { }
                    }

                    // Auto-heal missing Year from FirstAirDate
                    try
                    {
                        db.Database.ExecuteSqlRaw("UPDATE VideoFiles SET Year = substr(FirstAirDate, 1, 4) WHERE (Year IS NULL OR Year = '' OR Year = '0') AND FirstAirDate IS NOT NULL AND length(FirstAirDate) >= 4;");
                    }
                    catch { }

                    // Auto-heal rating scale for any score > 10
                    try
                    {
                        db.Database.ExecuteSqlRaw("UPDATE VideoFiles SET Rating = ROUND(Rating / 10.0, 1) WHERE Rating > 10.0;");
                    }
                    catch { }

                    try
                    {
                        db.Database.ExecuteSqlRaw(@"
                            CREATE TABLE IF NOT EXISTS TvSeasons (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                TmdbSeriesId INTEGER NOT NULL,
                                SeasonNumber INTEGER NOT NULL,
                                Name TEXT,
                                Overview TEXT,
                                PosterPath TEXT,
                                AirDate TEXT,
                                EpisodeCount INTEGER NOT NULL
                            );
                        ");
                    }
                    catch { }

                    try
                    {
                        db.Database.ExecuteSqlRaw(@"
                            CREATE TABLE IF NOT EXISTS TvEpisodes (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                TmdbSeriesId INTEGER NOT NULL,
                                SeasonNumber INTEGER NOT NULL,
                                EpisodeNumber INTEGER NOT NULL,
                                Name TEXT,
                                Overview TEXT,
                                StillPath TEXT,
                                AirDate TEXT,
                                VoteAverage REAL NOT NULL,
                                IsWatched INTEGER NOT NULL DEFAULT 0
                            );
                        ");
                    }
                    catch { }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Services.LoggerService.Error("[AppDbContext] Database initialization failed", ex);
                }
            }
        }

        public static string GetDatabasePath()
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManager");
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            
            var targetDbPath = Path.Combine(appDataDir, "movies.db");
            
            // Auto-migration: If a legacy movies.db exists in BaseDirectory and target doesn't exist or is empty, copy it over!
            try
            {
                var legacyDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "movies.db");
                if (File.Exists(legacyDbPath) && (!File.Exists(targetDbPath) || new FileInfo(targetDbPath).Length == 0))
                {
                    File.Copy(legacyDbPath, targetDbPath, true);
                }
            }
            catch { }
            
            return targetDbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
