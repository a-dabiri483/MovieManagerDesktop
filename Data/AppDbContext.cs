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
        public const int CurrentSchemaVersion = 5;

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

                        // Enable WAL (Write-Ahead Logging) and NORMAL synchronous for maximum concurrency and high-speed bulk inserts
                        using (var pragmaCmd = conn.CreateCommand())
                        {
                            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                            pragmaCmd.ExecuteNonQuery();
                        }

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA user_version;";
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int ver))
                        {
                            currentVersion = ver;
                        }

                        if (currentVersion < CurrentSchemaVersion)
                        {
                            Services.LoggerService.Info($"[AppDbContext] 🚀 Checking database schema migrations (current: v{currentVersion}, target: v{CurrentSchemaVersion})...");
                            ApplySchemaMigrations(conn, currentVersion);
                            Services.LoggerService.Info($"[AppDbContext] ✔ Database schema successfully verified up to v{CurrentSchemaVersion}");
                        }
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Services.LoggerService.Error("[AppDbContext] Database initialization or migration failed", ex);
                    throw;
                }
            }
        }

        private static void SetUserVersion(System.Data.Common.DbConnection conn, int version)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA user_version = {version};";
            cmd.ExecuteNonQuery();
            Services.LoggerService.Info($"[AppDbContext] ✔ Database schema successfully updated to v{version}");
        }

        private static void ApplySchemaMigrations(System.Data.Common.DbConnection conn, int fromVersion)
        {
            void ExecuteSql(System.Data.Common.DbCommand cmd, string sql)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Services.LoggerService.Error($"[AppDbContext] Migration command failed: {sql}", ex);
                    throw;
                }
            }

            // Migration v1: Ensure all schema columns exist in VideoFiles
            if (fromVersion < 1)
            {
                Services.LoggerService.Info("[AppDbContext] Applying migration v1 (schema columns for VideoFiles)...");
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cmd.CommandText = "PRAGMA table_info(VideoFiles);";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1)) existingCols.Add(reader.GetString(1));
                    }
                }

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
                        ExecuteSql(cmd, $"ALTER TABLE VideoFiles ADD COLUMN {colName} {colDef};");
                    }
                }

                tx.Commit();
                SetUserVersion(conn, 1);
                fromVersion = 1;
            }

            // Migration v2: Auto-heal legacy data
            if (fromVersion < 2)
            {
                Services.LoggerService.Info("[AppDbContext] Applying migration v2 (auto-heal legacy data)...");
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                ExecuteSql(cmd, "UPDATE VideoFiles SET Year = substr(FirstAirDate, 1, 4) WHERE (Year IS NULL OR Year = '' OR Year = '0') AND FirstAirDate IS NOT NULL AND length(FirstAirDate) >= 4;");
                ExecuteSql(cmd, "UPDATE VideoFiles SET Rating = ROUND(Rating / 10.0, 1) WHERE Rating > 10.0;");
                ExecuteSql(cmd, "UPDATE VideoFiles SET PosterUrl = replace(PosterUrl, 'AppData\\Roaming\\CineTrack\\Images', 'AppData\\Local\\MovieManager\\Images') WHERE PosterUrl LIKE '%AppData\\Roaming\\CineTrack\\Images%';");
                ExecuteSql(cmd, "UPDATE VideoFiles SET BackdropUrl = replace(BackdropUrl, 'AppData\\Roaming\\CineTrack\\Images', 'AppData\\Local\\MovieManager\\Images') WHERE BackdropUrl LIKE '%AppData\\Roaming\\CineTrack\\Images%';");
                ExecuteSql(cmd, "UPDATE VideoFiles SET PosterUrl = replace(PosterUrl, 'AppData/Roaming/CineTrack/Images', 'AppData/Local/MovieManager/Images') WHERE PosterUrl LIKE '%AppData/Roaming/CineTrack/Images%';");
                ExecuteSql(cmd, "UPDATE VideoFiles SET BackdropUrl = replace(BackdropUrl, 'AppData/Roaming/CineTrack/Images', 'AppData/Local/MovieManager/Images') WHERE BackdropUrl LIKE '%AppData/Roaming/CineTrack/Images%';");

                tx.Commit();
                SetUserVersion(conn, 2);
                fromVersion = 2;
            }

            // Migration v3: TvSeasons and TvEpisodes tables
            if (fromVersion < 3)
            {
                Services.LoggerService.Info("[AppDbContext] Applying migration v3 (TvSeasons and TvEpisodes tables)...");
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                ExecuteSql(cmd, @"
                    CREATE TABLE IF NOT EXISTS TvSeasons (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TmdbSeriesId INTEGER NOT NULL,
                        SeasonNumber INTEGER NOT NULL,
                        Name TEXT,
                        Overview TEXT,
                        PosterPath TEXT,
                        AirDate TEXT,
                        EpisodeCount INTEGER NOT NULL
                    );");

                ExecuteSql(cmd, @"
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
                    );");

                tx.Commit();
                SetUserVersion(conn, 3);
                fromVersion = 3;
            }

            // Migration v4: Performance Indexes
            if (fromVersion < 4)
            {
                Services.LoggerService.Info("[AppDbContext] Applying migration v4 (performance indexes)...");
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_VideoFiles_TmdbId ON VideoFiles (TmdbId);");
                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_VideoFiles_MediaType ON VideoFiles (MediaType);");
                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_VideoFiles_FormattedTitle ON VideoFiles (FormattedTitle);");
                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_TvSeasons_TmdbSeriesId ON TvSeasons (TmdbSeriesId);");
                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_TvEpisodes_TmdbSeriesId ON TvEpisodes (TmdbSeriesId, SeasonNumber);");

                tx.Commit();
                SetUserVersion(conn, 4);
                fromVersion = 4;
            }

            // Migration v5: FilePath index for instant duplicate lookups during bulk scanning
            if (fromVersion < 5)
            {
                Services.LoggerService.Info("[AppDbContext] Applying migration v5 (FilePath index)...");
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                ExecuteSql(cmd, "CREATE INDEX IF NOT EXISTS IX_VideoFiles_FilePath ON VideoFiles (FilePath);");

                tx.Commit();
                SetUserVersion(conn, 5);
                fromVersion = 5;
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
