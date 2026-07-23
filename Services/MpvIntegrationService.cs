using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MovieManagerDesktop.Controls;

namespace MovieManagerDesktop.Services
{
    public class MpvIntegrationService
    {
        private const string MpvDownloadUrl = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20240317/mpv-x86_64-20240317-git-0a373ff.zip"; // Stable fallback URL or we can fetch latest
        private const string PipeName = "moviemanager_mpv";
        
        private static StreamWriter _ipcWriter;
        private static MovieManagerDesktop.Views.PlayerSettingsWindow _currentMenuWindow;

        public static async Task<(long finalPositionSeconds, long totalDuration, bool isCompleted)> PlayVideoAsync(string videoPath, long startPositionSeconds, string? title = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mpvExePath = Path.Combine(baseDir, "Plugins", "MPV", "mpv.exe");

            if (!File.Exists(mpvExePath))
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    ToastService.Instance.ShowError("فایل اجرایی MPV یافت نشد. نصب افزونه ناقص است.");
                });
                return (startPositionSeconds, 0, false);
            }

            var startArg = startPositionSeconds > 0 ? $"--start={startPositionSeconds}" : "";
            var pipeArg = $"--input-ipc-server=\\\\.\\pipe\\{PipeName}";
            var titleArg = !string.IsNullOrEmpty(title) ? $"--force-media-title=\"{title}\"" : "";
            var args = $"\"{videoPath}\" {startArg} {pipeArg} {titleArg}";

            var processInfo = new ProcessStartInfo
            {
                FileName = mpvExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true // True to hide console window of mpv
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            // IPC Connection
            long currentPos = startPositionSeconds;
            long totalDuration = 0;
            bool isCompleted = false;

            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                // Wait for MPV to create the pipe
                await pipeClient.ConnectAsync(5000); // 5 seconds timeout

                using var reader = new StreamReader(pipeClient, Encoding.UTF8);
                await using var writer = new StreamWriter(pipeClient, Encoding.UTF8) { AutoFlush = true };

                // Subscribe to eof-reached
                await writer.WriteLineAsync("{\"command\": [\"observe_property\", 1, \"eof-reached\"]}");

                var pollTask = Task.Run(async () => {
                    try {
                        while (!process.HasExited) {
                            await writer.WriteLineAsync("{\"command\": [\"get_property\", \"time-pos\"], \"request_id\": 100}");
                            await writer.WriteLineAsync("{\"command\": [\"get_property\", \"duration\"], \"request_id\": 200}");
                            await Task.Delay(1000);
                        }
                    } catch { }
                });

                while (!process.HasExited)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("event", out var eventProp))
                        {
                            var eventName = eventProp.GetString();
                            if (eventName == "property-change")
                            {
                                var propName = root.GetProperty("name").GetString();
                                if (propName == "eof-reached" && root.TryGetProperty("data", out var eofData) && eofData.ValueKind != JsonValueKind.Null)
                                {
                                    if (eofData.ValueKind == JsonValueKind.True || eofData.ValueKind == JsonValueKind.False)
                                    {
                                        isCompleted = eofData.GetBoolean();
                                    }
                                }
                            }
                        }
                        else if (root.TryGetProperty("request_id", out var reqIdProp) && reqIdProp.ValueKind == JsonValueKind.Number)
                        {
                            var reqId = reqIdProp.GetInt32();
                            if (reqId == 100 && root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Number)
                            {
                                currentPos = (long)dataProp.GetDouble();
                            }
                            else if (reqId == 200 && root.TryGetProperty("data", out var durProp) && durProp.ValueKind == JsonValueKind.Number)
                            {
                                totalDuration = (long)durProp.GetDouble();
                            }
                        }
                    }
                    catch { /* Ignore parse errors */ }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("IPC connection to MPV failed", ex);
            }
            finally
            {
                _ipcWriter = null;
            }

            await process.WaitForExitAsync();
            return (currentPos, totalDuration, isCompleted);
        }

        public static async Task SendCommandAsync(string commandStr)
        {
            try
            {
                if (_ipcWriter == null) return;
                
                var parts = commandStr.Split(' ');
                var jsonArr = string.Join(", ", System.Linq.Enumerable.Select(parts, p => $"\"{p}\""));
                await _ipcWriter.WriteLineAsync($"{{\"command\": [{jsonArr}]}}");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to send command to MPV", ex);
            }
        }

        public static async Task PlayPlaylistAsync(System.Collections.Generic.List<MovieManagerDesktop.Models.VideoFile> playlist, int startIndex, Action<int, long, long, bool> progressCallback)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mpvExePath = Path.Combine(baseDir, "Plugins", "MPV", "mpv.exe");

            if (!File.Exists(mpvExePath))
            {
                throw new FileNotFoundException($"MPV player not found at: {mpvExePath}");
            }

            var playlistFile = Path.Combine(Path.GetTempPath(), "moviemanager_playlist.m3u");
            var m3uLines = new System.Collections.Generic.List<string> { "#EXTM3U" };
            var startPositions = new System.Collections.Generic.List<long>();

            foreach (var ep in playlist)
            {
                string title = !string.IsNullOrEmpty(ep.FormattedTitle) ? ep.FormattedTitle : (ep.FileName ?? "Unknown Title");
                if (ep.Season.HasValue && ep.Episode.HasValue)
                {
                    title = $"{title} - فصل {ep.Season} قسمت {ep.Episode}";
                }
                m3uLines.Add($"#EXTINF:-1,{title}");
                m3uLines.Add(ep.FilePath);
                startPositions.Add(ep.WatchProgressPercent >= 100 ? 0 : ep.WatchProgressSeconds);
            }

            File.WriteAllLines(playlistFile, m3uLines);

            var pipeArg = $"--input-ipc-server=\\\\.\\pipe\\{PipeName}";
            var args = $"--playlist=\"{playlistFile}\" --playlist-start={startIndex} {pipeArg}";

            var processInfo = new ProcessStartInfo
            {
                FileName = mpvExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipeClient.ConnectAsync(5000);
                using var reader = new StreamReader(pipeClient, Encoding.UTF8);
                await using var writer = new StreamWriter(pipeClient, Encoding.UTF8) { AutoFlush = true };
                
                _ipcWriter = writer;

                await writer.WriteLineAsync("{\"command\": [\"request_log_messages\", \"info\"]}");

                int currentPlaylistIndex = startIndex;
                long currentPos = startPositions != null && startPositions.Count > startIndex ? startPositions[startIndex] : 0;
                long totalDuration = 0;
                bool isEof = false;

                var pollTask = Task.Run(async () => {
                    try {
                        while (!process.HasExited) {
                            await writer.WriteLineAsync("{\"command\": [\"get_property\", \"time-pos\"], \"request_id\": 100}");
                            await writer.WriteLineAsync("{\"command\": [\"get_property\", \"duration\"], \"request_id\": 200}");
                            await Task.Delay(1000);
                        }
                    } catch { }
                });

                while (!process.HasExited)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("wpf-show-menu") || line.Contains("WPF_ACTION:SHOW_MENU"))
                    {
                        try 
                        {
                            Application.Current.Dispatcher.Invoke(() => {
                                if (_currentMenuWindow != null && _currentMenuWindow.IsLoaded) {
                                    _currentMenuWindow.Focus();
                                    return;
                                }
                                _currentMenuWindow = new MovieManagerDesktop.Views.PlayerSettingsWindow();
                                _currentMenuWindow.Closed += (s, e) => _currentMenuWindow = null;
                                _currentMenuWindow.Show();
                            });
                        } 
                        catch (Exception ex)
                        {
                            LoggerService.Error("Failed to open WPF menu from IPC", ex);
                        }
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("event", out var eventProp))
                        {
                            var eventName = eventProp.GetString();
                            if (eventName == "end-file")
                            {
                                var reason = root.GetProperty("reason").GetString();
                                if (reason == "eof") isEof = true;
                            }
                            else if (eventName == "start-file")
                            {
                                if (root.TryGetProperty("playlist_entry_id", out var idProp))
                                {
                                    int newIndex = idProp.GetInt32() - 1;
                                    
                                    if (newIndex != currentPlaylistIndex)
                                    {
                                        progressCallback?.Invoke(currentPlaylistIndex, currentPos, totalDuration, true);
                                    }
                                    
                                    currentPlaylistIndex = newIndex;
                                    currentPos = 0; 
                                    totalDuration = 0;
                                    isEof = false;
                                }
                            }
                            else if (eventName == "file-loaded")
                            {
                                if (startPositions != null && currentPlaylistIndex >= 0 && currentPlaylistIndex < startPositions.Count)
                                {
                                    long targetPos = startPositions[currentPlaylistIndex];
                                    if (targetPos > 0)
                                    {
                                        await writer.WriteLineAsync($"{{\"command\": [\"set_property\", \"time-pos\", {targetPos}]}}");
                                    }
                                }
                            }
                        }
                        else if (root.TryGetProperty("request_id", out var reqIdProp) && reqIdProp.ValueKind == JsonValueKind.Number)
                        {
                            var reqId = reqIdProp.GetInt32();
                            if (reqId == 100 && root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Number)
                            {
                                currentPos = (long)dataProp.GetDouble();
                                // Send real-time updates for UI tracking
                                progressCallback?.Invoke(currentPlaylistIndex, currentPos, totalDuration, false);
                            }
                            else if (reqId == 200 && root.TryGetProperty("data", out var durProp) && durProp.ValueKind == JsonValueKind.Number)
                            {
                                totalDuration = (long)durProp.GetDouble();
                            }
                        }
                    }
                    catch { }
                }
                
                // Process exited. We report the final state of the CURRENT file.
                progressCallback?.Invoke(currentPlaylistIndex, currentPos, totalDuration, isEof);
            }
            catch (Exception ex)
            {
                LoggerService.Error("IPC connection to MPV failed during playlist", ex);
            }

            await process.WaitForExitAsync();
        }
    }
}
