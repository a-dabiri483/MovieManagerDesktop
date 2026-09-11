using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public static class DataCleanupService
    {
        private static string GetAppDataDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManager");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static async Task<DataCleanupOptions> AnalyzeStorageAsync()
        {
            var options = new DataCleanupOptions();
            var appDataDir = GetAppDataDir();

            await Task.Run(() =>
            {
                // 1. Database
                try
                {
                    string dbPath = AppDbContext.GetDatabasePath();
                    long dbBytes = 0;
                    if (File.Exists(dbPath)) dbBytes += new FileInfo(dbPath).Length;
                    string wal = dbPath + "-wal";
                    if (File.Exists(wal)) dbBytes += new FileInfo(wal).Length;
                    string shm = dbPath + "-shm";
                    if (File.Exists(shm)) dbBytes += new FileInfo(shm).Length;

                    options.DatabaseSizeBytes = dbBytes;

                    using var db = new AppDbContext();
                    int videoCount = db.VideoFiles.Count();
                    int seriesCount = db.TvSeasons.Select(s => s.TmdbSeriesId).Distinct().Count();
                    options.DatabaseItemsCount = videoCount + seriesCount;
                }
                catch (Exception ex)
                {
                    LoggerService.Warning($"[DataCleanup] Could not analyze database: {ex.Message}");
                }

                // 2. Images Directory
                try
                {
                    string imagesDir = Path.Combine(appDataDir, "Images");
                    if (Directory.Exists(imagesDir))
                    {
                        var files = Directory.GetFiles(imagesDir, "*.*", SearchOption.AllDirectories);
                        options.ImagesCount = files.Length;
                        options.ImagesSizeBytes = files.Sum(f =>
                        {
                            try { return new FileInfo(f).Length; } catch { return 0L; }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warning($"[DataCleanup] Could not analyze Images dir: {ex.Message}");
                }

                // 3. ImageCache Directory
                try
                {
                    string cacheDir = Path.Combine(appDataDir, "ImageCache");
                    if (Directory.Exists(cacheDir))
                    {
                        var files = Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories);
                        options.ImageCacheCount = files.Length;
                        options.ImageCacheSizeBytes = files.Sum(f =>
                        {
                            try { return new FileInfo(f).Length; } catch { return 0L; }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warning($"[DataCleanup] Could not analyze ImageCache dir: {ex.Message}");
                }

                // 4. Watch History / Playback Sync
                try
                {
                    string syncPath = Path.Combine(appDataDir, "playback_sync.json");
                    if (File.Exists(syncPath))
                    {
                        options.WatchHistorySizeBytes = new FileInfo(syncPath).Length;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warning($"[DataCleanup] Could not analyze sync file: {ex.Message}");
                }

                // 5. Logs Directory
                try
                {
                    string logsDir = Path.Combine(appDataDir, "Logs");
                    if (Directory.Exists(logsDir))
                    {
                        var files = Directory.GetFiles(logsDir, "*.txt", SearchOption.AllDirectories);
                        options.LogsSizeBytes = files.Sum(f =>
                        {
                            try { return new FileInfo(f).Length; } catch { return 0L; }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warning($"[DataCleanup] Could not analyze Logs dir: {ex.Message}");
                }
            });

            return options;
        }

        public static async Task ExecuteCleanupAsync(DataCleanupOptions options)
        {
            var appDataDir = GetAppDataDir();

            await Task.Run(async () =>
            {
                // 1. Clean Database
                if (options.CleanDatabase)
                {
                    try
                    {
                        LoggerService.Info("[DataCleanup] Starting database cleanup...");
                        using (var db = new AppDbContext())
                        {
                            await db.Database.ExecuteSqlRawAsync("DELETE FROM VideoFiles;");
                            await db.Database.ExecuteSqlRawAsync("DELETE FROM TvEpisodes;");
                            await db.Database.ExecuteSqlRawAsync("DELETE FROM TvSeasons;");
                            try
                            {
                                await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('VideoFiles', 'TvEpisodes', 'TvSeasons');");
                            }
                            catch { }
                        }

                        // Flush pool to allow full WAL truncation & vacuum
                        SqliteConnection.ClearAllPools();

                        string dbPath = AppDbContext.GetDatabasePath();
                        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
                        {
                            await conn.OpenAsync();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM; PRAGMA wal_checkpoint(TRUNCATE);";
                            await cmd.ExecuteNonQueryAsync();
                        }

                        SqliteConnection.ClearAllPools();
                        LoggerService.Info("[DataCleanup] Database records deleted and vacuum/checkpoint successfully finished.");
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Error("[DataCleanup] Failed to clean database tables", ex);
                        throw;
                    }
                }

                // 2. Clean Images Directory
                if (options.CleanImages)
                {
                    try
                    {
                        string imagesDir = Path.Combine(appDataDir, "Images");
                        if (Directory.Exists(imagesDir))
                        {
                            LoggerService.Info($"[DataCleanup] Deleting all files in: {imagesDir}");
                            foreach (var file in Directory.GetFiles(imagesDir, "*.*", SearchOption.AllDirectories))
                            {
                                try { File.Delete(file); } catch { }
                            }
                            foreach (var subDir in Directory.GetDirectories(imagesDir))
                            {
                                try { Directory.Delete(subDir, true); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[DataCleanup] Error cleaning Images directory: {ex.Message}");
                    }
                }

                // 3. Clean ImageCache Directory
                if (options.CleanImageCache)
                {
                    try
                    {
                        string cacheDir = Path.Combine(appDataDir, "ImageCache");
                        if (Directory.Exists(cacheDir))
                        {
                            LoggerService.Info($"[DataCleanup] Clearing image cache: {cacheDir}");
                            foreach (var file in Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[DataCleanup] Error cleaning ImageCache directory: {ex.Message}");
                    }
                }

                // 4. Clean Watch History & Playback Sync
                if (options.CleanWatchHistory)
                {
                    try
                    {
                        string syncPath = Path.Combine(appDataDir, "playback_sync.json");
                        if (File.Exists(syncPath))
                        {
                            File.Delete(syncPath);
                            LoggerService.Info("[DataCleanup] Deleted playback_sync.json");
                        }

                        // If database wasn't completely cleared, reset watch flags on existing media
                        if (!options.CleanDatabase)
                        {
                            using var db = new AppDbContext();
                            await db.Database.ExecuteSqlRawAsync("UPDATE VideoFiles SET IsWatched = 0, WatchProgressPercent = 0, LastPlayedPositionSeconds = 0, LastWatchedDate = NULL;");
                            await db.Database.ExecuteSqlRawAsync("UPDATE TvEpisodes SET IsWatched = 0;");
                            LoggerService.Info("[DataCleanup] Reset watch status in database tables.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[DataCleanup] Error resetting watch history: {ex.Message}");
                    }
                }

                // 5. Clean Logs Directory
                if (options.CleanLogs)
                {
                    try
                    {
                        string logsDir = Path.Combine(appDataDir, "Logs");
                        if (Directory.Exists(logsDir))
                        {
                            foreach (var file in Directory.GetFiles(logsDir, "*.txt"))
                            {
                                try { File.Delete(file); } catch { /* Ignore active locked log file */ }
                            }
                            LoggerService.Info("[DataCleanup] Cleaned old log files.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[DataCleanup] Error cleaning logs directory: {ex.Message}");
                    }
                }
            });
        }
    }
}
