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
        public const int CurrentSchemaVersion = 4;

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

                    // Read current SQLite schema user_version
                    int currentVersion = 0;
                    using (var conn = db.Database.GetDbConnection())
                    {
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA user_version;";
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int ver))
                        {
                            currentVersion = ver;
                        }

                        if (currentVersion < CurrentSchemaVersion)
                        {
                            Services.LoggerService.Info($"[AppDbContext] 🚀 Upgrading database schema from v{currentVersion} to v{CurrentSchemaVersion}...");
                            ApplySchemaMigrations(conn, currentVersion);

                            cmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
                            cmd.ExecuteNonQuery();
                            Services.LoggerService.Info($"[AppDbContext] ✔ Database schema successfully updated to v{CurrentSchemaVersion}");
                        }
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Services.LoggerService.Error("[AppDbContext] Database initialization failed", ex);
                }
            }
        }

        private static void ApplySchemaMigrations(System.Data.Common.DbConnection conn, int fromVersion)
        {
            using var cmd = conn.CreateCommand();

            void Exec(string sql)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }

            // Migration v1: Ensure all schema columns exist in VideoFiles
            if (fromVersion < 1)
            {
                var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cmd.CommandText = "PRAGMA table_info(VideoFiles);";
                try
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1)) existingCols.Add(reader.GetString(1));
                    }
                }
                catch { }

                var missingColumns = new Dictionary<string, string>
                {
                    { "FirstAirDate", "TEXT" },
                    { "LastAirDate", "TEXT" },
                    { "NetworkName", "TEXT" },
                    { "AirDay", "TEXT" },
                    { "AirTime", "TEXT" },
                    { "TotalSeasonsCount", "INTEGER" },
                    { "TotalEpisodesCount", "INTEGER" },
                    { "IsWatched", "INTEGER NOT NULL DEFAULT 0" },
                    { "IsFavorite", "INTEGER NOT NULL DEFAULT 0" },
                    { "IsWatchlist", "INTEGER NOT NULL DEFAULT 0" },
                    { "WatchProgressPercent", "REAL NOT NULL DEFAULT 0" },
                    { "WatchProgressSeconds", "INTEGER NOT NULL DEFAULT 0" },
                    { "TotalDurationSeconds", "INTEGER NOT NULL DEFAULT 0" },
                    { "CollectionName", "TEXT" },
                    { "IsHidden", "INTEGER NOT NULL DEFAULT 0" },
                    { "CustomTags", "TEXT" },
                    { "HasDubbing", "INTEGER NOT NULL DEFAULT 0" },
                    { "HasSubtitle", "INTEGER NOT NULL DEFAULT 0" },
                    { "ContentRating", "TEXT" },
                    { "LastPlayedEpisode", "INTEGER" },
                    { "LastPlayedAt", "TEXT" },
                    { "IsTracked", "INTEGER NOT NULL DEFAULT 0" },
                    { "SeriesStatus", "TEXT" },
                    { "LastAiredSeason", "INTEGER" },
                    { "HasNewEpisode", "INTEGER NOT NULL DEFAULT 0" },
                    { "NextEpisodeDate", "TEXT" },
                    { "NextEpisodeSeason", "INTEGER" },
                    { "NextEpisodeNumber", "INTEGER" }
                };

                foreach (var (colName, colDef) in missingColumns)
                {
                    if (!existingCols.Contains(colName))
                    {
                        Exec($"ALTER TABLE VideoFiles ADD COLUMN {colName} {colDef};");
                    }
                }
            }

            // Migration v2: Auto-heal legacy data
            if (fromVersion < 2)
            {
                Exec("UPDATE VideoFiles SET Year = substr(FirstAirDate, 1, 4) WHERE (Year IS NULL OR Year = '' OR Year = '0') AND FirstAirDate IS NOT NULL AND length(FirstAirDate) >= 4;");
                Exec("UPDATE VideoFiles SET Rating = ROUND(Rating / 10.0, 1) WHERE Rating > 10.0;");
                Exec(@"
                    UPDATE VideoFiles 
                    SET PosterUrl = replace(PosterUrl, 'AppData\\Roaming\\CineTrack\\Images', 'AppData\\Local\\MovieManager\\Images')
                    WHERE PosterUrl LIKE '%AppData\\Roaming\\CineTrack\\Images%';
                    UPDATE VideoFiles 
                    SET BackdropUrl = replace(BackdropUrl, 'AppData\\Roaming\\CineTrack\\Images', 'AppData\\Local\\MovieManager\\Images')
                    WHERE BackdropUrl LIKE '%AppData\\Roaming\\CineTrack\\Images%';
                    UPDATE VideoFiles 
                    SET PosterUrl = replace(PosterUrl, 'AppData/Roaming/CineTrack/Images', 'AppData/Local/MovieManager/Images')
                    WHERE PosterUrl LIKE '%AppData/Roaming/CineTrack/Images%';
                    UPDATE VideoFiles 
                    SET BackdropUrl = replace(BackdropUrl, 'AppData/Roaming/CineTrack/Images', 'AppData/Local/MovieManager/Images')
                    WHERE BackdropUrl LIKE '%AppData/Roaming/CineTrack/Images%';
                ");
            }

            // Migration v3: TvSeasons and TvEpisodes tables
            if (fromVersion < 3)
            {
                Exec(@"
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
                Exec(@"
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

            // Migration v4: Performance Indexes
            if (fromVersion < 4)
            {
                Exec("CREATE INDEX IF NOT EXISTS IX_VideoFiles_TmdbId ON VideoFiles (TmdbId);");
                Exec("CREATE INDEX IF NOT EXISTS IX_VideoFiles_MediaType ON VideoFiles (MediaType);");
                Exec("CREATE INDEX IF NOT EXISTS IX_VideoFiles_FormattedTitle ON VideoFiles (FormattedTitle);");
                Exec("CREATE INDEX IF NOT EXISTS IX_TvSeasons_TmdbSeriesId ON TvSeasons (TmdbSeriesId);");
                Exec("CREATE INDEX IF NOT EXISTS IX_TvEpisodes_TmdbSeriesId ON TvEpisodes (TmdbSeriesId, SeasonNumber);");
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
