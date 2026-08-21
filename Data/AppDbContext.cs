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
            Database.EnsureCreated();
            
            // Add new columns for Series Tracker
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN FirstAirDate TEXT;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN LastAirDate TEXT;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN NetworkName TEXT;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN AirDay TEXT;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN AirTime TEXT;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN TotalSeasonsCount INTEGER;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN TotalEpisodesCount INTEGER;"); } catch { }

            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN IsWatched INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN IsWatchlist INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN WatchProgressPercent REAL NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN WatchProgressSeconds INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN TotalDurationSeconds INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN CollectionName TEXT;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN IsHidden INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }

            // Auto-heal missing Year from FirstAirDate
            try
            {
                Database.ExecuteSqlRaw("UPDATE VideoFiles SET Year = substr(FirstAirDate, 1, 4) WHERE (Year IS NULL OR Year = '' OR Year = '0') AND FirstAirDate IS NOT NULL AND length(FirstAirDate) >= 4;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN CustomTags TEXT;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN HasDubbing INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN HasSubtitle INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN ContentRating TEXT;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN LastPlayedEpisode INTEGER;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN IsTracked INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN SeriesStatus TEXT;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN LastAiredSeason INTEGER;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN HasNewEpisode INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN NextEpisodeDate TEXT;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN NextEpisodeSeason INTEGER;");
            }
            catch { }
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE VideoFiles ADD COLUMN NextEpisodeNumber INTEGER;");
            }
            catch { }
            
            try
            {
                Database.ExecuteSqlRaw(@"
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
                Database.ExecuteSqlRaw(@"
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
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "movies.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
