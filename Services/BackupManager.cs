using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using MovieManagerDesktop.Data;
using System.Text.Json;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public class BackupManager
    {
        private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
        private const string ApplicationName = "Movie Manager Desktop";
        private const string CredentialsFile = "credentials.json";
        private static readonly string TokenStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieManager", "GoogleAuth");

        public static bool IsBackupNeeded()
        {
            var settings = SettingsManager.LoadSettings();
            if (!settings.IsLocalAutoBackupEnabled && !settings.IsGoogleDriveAutoBackupEnabled)
            {
                return false;
            }

            if (settings.BackupFrequencyIndex == 1) // Daily
            {
                if (DateTime.Now.Date <= settings.LastBackupTime.Date)
                    return false;
            }
            else if (settings.BackupFrequencyIndex == 2) // Weekly
            {
                if ((DateTime.Now - settings.LastBackupTime).TotalDays < 7)
                    return false;
            }

            return true;
        }

        public static async Task<bool> RunBackupAsync()
        {
            var settings = SettingsManager.LoadSettings();
            if (!settings.IsLocalAutoBackupEnabled && !settings.IsGoogleDriveAutoBackupEnabled)
            {
                return true;
            }

            // Check Frequency
            if (settings.BackupFrequencyIndex == 1) // Daily
            {
                if (DateTime.Now.Date <= settings.LastBackupTime.Date)
                    return true;
            }
            else if (settings.BackupFrequencyIndex == 2) // Weekly
            {
                if ((DateTime.Now - settings.LastBackupTime).TotalDays < 7)
                    return true;
            }

            try
            {
                // Generate the backup JSON string
                var backupJson = await GenerateBackupJsonAsync(settings);
                
                string localBackupFilePath = string.Empty;

                if (settings.IsLocalAutoBackupEnabled)
                {
                    localBackupFilePath = await RunLocalBackupAsync(settings, backupJson);
                }

                if (settings.IsGoogleDriveAutoBackupEnabled)
                {
                    // Use a temp file if local backup wasn't enabled
                    bool usingTempFile = false;
                    if (string.IsNullOrEmpty(localBackupFilePath))
                    {
                        localBackupFilePath = Path.GetTempFileName();
                        System.IO.File.WriteAllText(localBackupFilePath, backupJson);
                        usingTempFile = true;
                    }

                    await RunGoogleDriveBackupAsync(localBackupFilePath);

                    if (usingTempFile)
                    {
                        System.IO.File.Delete(localBackupFilePath);
                    }
                }

                // Update Last Backup Time
                settings.LastBackupTime = DateTime.Now;
                SettingsManager.SaveSettings(settings);

                return true;
            }
            catch (Exception ex)
            {
                // In a real app we'd log this, but for now we'll just return false.
                System.Diagnostics.Debug.WriteLine($"Auto Backup Error: {ex.Message}");
                return false;
            }
        }

        public class FullBackupModel
        {
            public List<VideoFile> VideoFiles { get; set; } = new();
            public List<TvSeason> TvSeasons { get; set; } = new();
            public List<TvEpisode> TvEpisodes { get; set; } = new();
            public SettingsModel Settings { get; set; } = new();
        }

        private static async Task<string> GenerateBackupJsonAsync(SettingsModel settings)
        {
            using var db = new AppDbContext();
            var backupModel = new FullBackupModel
            {
                VideoFiles = await db.VideoFiles.ToListAsync(),
                TvSeasons = await db.TvSeasons.ToListAsync(),
                TvEpisodes = await db.TvEpisodes.ToListAsync(),
                Settings = settings
            };
            return JsonSerializer.Serialize(backupModel, new JsonSerializerOptions { WriteIndented = true });
        }

        private static async Task<string> RunLocalBackupAsync(SettingsModel settings, string backupJson)
        {
            string backupDir = settings.LocalAutoBackupPath;
            if (string.IsNullOrWhiteSpace(backupDir))
            {
                backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MovieManagerBackups");
            }

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"Backup_{timestamp}.json";
            string backupFilePath = Path.Combine(backupDir, backupFileName);

            await System.IO.File.WriteAllTextAsync(backupFilePath, backupJson);

            // Cleanup old backups (keep last 5)
            var directoryInfo = new DirectoryInfo(backupDir);
            var backupFiles = directoryInfo.GetFiles("Backup_*.json")
                                           .OrderByDescending(f => f.CreationTime)
                                           .ToList();

            if (backupFiles.Count > 5)
            {
                foreach (var file in backupFiles.Skip(5))
                {
                    file.Delete();
                }
            }

            return backupFilePath;
        }

        public static async Task ForceGoogleDriveBackupAsync(IProgress<double> progress = null, IProgress<string> textProgress = null)
        {
            var settings = SettingsManager.LoadSettings();
            
            if (textProgress != null) textProgress.Report("در حال جمع‌آوری اطلاعات از دیتابیس...");
            var backupJson = await GenerateBackupJsonAsync(settings);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var localBackupFilePath = Path.Combine(Path.GetTempPath(), $"MovieManager_Backup_{timestamp}.json");
            System.IO.File.WriteAllText(localBackupFilePath, backupJson);

            long fileLength = new FileInfo(localBackupFilePath).Length;
            string formattedSize = fileLength > 1024 * 1024 
                ? $"{(fileLength / 1024f / 1024f):F1} MB" 
                : $"{(fileLength / 1024f):F1} KB";

            if (textProgress != null) textProgress.Report($"حجم بکاپ محاسبه شد: {formattedSize}. آماده‌سازی آپلود...");
            await Task.Delay(1000); // Give user time to see the size

            try
            {
                await RunGoogleDriveBackupAsync(localBackupFilePath, progress, textProgress);
            }
            finally
            {
                System.IO.File.Delete(localBackupFilePath);
            }
        }

        public static async Task DisconnectGoogleDriveAsync()
        {
            if (Directory.Exists(TokenStorePath))
            {
                Directory.Delete(TokenStorePath, true);
            }
            await Task.CompletedTask;
        }

        public static bool IsConnectedToGoogleDrive()
        {
            return Directory.Exists(TokenStorePath) && Directory.GetFiles(TokenStorePath).Length > 0;
        }

        private static async Task<DriveService> GetDriveServiceAsync()
        {
            if (!System.IO.File.Exists(CredentialsFile))
            {
                throw new FileNotFoundException($"برای ارتباط با گوگل درایو فایل {CredentialsFile} نیاز است. لطفاً آن را در پوشه اصلی برنامه قرار دهید.");
            }

            UserCredential credential;
            using (var stream = new FileStream(CredentialsFile, FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(TokenStorePath, true));
            }

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }

        public static async Task ConnectToGoogleDriveAsync()
        {
            await GetDriveServiceAsync();
        }

        public static async Task RunGoogleDriveBackupAsync(string filePath, IProgress<double> progress = null, IProgress<string> textProgress = null)
        {
            if (textProgress != null) textProgress.Report("در حال برقراری ارتباط با گوگل درایو...");
            var service = await GetDriveServiceAsync();

            // Find or create "MovieManagerBackups" folder
            if (textProgress != null) textProgress.Report("در حال جستجوی پوشه مقصد...");
            string folderName = "MovieManagerBackups";
            string folderId = await GetOrCreateFolderAsync(service, folderName);

            // Upload the file
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(filePath),
                Parents = new List<string> { folderId }
            };

            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                long fileLength = stream.Length;
                request = service.Files.Create(fileMetadata, stream, "application/json");
                request.Fields = "id";
                request.ChunkSize = Google.Apis.Upload.ResumableUpload.MinimumChunkSize; // Minimum chunk size (256KB)
                
                if (progress != null)
                {
                    request.ProgressChanged += (Google.Apis.Upload.IUploadProgress uploadProgress) =>
                    {
                        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Uploading)
                        {
                            double percentage = fileLength > 0 ? (double)uploadProgress.BytesSent / fileLength * 100 : 0;
                            progress.Report(percentage);
                            
                            string sentSize = uploadProgress.BytesSent > 1024 * 1024 
                                ? $"{(uploadProgress.BytesSent / 1024f / 1024f):F1} MB" 
                                : $"{(uploadProgress.BytesSent / 1024f):F0} KB";
                            if (textProgress != null) textProgress.Report($"ارسال شده: {sentSize} ({percentage:F1}%)");
                        }
                    };
                }
                
                if (textProgress != null) textProgress.Report("شروع آپلود فایل...");
                
                // If file is very small (<256KB), simulate progress for a moment so UI looks good
                if (fileLength < Google.Apis.Upload.ResumableUpload.MinimumChunkSize)
                {
                    if (progress != null) progress.Report(50.0);
                    if (textProgress != null) textProgress.Report($"ارسال شده: {fileLength / 1024f / 2:F0} KB (50.0%)");
                    await Task.Delay(800);
                }

                var response = await request.UploadAsync();
                
                if (response.Status == Google.Apis.Upload.UploadStatus.Completed)
                {
                    if (progress != null) progress.Report(100.0);
                    if (textProgress != null) textProgress.Report("آپلود تکمیل شد.");
                }
            }

            if (textProgress != null) textProgress.Report("در حال پاک‌سازی بکاپ‌های قدیمی...");
            // Cleanup old backups on Drive (keep last 5)
            await CleanupDriveBackupsAsync(service, folderId);
        }

        private static async Task<string> GetOrCreateFolderAsync(DriveService service, string folderName)
        {
            var request = service.Files.List();
            request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false";
            request.Spaces = "drive";
            request.Fields = "files(id, name)";
            
            var result = await request.ExecuteAsync();
            if (result.Files != null && result.Files.Count > 0)
            {
                return result.Files[0].Id;
            }

            // Create folder
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var createRequest = service.Files.Create(fileMetadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync();
            return folder.Id;
        }

        private static async Task CleanupDriveBackupsAsync(DriveService service, string folderId)
        {
            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false";
            request.Spaces = "drive";
            request.Fields = "files(id, name, createdTime)";
            request.OrderBy = "createdTime desc";

            var result = await request.ExecuteAsync();
            if (result.Files != null && result.Files.Count > 5)
            {
                var filesToDelete = result.Files.Skip(5);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        await service.Files.Delete(file.Id).ExecuteAsync();
                    }
                    catch { /* Ignore errors during cleanup */ }
                }
            }
        }

        public static async Task<List<CloudBackupModel>> GetDriveBackupsAsync()
        {
            if (!IsConnectedToGoogleDrive()) return new List<CloudBackupModel>();

            var service = await GetDriveServiceAsync();
            string folderName = "MovieManagerBackups";
            string folderId = await GetOrCreateFolderAsync(service, folderName);

            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and name contains 'Backup_' and trashed=false";
            request.Spaces = "drive";
            request.Fields = "files(id, name, createdTime, size, webViewLink)";
            request.OrderBy = "createdTime desc";

            var result = await request.ExecuteAsync();
            var list = new List<CloudBackupModel>();

            if (result.Files != null)
            {
                foreach (var file in result.Files)
                {
                    list.Add(new CloudBackupModel
                    {
                        Id = file.Id,
                        Name = file.Name,
                        CreatedTime = file.CreatedTimeDateTimeOffset?.LocalDateTime ?? DateTime.Now,
                        SizeInBytes = file.Size ?? 0,
                        WebViewLink = file.WebViewLink
                    });
                }
            }
            return list;
        }

        public static async Task DownloadDriveBackupAsync(string fileId, string destinationPath, IProgress<double> progress = null, IProgress<string> textProgress = null, long expectedSize = 0)
        {
            var service = await GetDriveServiceAsync();
            var request = service.Files.Get(fileId);
            
            if (expectedSize <= 0)
            {
                // Fetch file size if not provided
                var metaRequest = service.Files.Get(fileId);
                metaRequest.Fields = "size";
                var meta = await metaRequest.ExecuteAsync();
                expectedSize = meta.Size ?? 0;
            }

            request.MediaDownloader.ChunkSize = Google.Apis.Download.MediaDownloader.MinimumChunkSize; // 256KB
            
            if (progress != null)
            {
                request.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress downloadProgress) =>
                {
                    if (downloadProgress.Status == Google.Apis.Download.DownloadStatus.Downloading)
                    {
                        double percentage = expectedSize > 0 ? (double)downloadProgress.BytesDownloaded / expectedSize * 100 : 0;
                        progress.Report(percentage);
                        
                        string recvSize = downloadProgress.BytesDownloaded > 1024 * 1024 
                            ? $"{(downloadProgress.BytesDownloaded / 1024f / 1024f):F1} MB" 
                            : $"{(downloadProgress.BytesDownloaded / 1024f):F0} KB";
                            
                        if (textProgress != null) textProgress.Report($"دریافت شده: {recvSize} ({percentage:F1}%)");
                    }
                };
            }

            if (textProgress != null) textProgress.Report("شروع دانلود فایل...");

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            var response = await request.DownloadAsync(fileStream);
            
            if (response.Status == Google.Apis.Download.DownloadStatus.Completed)
            {
                if (progress != null) progress.Report(100.0);
                if (textProgress != null) textProgress.Report("دانلود تکمیل شد.");
            }
        }

        public static async Task DeleteDriveBackupAsync(string fileId)
        {
            var service = await GetDriveServiceAsync();
            await service.Files.Delete(fileId).ExecuteAsync();
        }

        public static async Task<string> ShareDriveBackupAsync(string fileId)
        {
            var service = await GetDriveServiceAsync();
            
            // Create a permission for anyone to read
            var permission = new Google.Apis.Drive.v3.Data.Permission
            {
                Type = "anyone",
                Role = "reader"
            };

            await service.Permissions.Create(permission, fileId).ExecuteAsync();

            // Get the file's web view link
            var request = service.Files.Get(fileId);
            request.Fields = "webViewLink";
            var file = await request.ExecuteAsync();

            return file.WebViewLink;
        }
    }

    public class CloudBackupModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedTime { get; set; }
        public long SizeInBytes { get; set; }
        public string WebViewLink { get; set; }
        public string FormattedSize => SizeInBytes > 1024 * 1024 
            ? $"{(SizeInBytes / 1024f / 1024f):F1} MB" 
            : $"{(SizeInBytes / 1024f):F1} KB";
    }
}
