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
using Microsoft.EntityFrameworkCore;
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
                Path.Combine(baseDir, "mpv.exe"),
                Path.Combine(projectRoot, "MPVPlayer", "mpv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MovieManager", "MPVPlayer", "mpv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MovieManager", "MPVPlayer", "mpv.exe")
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

        public static string FormatMediaDisplayTitle(VideoFile vf, string? defaultSeriesTitle = null)
        {
            int? season = vf.Season;
            int? episode = vf.Episode;

            // If season/episode are not set in model, try to extract from filename
            if (season == null || episode == null)
            {
                string fn = vf.FileName ?? Path.GetFileName(vf.FilePath) ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(
                    fn, 
                    @"(?:[sS](\d+)[eE](\d+)|(?:فصل|فصل\s*اول|فصل\s*دوم|فصل\s*سوم|فصل\s*چهارم)?\s*(\d+)?\s*(?:قسمت|اپیزود|قسمت\s*اول|قسمت\s*دوم)?\s*(\d+))",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (m.Success)
                {
                    if (season == null && int.TryParse(m.Groups[1].Value, out int s)) season = s;
                    if (episode == null && int.TryParse(m.Groups[2].Value, out int e)) episode = e;
                }
            }

            string baseName = !string.IsNullOrWhiteSpace(vf.FormattedTitle) 
                ? vf.FormattedTitle 
                : (!string.IsNullOrWhiteSpace(defaultSeriesTitle) ? defaultSeriesTitle : Path.GetFileNameWithoutExtension(vf.FilePath));

            if (season != null && episode != null)
            {
                return $"{baseName} - فصل {season:D2} قسمت {episode:D2}";
            }
            else if (episode != null)
            {
                return $"{baseName} - قسمت {episode:D2}";
            }
            return baseName;
        }

        public static bool PlayMedia(VideoFile file, List<VideoFile>? playlist = null, int initialIndex = 0)
        {
            string? mpvExe = FindMpvPath();
            if (string.IsNullOrEmpty(mpvExe) || !File.Exists(mpvExe))
            {
                return false;
            }

            // 1. Synchronously sync any prior offline progress before launching so DB is updated
            SyncOfflineProgressInternal();

            // 2. Query DB directly for fresh progress of target file and playlist items
            try
            {
                using var db = new AppDbContext();

                VideoFile? dbFile = null;
                if (file.Id > 0)
                {
                    dbFile = db.VideoFiles.FirstOrDefault(v => v.Id == file.Id);
                }
                if (dbFile == null && !string.IsNullOrEmpty(file.FilePath))
                {
                    string lowerPath = file.FilePath.ToLowerInvariant();
                    dbFile = db.VideoFiles.FirstOrDefault(v => v.FilePath.ToLower() == lowerPath);
                }

                if (dbFile != null)
                {
                    file.WatchProgressSeconds = dbFile.WatchProgressSeconds;
                    file.WatchProgressPercent = dbFile.WatchProgressPercent;
                    file.IsWatched = dbFile.IsWatched;
                    file.TotalDurationSeconds = dbFile.TotalDurationSeconds;
                    file.LastPlayedAt = dbFile.LastPlayedAt;

                    // If single standalone file is already watched, reset progress to 0 for fresh replay
                    if ((playlist == null || playlist.Count <= 1) && (dbFile.IsWatched || dbFile.WatchProgressPercent >= 90.0))
                    {
                        if (dbFile.WatchProgressSeconds > 0)
                        {
                            dbFile.WatchProgressSeconds = 0;
                            db.SaveChanges();
                        }
                        file.WatchProgressSeconds = 0;
                        file.WatchProgressPercent = 0;
                    }
                }

                // 3. Auto-discover all Series episodes if playlist not explicitly provided or only has 1 item
                if ((playlist == null || playlist.Count <= 1) && (file.MediaType == "Series" || file.Season != null || file.Episode != null || !string.IsNullOrWhiteSpace(file.FormattedTitle)))
                {
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
                else if (playlist != null && playlist.Count > 0)
                {
                    // If a playlist was provided, refresh all its items from DB
                    var playlistIds = playlist.Where(p => p.Id > 0).Select(p => p.Id).ToList();
                    var dbProgressMap = db.VideoFiles
                        .Where(v => playlistIds.Contains(v.Id))
                        .Select(v => new { v.Id, v.WatchProgressSeconds, v.WatchProgressPercent, v.IsWatched })
                        .ToDictionary(v => v.Id);

                    foreach (var ep in playlist)
                    {
                        if (ep.Id > 0 && dbProgressMap.TryGetValue(ep.Id, out var dbp))
                        {
                            ep.WatchProgressSeconds = dbp.WatchProgressSeconds;
                            ep.WatchProgressPercent = dbp.WatchProgressPercent;
                            ep.IsWatched = dbp.IsWatched;
                        }
                    }
                }

                string pipeName = $"moviemanager_mpv_{file.Id}_{Environment.TickCount64}";
                var args = new List<string>();
                string mpvDir = Path.GetDirectoryName(mpvExe)!;

                try
                {
                    var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManager");
                    var appDataWindowConf = Path.Combine(appData, "window_state.conf");
                    if (!File.Exists(appDataWindowConf))
                    {
                        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop", "window_state.conf");
                        if (File.Exists(legacy)) appDataWindowConf = legacy;
                    }
                    var mpvWindowConfPath = Path.Combine(mpvDir, "window_state.conf");
                    if (File.Exists(appDataWindowConf))
                    {
                        File.Copy(appDataWindowConf, mpvWindowConfPath, true);
                    }

                    var appDataSubConf = Path.Combine(appData, "sub_style.conf");
                    if (!File.Exists(appDataSubConf))
                    {
                        var legacySub = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop", "sub_style.conf");
                        if (File.Exists(legacySub)) appDataSubConf = legacySub;
                    }
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
                string title = FormatMediaDisplayTitle(file);
                args.Add($"--title=\"{title.Replace("\"", "\\\"")}\"");

                // 5. Playlist queue with per-file resume position & forced formatted media title
                var playlistMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (playlist != null && playlist.Count > 0)
                {
                    args.Add($"--playlist-start={initialIndex}");
                    foreach (var ep in playlist)
                    {
                        if (File.Exists(ep.FilePath))
                        {
                            playlistMap[ep.FilePath] = ep.Id;
                            string epTitle = FormatMediaDisplayTitle(ep, file.FormattedTitle);
                            string cleanEpTitle = epTitle.Replace("\"", "\\\"");

                            bool isWatched = ep.IsWatched || ep.WatchProgressPercent >= 90.0;
                            bool shouldResume = !isWatched && ep.WatchProgressSeconds > 5;
                            long startSec = shouldResume ? ep.WatchProgressSeconds : 0;

                            if (startSec > 5)
                            {
                                args.Add($"--{{ --force-media-title=\"{cleanEpTitle}\" --start={startSec} \"{ep.FilePath}\" --}}");
                            }
                            else
                            {
                                args.Add($"--{{ --force-media-title=\"{cleanEpTitle}\" \"{ep.FilePath}\" --}}");
                            }
                        }
                    }
                }
                else
                {
                    playlistMap[file.FilePath] = file.Id;
                    string singleTitle = FormatMediaDisplayTitle(file);
                    string cleanSingleTitle = singleTitle.Replace("\"", "\\\"");

                    bool isWatched = file.IsWatched || file.WatchProgressPercent >= 90.0;
                    bool shouldResume = !isWatched && file.WatchProgressSeconds > 5;
                    long startSec = shouldResume ? file.WatchProgressSeconds : 0;

                    if (startSec > 5)
                    {
                        args.Add($"--{{ --force-media-title=\"{cleanSingleTitle}\" --start={startSec} \"{file.FilePath}\" --}}");
                    }
                    else
                    {
                        args.Add($"--{{ --force-media-title=\"{cleanSingleTitle}\" \"{file.FilePath}\" --}}");
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

            // In-memory cache of episode positions & durations during this session
            var sessionFilePositions = new Dictionary<int, long>();
            var sessionFileDurations = new Dictionary<int, long>();

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
                                        if (lastTimePos > 0)
                                        {
                                            sessionFilePositions[currentActiveFileId] = (long)lastTimePos;
                                        }
                                    }
                                    else if (propName == "duration" && root.TryGetProperty("data", out var durProp) && durProp.ValueKind == JsonValueKind.Number)
                                    {
                                        lastDuration = durProp.GetDouble();
                                        if (lastDuration > 0)
                                        {
                                            sessionFileDurations[currentActiveFileId] = (long)lastDuration;
                                        }
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

                                                long prevPos = (long)lastTimePos;
                                                long prevDur = (long)lastDuration;

                                                if (newIndex > oldIndex)
                                                {
                                                    // ⏩ Moving forward (PageDown / > / Next Episode)
                                                    // 1. Mark previous episode as WATCHED!
                                                    // If watched to end (>= 85% or near end), preserve resume point at 20s before end
                                                    long resumeForOld = prevPos;
                                                    if ((prevDur > 60 && prevPos >= prevDur - 60) || (prevDur <= 60 && prevDur > 0 && prevPos >= prevDur - 5))
                                                    {
                                                        resumeForOld = Math.Max(0, prevDur - 20);
                                                    }

                                                    sessionFilePositions[currentActiveFileId] = resumeForOld;
                                                    MarkEpisodeWatched(currentActiveFileId, isWatched: true, prevDur, resumeForOld);

                                                    // 2. Resume current episode if user was already watching it earlier
                                                    long nextResumePos = 0;
                                                    if (sessionFilePositions.TryGetValue(match.Value, out var memPos) && memPos > 2)
                                                    {
                                                        nextResumePos = memPos;
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            using var db = new AppDbContext();
                                                            var epDb = db.VideoFiles.Find(match.Value);
                                                            if (epDb != null && !epDb.IsWatched && epDb.WatchProgressSeconds > 5)
                                                            {
                                                                nextResumePos = epDb.WatchProgressSeconds;
                                                            }
                                                        }
                                                        catch { }
                                                    }

                                                    if (nextResumePos > 2)
                                                    {
                                                        try
                                                        {
                                                            await writer.WriteLineAsync($"{{\"command\": [\"seek\", {nextResumePos}, \"absolute\"]}}");
                                                        }
                                                        catch { }
                                                    }
                                                }
                                                else if (newIndex < oldIndex)
                                                {
                                                    // ⏪ Moving backward (PageUp / < / Previous Episode)
                                                    // 1. Save current position of the episode we are leaving (e.g. at 12:00) so returning to it resumes here!
                                                    if (prevPos > 2)
                                                    {
                                                        sessionFilePositions[currentActiveFileId] = prevPos;
                                                        SaveProgressToDb(currentActiveFileId, prevPos, prevDur);
                                                    }

                                                    // 2. For the episode we are returning to:
                                                    //    REMOVE watched tick (isWatched = false)!
                                                    //    Seek to where user previously pressed Next, or 20s before end!
                                                    long targetSeekPos = 0;
                                                    long targetDuration = 0;

                                                    if (sessionFilePositions.TryGetValue(match.Value, out var memPrev) && memPrev > 0)
                                                    {
                                                        targetSeekPos = memPrev;
                                                    }

                                                    try
                                                    {
                                                        using var db = new AppDbContext();
                                                        var epDb = db.VideoFiles.Find(match.Value);
                                                        if (epDb != null)
                                                        {
                                                            targetDuration = epDb.TotalDurationSeconds;
                                                            if (targetSeekPos <= 0 && epDb.WatchProgressSeconds > 0)
                                                            {
                                                                targetSeekPos = epDb.WatchProgressSeconds;
                                                            }
                                                        }
                                                    }
                                                    catch { }

                                                    if (sessionFileDurations.TryGetValue(match.Value, out var memDur) && memDur > targetDuration)
                                                    {
                                                        targetDuration = memDur;
                                                    }

                                                    if ((targetSeekPos <= 0 || (targetDuration > 30 && targetSeekPos >= targetDuration - 25)) && targetDuration > 30)
                                                    {
                                                        targetSeekPos = Math.Max(0, targetDuration - 20);
                                                    }

                                                    sessionFilePositions[match.Value] = targetSeekPos;
                                                    MarkEpisodeWatched(match.Value, isWatched: false, targetDuration, targetSeekPos);

                                                    if (targetSeekPos > 0)
                                                    {
                                                        try
                                                        {
                                                            await writer.WriteLineAsync($"{{\"command\": [\"seek\", {targetSeekPos}, \"absolute\"]}}");
                                                        }
                                                        catch { }
                                                    }
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

        private static void MarkEpisodeWatched(int fileId, bool isWatched, long durationSeconds, long resumeSeconds = 0)
        {
            try
            {
                using var db = new AppDbContext();
                var dbItem = db.VideoFiles.Find(fileId);
                if (dbItem != null)
                {
                    dbItem.IsWatched = isWatched;
                    if (durationSeconds > 0)
                    {
                        dbItem.TotalDurationSeconds = durationSeconds;
                    }

                    if (isWatched)
                    {
                        dbItem.WatchProgressPercent = 100.0;
                        // Preserve the resume point (or 20s before end) so returning via Prev resumes accurately
                        dbItem.WatchProgressSeconds = resumeSeconds;
                    }
                    else
                    {
                        // Unmarking watched: set progress to resumeSeconds
                        dbItem.WatchProgressSeconds = resumeSeconds;
                        if (dbItem.TotalDurationSeconds > 0 && resumeSeconds > 0)
                        {
                            dbItem.WatchProgressPercent = Math.Clamp((double)resumeSeconds / dbItem.TotalDurationSeconds * 100.0, 0.0, 100.0);
                        }
                        else
                        {
                            dbItem.WatchProgressPercent = 0.0;
                        }
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
                    bool becameWatched = false;
                    if (durationSeconds > 0)
                    {
                        dbItem.TotalDurationSeconds = durationSeconds;
                        dbItem.WatchProgressPercent = Math.Clamp((double)timePosSeconds / durationSeconds * 100.0, 0.0, 100.0);
                        bool isNearEnd = (durationSeconds > 60 && timePosSeconds >= durationSeconds - 60) ||
                                         (durationSeconds <= 60 && durationSeconds > 0 && timePosSeconds >= durationSeconds - 5);
                        if (isNearEnd && !dbItem.IsWatched)
                        {
                            dbItem.IsWatched = true;
                            becameWatched = true;
                        }
                    }

                    if (dbItem.IsWatched || dbItem.WatchProgressPercent >= 90.0)
                    {
                        // If finished or near end, save 20s before end
                        if (durationSeconds > 30 && timePosSeconds >= durationSeconds - 25)
                        {
                            dbItem.WatchProgressSeconds = Math.Max(0, durationSeconds - 20);
                        }
                        else if (timePosSeconds > 0)
                        {
                            dbItem.WatchProgressSeconds = timePosSeconds;
                        }
                    }
                    else
                    {
                        dbItem.WatchProgressSeconds = timePosSeconds;
                    }

                    dbItem.LastPlayedAt = DateTime.Now;
                    db.SaveChanges();

                    if (becameWatched)
                    {
                        WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                    }
                }
            }
            catch { }
        }

        public static void SyncOfflineProgress()
        {
            Task.Run(SyncOfflineProgressInternal);
        }

        public static void SyncOfflineProgressInternal()
        {
            try
                {
                    var candidates = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManager", "playback_sync.json"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop", "playback_sync.json")
                    };

                    foreach (var syncPath in candidates)
                    {
                        if (!File.Exists(syncPath)) continue;

                        string json = File.ReadAllText(syncPath);
                        if (string.IsNullOrWhiteSpace(json)) continue;

                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                        using var db = new AppDbContext();
                        bool dbModified = false;

                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            string filePath = prop.Name;
                            var val = prop.Value;

                            long timePos = val.TryGetProperty("timePos", out var tp) && tp.TryGetInt64(out var tVal) ? tVal : 0;
                            long duration = val.TryGetProperty("duration", out var dur) && dur.TryGetInt64(out var dVal) ? dVal : 0;
                            bool isWatched = val.TryGetProperty("isWatched", out var iw) && iw.GetBoolean();
                            double percent = val.TryGetProperty("percent", out var per) && per.TryGetDouble(out var pVal) ? pVal : 0;

                            if (timePos <= 0 && !isWatched) continue;

                            string lowerPath = filePath.ToLowerInvariant();
                            var dbItem = db.VideoFiles.FirstOrDefault(v => v.FilePath.ToLower() == lowerPath);
                            if (dbItem != null)
                            {
                                bool updated = false;
                                if (isWatched && !dbItem.IsWatched)
                                {
                                    dbItem.IsWatched = true;
                                    dbItem.WatchProgressPercent = 100.0;
                                    if (duration > 0)
                                    {
                                        dbItem.TotalDurationSeconds = duration;
                                    }
                                    if (timePos > 0)
                                    {
                                        dbItem.WatchProgressSeconds = timePos;
                                    }
                                    else if (duration > 30)
                                    {
                                        dbItem.WatchProgressSeconds = Math.Max(0, duration - 20);
                                    }
                                    updated = true;
                                }
                                else if (!dbItem.IsWatched && timePos > dbItem.WatchProgressSeconds)
                                {
                                    if (duration > 0) dbItem.TotalDurationSeconds = duration;
                                    if (percent > 0) dbItem.WatchProgressPercent = percent;
                                    bool isNearEnd = (duration > 60 && timePos >= duration - 60) ||
                                                     (duration <= 60 && duration > 0 && timePos >= duration - 5);
                                    if (isNearEnd)
                                    {
                                        dbItem.IsWatched = true;
                                        dbItem.WatchProgressSeconds = (duration > 30 && timePos >= duration - 25) ? Math.Max(0, duration - 20) : timePos;
                                    }
                                    else
                                    {
                                        dbItem.WatchProgressSeconds = timePos;
                                    }
                                    updated = true;
                                }

                            if (updated)
                            {
                                dbItem.LastPlayedAt = DateTime.Now;
                                dbModified = true;
                            }
                        }
                    }

                    if (dbModified)
                    {
                        db.SaveChanges();
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to sync offline MPV playback progress", ex);
            }
        }
    }
}
