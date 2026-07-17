using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class ScannerService
    {
        private readonly IdentifyMediaService _identifyService;
        private readonly FileNameParser _parser;
        private readonly string[] _videoExtensions = { ".mp4", ".mkv", ".avi", ".mov" };

        public ScannerService()
        {
            _identifyService = new IdentifyMediaService();
            _parser = new FileNameParser();
        }

        public async Task<List<VideoFile>> ScanDirectoryAsync(string directoryPath, IProgress<string> progress, CancellationToken cancellationToken)
        {
            var foundFiles = new List<VideoFile>();
            
            if (!Directory.Exists(directoryPath))
                return foundFiles;

            await Task.Run(async () =>
            {
                try
                {
                    using var db = new AppDbContext();
                    var existingFiles = db.VideoFiles.Select(v => v.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        ReturnSpecialDirectories = false
                    };
                    
                    progress?.Report($"در حال جستجوی فایل‌های ویدئویی در {directoryPath}");
                    var allVideoFiles = Directory.EnumerateFiles(directoryPath, "*.*", options)
                        .Where(f => _videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    var newFilesPaths = allVideoFiles.Where(f => !existingFiles.Contains(f)).ToList();
                    
                    if (newFilesPaths.Count == 0)
                    {
                        progress?.Report("فایل جدیدی یافت نشد.");
                        return;
                    }

                    progress?.Report($"تعداد {newFilesPaths.Count} فایل ویدئویی جدید یافت شد. در حال پردازش...");

                    var parsedFiles = new List<VideoFile>();
                    foreach (var f in newFilesPaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var fileInfo = new FileInfo(f);
                        var parsed = _parser.Parse(fileInfo.Name, f);

                        var video = new VideoFile
                        {
                            FilePath = f,
                            FileName = fileInfo.Name,
                            FormattedTitle = parsed.ParsedTitle,
                            Year = parsed.Year?.ToString(),
                            Season = parsed.Season,
                            Episode = parsed.Episode,
                            Quality = parsed.Quality ?? "نامشخص",
                            MediaType = parsed.MediaType,
                            FileSizeBytes = fileInfo.Length,
                            DateAdded = DateTime.Now
                        };
                        parsedFiles.Add(video);
                    }

                    progress?.Report($"اسکن با موفقیت تمام شد. {parsedFiles.Count} فایل جدید برای ثبت آماده است.");
                    foundFiles = parsedFiles;
                }
                catch (Exception ex)
                {
                    progress?.Report($"خطا در حین اسکن: {ex.Message}");
                }

            }, cancellationToken);
            
            return foundFiles;
        }
    }
}
