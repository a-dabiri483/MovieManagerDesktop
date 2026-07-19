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

        public static async Task<bool> RunBackupAsync()
        {
            var settings = SettingsManager.LoadSettings();
            if (!settings.IsLocalAutoBackupEnabled && !settings.IsGoogleDriveAutoBackupEnabled)
            {
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

        private static async Task RunGoogleDriveBackupAsync(string filePath)
        {
            if (!System.IO.File.Exists(CredentialsFile))
            {
                throw new FileNotFoundException($"برای پشتیبان‌گیری در گوگل درایو فایل {CredentialsFile} نیاز است. لطفاً آن را در پوشه اصلی برنامه قرار دهید.");
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

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Find or create "MovieManagerBackups" folder
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
                request = service.Files.Create(fileMetadata, stream, "application/json");
                request.Fields = "id";
                await request.UploadAsync();
            }

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
            request.Q = $"'{folderId}' in parents and name contains 'Backup_' and trashed=false";
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
    }
}
