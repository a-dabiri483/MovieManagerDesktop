using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class EmbeddedSubtitleTrackInfo
    {
        public int StreamIndex { get; set; }
        public int SubtitleIndex { get; set; }
        public string Language { get; set; } = "und";
        public string Title { get; set; } = string.Empty;
        public string Codec { get; set; } = string.Empty;
        public string DisplayName => !string.IsNullOrEmpty(Title) 
            ? $"{Title} ({Language})" 
            : $"زیرنویس {Language} (#{SubtitleIndex + 1})";
    }

    public static class EmbeddedSubtitleExtractorService
    {
        private static string? _ffmpegPath;

        public static string? GetFFmpegPath()
        {
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                return _ffmpegPath;

            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "ffmpeg.exe"),
                @"C:\Users\ALI\CascadeProjects\MovieManagerDesktop\ffmpeg7.1_extracted\ffmpeg-n7.1-latest-win64-gpl-shared-7.1\bin\ffmpeg.exe",
                @"C:\Users\ALI\CascadeProjects\MovieManagerDesktop\ffmpeg_extracted\ffmpeg-master-latest-win64-gpl-shared\bin\ffmpeg.exe",
                @"C:\Users\ALI\CascadeProjects\MovieManager\ffmpeg_folder\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe"
            };

            foreach (var p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    _ffmpegPath = p;
                    return p;
                }
            }

            return "ffmpeg.exe";
        }

        public static async Task<List<EmbeddedSubtitleTrackInfo>> GetEmbeddedSubtitleTracksAsync(string videoPath)
        {
            var result = new List<EmbeddedSubtitleTrackInfo>();
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) return result;

            string? ffmpeg = GetFFmpegPath();
            if (string.IsNullOrEmpty(ffmpeg)) return result;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-hide_banner -i \"{videoPath}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return result;

                string stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var regex = new Regex(@"Stream\s+#0:(\d+)(?:\(([^)]+)\))?.*Subtitle:\s*([^,\r\n]+)", RegexOptions.IgnoreCase);
                var matches = regex.Matches(stderr);

                int subIndex = 0;
                foreach (Match match in matches)
                {
                    int streamIndex = int.Parse(match.Groups[1].Value);
                    string lang = match.Groups[2].Success ? match.Groups[2].Value : "und";
                    string codec = match.Groups[3].Value.Trim();

                    string title = "";
                    var titleMatch = Regex.Match(stderr, $@"Stream\s+#0:{streamIndex}.*?title\s*:\s*([^\r\n]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        title = titleMatch.Groups[1].Value.Trim();
                    }

                    result.Add(new EmbeddedSubtitleTrackInfo
                    {
                        StreamIndex = streamIndex,
                        SubtitleIndex = subIndex++,
                        Language = lang,
                        Title = title,
                        Codec = codec
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to probe subtitle tracks with FFmpeg", ex);
            }

            return result;
        }

        public static async Task<string?> ExtractEmbeddedSubtitleToSrtAsync(string videoPath, int subtitleIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) return null;

            string? ffmpeg = GetFFmpegPath();
            if (string.IsNullOrEmpty(ffmpeg)) return null;

            try
            {
                string cacheDir = Path.Combine(Path.GetTempPath(), "MovieManagerDesktop", "ExtractedSubs");
                Directory.CreateDirectory(cacheDir);

                long fileTime = 0;
                try { fileTime = File.GetLastWriteTimeUtc(videoPath).Ticks; } catch { }
                string safeName = Path.GetFileNameWithoutExtension(videoPath);
                string outPath = Path.Combine(cacheDir, $"{safeName}_{fileTime}_sub_{subtitleIndex}.srt");

                if (File.Exists(outPath))
                {
                    var fi = new FileInfo(outPath);
                    if (fi.Length > 20)
                    {
                        return outPath;
                    }
                    else
                    {
                        try { File.Delete(outPath); } catch { }
                    }
                }

                // 1. Convert stream to standard SRT with -c:s srt
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-nostdin -y -hide_banner -loglevel error -i \"{videoPath}\" -map 0:s:{subtitleIndex} -c:s srt \"{outPath}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = new Process { StartInfo = psi })
                {
                    proc.Start();
                    var errTask = proc.StandardError.ReadToEndAsync();
                    var outTask = proc.StandardOutput.ReadToEndAsync();

                    bool finished = await Task.Run(() => proc.WaitForExit(7000));
                    if (!finished)
                    {
                        try { proc.Kill(); } catch { }
                    }
                    else
                    {
                        await Task.WhenAll(errTask, outTask);
                    }
                }

                if (File.Exists(outPath) && new FileInfo(outPath).Length > 10)
                {
                    return outPath;
                }

                // 2. Fallback attempt: extract raw stream (e.g. ASS/SSA/VTT)
                string outFallbackPath = Path.Combine(cacheDir, $"{safeName}_{fileTime}_sub_{subtitleIndex}.ass");
                var psiFallback = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-nostdin -y -hide_banner -loglevel error -i \"{videoPath}\" -map 0:s:{subtitleIndex} -c:s copy \"{outFallbackPath}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = new Process { StartInfo = psiFallback })
                {
                    proc.Start();
                    var errTask = proc.StandardError.ReadToEndAsync();
                    var outTask = proc.StandardOutput.ReadToEndAsync();

                    bool finished = await Task.Run(() => proc.WaitForExit(7000));
                    if (!finished)
                    {
                        try { proc.Kill(); } catch { }
                    }
                    else
                    {
                        await Task.WhenAll(errTask, outTask);
                    }
                }

                if (File.Exists(outFallbackPath) && new FileInfo(outFallbackPath).Length > 10)
                {
                    return outFallbackPath;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to extract embedded subtitle", ex);
            }

            return null;
        }
    }
}
