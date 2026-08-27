using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public static class MpvPlaybackService
    {
        public static string? FindMpvPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            string[] candidatePaths = new[]
            {
                Path.Combine(baseDir, "MPVPlayer", "mpv.exe"),
                Path.Combine(projectRoot, "MPVPlayer", "mpv.exe"),
                @"C:\Users\ALI\CascadeProjects\MovieManagerDesktop\MPVPlayer\mpv.exe",
                @"C:\Users\ALI\Downloads\MPV-EASY Player V0.41.0.5\mpv\mpv.exe"
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        public static bool PlayMedia(VideoFile file, List<VideoFile>? playlist = null, int initialIndex = 0)
        {
            string? mpvExe = FindMpvPath();
            if (string.IsNullOrEmpty(mpvExe) || !File.Exists(mpvExe))
            {
                return false;
            }

            try
            {
                // 1. Auto-discover all Series episodes if playlist not explicitly provided or only has 1 item
                if ((playlist == null || playlist.Count <= 1) && (file.MediaType == "Series" || file.Season != null || file.Episode != null || !string.IsNullOrWhiteSpace(file.FormattedTitle)))
                {
                    try
                    {
                        using var db = new AppDbContext();
                        List<VideoFile> episodes = new();

                        if (file.TmdbId != null && file.TmdbId > 0)
                        {
                            episodes = db.VideoFiles
                                .Where(v => v.TmdbId == file.TmdbId)
                                .OrderBy(v => v.Season ?? 1)
                                .ThenBy(v => v.Episode ?? 1)
                                .ThenBy(v => v.FileName)
                                .ToList();
                        }
                        
                        if (episodes.Count <= 1 && !string.IsNullOrWhiteSpace(file.FormattedTitle))
                        {
                            string titleLower = file.FormattedTitle.ToLowerInvariant();
                            episodes = db.VideoFiles
                                .Where(v => v.FormattedTitle != null && v.FormattedTitle.ToLower() == titleLower)
                                .OrderBy(v => v.Season ?? 1)
                                .ThenBy(v => v.Episode ?? 1)
                                .ThenBy(v => v.FileName)
                                .ToList();
                        }

                        if (episodes.Count <= 1 && !string.IsNullOrEmpty(file.FilePath))
                        {
                            string? dir = Path.GetDirectoryName(file.FilePath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                string searchDir = dir;
                                string dirName = Path.GetFileName(dir).ToLowerInvariant();
                                if (dirName.Contains("season") || dirName.Contains("فصل") || dirName.StartsWith("s0") || dirName.StartsWith("s1") || dirName.StartsWith("s2"))
                                {
                                    string? parent = Directory.GetParent(dir)?.FullName;
                                    if (!string.IsNullOrEmpty(parent))
                                    {
                                        searchDir = parent;
                                    }
                                }

                                episodes = db.VideoFiles
                                    .Where(v => v.FilePath.StartsWith(searchDir))
                                    .OrderBy(v => v.Season ?? 1)
                                    .ThenBy(v => v.Episode ?? 1)
                                    .ThenBy(v => v.FileName)
                                    .ToList();

                                if (episodes.Count <= 1 && Directory.Exists(searchDir))
                                {
                                    var diskFiles = Directory.GetFiles(searchDir, "*.*", SearchOption.AllDirectories)
                                        .Where(f => f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) || 
                                                    f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                                    f.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
                                                    f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
                                        .OrderBy(f => f)
                                        .ToList();

                                    if (diskFiles.Count > 1)
                                    {
                                        episodes = diskFiles.Select(f => new VideoFile
                                        {
                                            FilePath = f,
                                            FileName = Path.GetFileName(f),
                                            FormattedTitle = file.FormattedTitle,
                                            TmdbId = file.TmdbId,
                                            PosterUrl = file.PosterUrl,
                                            BackdropUrl = file.BackdropUrl
                                        }).ToList();
                                    }
                                }
                            }
                        }

                        if (episodes.Count > 0)
                        {
                            playlist = episodes;
                            initialIndex = Math.Max(0, playlist.FindIndex(e => (e.Id > 0 && e.Id == file.Id) || string.Equals(e.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                    catch { }
                }

                string pipeName = $"moviemanager_mpv_{file.Id}_{Environment.TickCount64}";
                var args = new List<string>();
                string mpvDir = Path.GetDirectoryName(mpvExe)!;

                // Restore permanent window state and subtitle style if exist in AppData
                try
                {
                    var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
                    var appDataWindowConf = Path.Combine(appData, "window_state.conf");
                    var mpvWindowConfPath = Path.Combine(mpvDir, "window_state.conf");
                    if (File.Exists(appDataWindowConf))
                    {
                        File.Copy(appDataWindowConf, mpvWindowConfPath, true);
                    }

                    var appDataSubConf = Path.Combine(appData, "sub_style.conf");
                    var mpvSubConfPath = Path.Combine(mpvDir, "sub_style.conf");
                    if (File.Exists(appDataSubConf))
                    {
                        File.Copy(appDataSubConf, mpvSubConfPath, true);
                    }
                }
                catch { }

                // 2. Custom config & scripts directory
                args.Add($"--config-dir=\"{mpvDir}\"");

                // 3. IPC Server for live bidirectional database synchronization
                args.Add($"--input-ipc-server=\\\\.\\pipe\\{pipeName}");

                // 4. Window Title with episode info
                string title;
                if (file.Season != null && file.Episode != null)
                {
                    string seriesName = !string.IsNullOrWhiteSpace(file.FormattedTitle) ? file.FormattedTitle : file.FileName;
                    title = $"{seriesName} - [فصل {file.Season:D2} قسمت {file.Episode:D2}]";
                }
                else
                {
                    title = !string.IsNullOrWhiteSpace(file.FormattedTitle) ? file.FormattedTitle : file.FileName;
                }
                args.Add($"--title=\"{title.Replace("\"", "\\\"")}\"");

                // 5. Playlist queue with per-file resume position
                var playlistMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (playlist != null && playlist.Count > 0)
                {
                    args.Add($"--playlist-start={initialIndex}");
                    foreach (var ep in playlist)
                    {
                        if (File.Exists(ep.FilePath))
                        {
                            playlistMap[ep.FilePath] = ep.Id;
                            if (ep.WatchProgressSeconds > 5)
                            {
                                args.Add($"--{{ --start={ep.WatchProgressSeconds} \"{ep.FilePath}\" --}}");
                            }
                            else
                            {
                                args.Add($"\"{ep.FilePath}\"");
                            }
                        }
                    }
                }
                else
                {
                    playlistMap[file.FilePath] = file.Id;
                    if (file.WatchProgressSeconds > 5)
                    {
                        args.Add($"--{{ --start={file.WatchProgressSeconds} \"{file.FilePath}\" --}}");
                    }
                    else
                    {
                        args.Add($"\"{file.FilePath}\"");
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = mpvExe,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = mpvDir,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        LoggerService.Info($"[MPV] {e.Data}");
                };
                proc.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        LoggerService.Error($"[MPV ERR] {e.Data}");
                };

                if (!proc.Start()) return false;
                
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                // Launch background IPC monitoring thread for real-time progress saving
                Task.Run(() => MonitorMpvProgress(pipeName, proc, file.Id, playlistMap));

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to launch MPV player", ex);
                return false;
            }
        }

        private static async Task MonitorMpvProgress(string pipeName, Process proc, int defaultFileId, Dictionary<string, int> playlistMap)
        {
            int currentActiveFileId = defaultFileId;
            double lastTimePos = 0;
            double lastDuration = 0;
            DateTime lastSaveTime = DateTime.MinValue;

            try
            {
                await Task.Delay(500);

                using var cts = new CancellationTokenSource(TimeSpan.FromHours(12));
                using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                int attempts = 0;
                while (!pipeClient.IsConnected && attempts < 25 && !proc.HasExited)
                {
                    try
                    {
                        await pipeClient.ConnectAsync(300, cts.Token);
                    }
                    catch
                    {
                        attempts++;
                        await Task.Delay(150);
                    }
                }

                if (pipeClient.IsConnected)
                {
                    using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                    using var reader = new StreamReader(pipeClient);

                    await writer.WriteLineAsync("{\"command\": [\"observe_property\", 1, \"time-pos\"]}");
                    await writer.WriteLineAsync("{\"command\": [\"observe_property\", 2, \"duration\"]}");
                    await writer.WriteLineAsync("{\"command\": [\"observe_property\", 3, \"path\"]}");

                    while (!proc.HasExited && pipeClient.IsConnected)
                    {
                        var lineTask = reader.ReadLineAsync();
                        var completedTask = await Task.WhenAny(lineTask, Task.Delay(1000));

                        if (completedTask == lineTask)
                        {
                            string? line = await lineTask;
                            if (line == null) break;

                            try
                            {
                                using var doc = JsonDocument.Parse(line);
                                var root = doc.RootElement;

                                if (root.TryGetProperty("name", out var nameProp))
                                {
                                    string propName = nameProp.GetString() ?? "";
                                    if (propName == "time-pos" && root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Number)
                                    {
                                        lastTimePos = dataProp.GetDouble();
                                    }
                                    else if (propName == "duration" && root.TryGetProperty("data", out var durProp) && durProp.ValueKind == JsonValueKind.Number)
                                    {
                                        lastDuration = durProp.GetDouble();
                                    }
                                     else if (propName == "path" && root.TryGetProperty("data", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                                    {
                                        string? currentPath = pathProp.GetString();
                                        if (!string.IsNullOrEmpty(currentPath))
                                        {
                                            var match = playlistMap.FirstOrDefault(kv => string.Equals(kv.Key, currentPath, StringComparison.OrdinalIgnoreCase));
                                            if (match.Value > 0 && match.Value != currentActiveFileId)
                                            {
                                                var playlistOrder = playlistMap.Values.ToList();
                                                int oldIndex = playlistOrder.IndexOf(currentActiveFileId);
                                                int newIndex = playlistOrder.IndexOf(match.Value);

                                                if (newIndex > oldIndex)
                                                {
                                                    // Moving forward (PageDown / Next Episode) -> Mark previous episode as Watched!
                                                    MarkEpisodeWatched(currentActiveFileId, isWatched: true, (long)lastDuration);
                                                }
                                                else if (newIndex < oldIndex)
                                                {
                                                    // Moving backward (PageUp / Prev Episode) -> Unmark the episode we are returning to!
                                                    MarkEpisodeWatched(match.Value, isWatched: false, 0);
                                                }
                                                else if (lastTimePos > 2)
                                                {
                                                    SaveProgressToDb(currentActiveFileId, (long)lastTimePos, (long)lastDuration);
                                                }

                                                currentActiveFileId = match.Value;
                                                lastTimePos = 0;
                                                lastDuration = 0;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                await writer.WriteLineAsync("{\"command\": [\"get_property\", \"time-pos\"]}");
                            }
                            catch { }
                        }

                        // Save progress every 5 seconds
                        if (lastTimePos > 2 && (DateTime.Now - lastSaveTime).TotalSeconds >= 5)
                        {
                            lastSaveTime = DateTime.Now;
                            SaveProgressToDb(currentActiveFileId, (long)lastTimePos, (long)lastDuration);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (lastTimePos > 2)
                {
                    SaveProgressToDb(currentActiveFileId, (long)lastTimePos, (long)lastDuration);
                }
            }
        }

        private static void MarkEpisodeWatched(int fileId, bool isWatched, long durationSeconds)
        {
            try
            {
                using var db = new AppDbContext();
                var dbItem = db.VideoFiles.Find(fileId);
                if (dbItem != null)
                {
                    dbItem.IsWatched = isWatched;
                    if (isWatched)
                    {
                        dbItem.WatchProgressPercent = 100.0;
                        if (durationSeconds > 0)
                        {
                            dbItem.TotalDurationSeconds = durationSeconds;
                            dbItem.WatchProgressSeconds = durationSeconds;
                        }
                    }
                    else
                    {
                        dbItem.WatchProgressPercent = 0.0;
                        dbItem.WatchProgressSeconds = 0;
                    }
                    dbItem.LastPlayedAt = DateTime.Now;
                    db.SaveChanges();
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
            }
            catch { }
        }

        private static void SaveProgressToDb(int fileId, long timePosSeconds, long durationSeconds)
        {
            try
            {
                using var db = new AppDbContext();
                var dbItem = db.VideoFiles.Find(fileId);
                if (dbItem != null)
                {
                    dbItem.WatchProgressSeconds = timePosSeconds;
                    if (durationSeconds > 0)
                    {
                        dbItem.TotalDurationSeconds = durationSeconds;
                        dbItem.WatchProgressPercent = Math.Clamp((double)timePosSeconds / durationSeconds * 100.0, 0.0, 100.0);
                        if (dbItem.WatchProgressPercent >= 85.0)
                        {
                            dbItem.IsWatched = true;
                        }
                    }
                    dbItem.LastPlayedAt = DateTime.Now;
                    db.SaveChanges();
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
            }
            catch { }
        }
    }
}
