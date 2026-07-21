using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class MpvPlayerService
    {
        private const string MpvFileName = "mpv.exe";
        
        public static string GetMpvPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, MpvFileName);
            if (File.Exists(path)) return path;
            
            // Also check if it's in system PATH
            return MpvFileName;
        }

        public static bool IsMpvAvailable()
        {
            try
            {
                var path = GetMpvPath();
                if (path != MpvFileName) return true; // Found in local dir
                
                // Test if it's in PATH
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = MpvFileName,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });
                process?.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task PlayVideoAsync(string filePath, int startSeconds, Action<double, double, bool> onProgressUpdate)
        {
            string pipeName = $"mpv_pipe_{Guid.NewGuid():N}";
            string ipcPath = $@"\\.\pipe\{pipeName}";
            
            var arguments = $"\"{filePath}\" --input-ipc-server={ipcPath} --start={startSeconds}";

            var processInfo = new ProcessStartInfo
            {
                FileName = GetMpvPath(),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Process process;
            try
            {
                process = Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to start mpv.exe", ex);
                throw new FileNotFoundException("نرم افزار mpv پیدا نشد. لطفاً آن را دانلود کرده و در مسیر برنامه قرار دهید.");
            }

            if (process == null) return;

            // Give MPV a moment to start the IPC server
            await Task.Delay(1000);

            try
            {
                using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                // Connect with a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await pipeClient.ConnectAsync(cts.Token);

                // Observe properties: time-pos, duration, eof-reached
                await SendCommandAsync(pipeClient, new { command = new object[] { "observe_property", 1, "time-pos" } });
                await SendCommandAsync(pipeClient, new { command = new object[] { "observe_property", 2, "duration" } });
                await SendCommandAsync(pipeClient, new { command = new object[] { "observe_property", 3, "eof-reached" } });

                using var reader = new StreamReader(pipeClient);
                double currentTime = 0;
                double totalDuration = 0;

                while (!process.HasExited)
                {
                    if (pipeClient.IsConnected)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line)) continue;

                        try
                        {
                            var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("event", out var eventProp) && eventProp.GetString() == "property-change")
                            {
                                var name = doc.RootElement.GetProperty("name").GetString();
                                
                                if (name == "time-pos" && doc.RootElement.TryGetProperty("data", out var timeData) && timeData.ValueKind == JsonValueKind.Number)
                                {
                                    currentTime = timeData.GetDouble();
                                }
                                else if (name == "duration" && doc.RootElement.TryGetProperty("data", out var durData) && durData.ValueKind == JsonValueKind.Number)
                                {
                                    totalDuration = durData.GetDouble();
                                }
                                else if (name == "eof-reached" && doc.RootElement.TryGetProperty("data", out var eofData) && eofData.ValueKind == JsonValueKind.True)
                                {
                                    onProgressUpdate?.Invoke(totalDuration, 100, true);
                                    break; // Video ended
                                }

                                if (totalDuration > 0 && currentTime > 0)
                                {
                                    double percent = (currentTime / totalDuration) * 100;
                                    bool isFinished = percent > 90; // Mark as finished if >90% watched
                                    onProgressUpdate?.Invoke(currentTime, percent, isFinished);
                                }
                            }
                        }
                        catch
                        {
                            // Ignore parsing errors for individual lines
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error in MPV IPC", ex);
            }
        }

        private static async Task SendCommandAsync(NamedPipeClientStream pipe, object command)
        {
            var json = JsonSerializer.Serialize(command) + "\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, 0, bytes.Length);
            await pipe.FlushAsync();
        }
    }
}
