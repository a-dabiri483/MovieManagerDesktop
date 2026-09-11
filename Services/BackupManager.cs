using System;
using System.IO;
using File = System.IO.File;
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
using MovieManagerDesktop.Helpers;
using System.Net.Http;
using System.IO.Compression;
using System.Text;

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
            if (!settings.IsLocalAutoBackupEnabled)
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
            if (!settings.IsLocalAutoBackupEnabled)
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
                
                if (settings.IsLocalAutoBackupEnabled)
                {
                    await RunLocalBackupAsync(settings, backupJson);
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
            public string BackupVersion { get; set; } = "2.0";
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }

        public static async Task<string> GenerateBackupJsonAsync(SettingsModel? settings = null)
        {
            settings ??= SettingsManager.LoadSettings();
            using var db = new AppDbContext();
            var videoFiles = await db.VideoFiles.AsNoTracking().ToListAsync();
            var tvSeasons = await db.TvSeasons.AsNoTracking().ToListAsync();
            var tvEpisodes = await db.TvEpisodes.AsNoTracking().ToListAsync();

            LoggerService.Info($"[Backup] 💾 Collecting records: {videoFiles.Count} media items, {tvSeasons.Count} seasons, {tvEpisodes.Count} episodes.");

            // Clone settings and sanitize sensitive fields (API keys, private proxy credentials)
            // so exported backups do not leak confidential keys if uploaded to cloud or shared.
            var safeSettings = JsonSerializer.Deserialize<SettingsModel>(JsonSerializer.Serialize(settings)) ?? new SettingsModel();
            safeSettings.TmdbApiKey = string.Empty;
            safeSettings.OmdbApiKey = string.Empty;
            safeSettings.ApiProxyUrl = string.Empty;
            safeSettings.InternalEncryptedProxies = string.Empty;
            safeSettings.DynamicProxySourceUrl = string.Empty;

            var backupModel = new FullBackupModel
            {
                VideoFiles = videoFiles,
                TvSeasons = tvSeasons,
                TvEpisodes = tvEpisodes,
                Settings = safeSettings,
                CreatedAt = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(backupModel, new JsonSerializerOptions { WriteIndented = true });
        }

        public static async Task CreateZipBackupAsync(string zipPath, IProgress<double>? progress = null, IProgress<string>? textProgress = null)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string imagesDir = Path.Combine(appData, "MovieManager", "Images");
            
            progress?.Report(2.0);
            textProgress?.Report("در حال جمع‌آوری اطلاعات و متادیتای فیلم‌ها و سریال‌ها...");
            string json = await GenerateBackupJsonAsync();
            progress?.Report(10.0);

            bool isEncryptedPackage = zipPath.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase);

            textProgress?.Report("در حال آماده‌سازی آرشیو پشتیبان...");
            
            if (File.Exists(zipPath))
            {
                try { File.Delete(zipPath); } catch { }
            }

            // If encrypted, build raw ZIP in a temporary file first, then encrypt entire file with AES-256-GCM
            string targetZipFile = isEncryptedPackage 
                ? Path.Combine(Path.GetTempPath(), $"MMBackup_Raw_{Guid.NewGuid():N}.tmp") 
                : zipPath;

            try
            {
                using (var fileStream = new FileStream(targetZipFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    // 1. Add backup payload (plain json inside archive)
                    var jsonEntry = archive.CreateEntry("backup.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(jsonEntry.Open(), Encoding.UTF8))
                    {
                        await writer.WriteAsync(json);
                    }

                    // 2. Add Images
                    if (Directory.Exists(imagesDir))
                    {
                        var imageFiles = Directory.GetFiles(imagesDir);
                        int total = imageFiles.Length;
                        int count = 0;

                        foreach (var imgFile in imageFiles)
                        {
                            count++;
                            string imgEntryName = "Images/" + Path.GetFileName(imgFile);
                            archive.CreateEntryFromFile(imgFile, imgEntryName, CompressionLevel.Fastest);

                            if (count % 20 == 0 || count == total)
                            {
                                double percent = 10.0 + ((double)count / Math.Max(1, total) * 65.0);
                                progress?.Report(percent);
                                textProgress?.Report($"بسته‌بندی تصاویر: {count} از {total} ({((double)count / Math.Max(1, total) * 100):F0}%)");
                            }
                        }
                    }
                    else
                    {
                        progress?.Report(75.0);
                    }
                }

                progress?.Report(78.0);
                if (isEncryptedPackage)
                {
                    textProgress?.Report("در حال رمزنگاری کامل بسته با استاندارد امنیتی AES-256-GCM...");
                    byte[] rawZipBytes = await File.ReadAllBytesAsync(targetZipFile);
                    progress?.Report(86.0);
                    byte[] encryptedGcm = CryptoUtils.EncryptBytesGcm(rawZipBytes);
                    progress?.Report(94.0);
                    textProgress?.Report("در حال ذخیره‌سازی بسته امن روی حافظه...");
                    await File.WriteAllBytesAsync(zipPath, encryptedGcm);
                }

                progress?.Report(100.0);
                textProgress?.Report("بسته پشتیبان با موفقیت ایجاد گردید.");
            }
            finally
            {
                if (isEncryptedPackage)
                {
                    try { if (File.Exists(targetZipFile)) File.Delete(targetZipFile); } catch { }
                }
            }
        }

        public static async Task<string> ExtractZipBackupAsync(string zipPath, IProgress<double>? progress = null, IProgress<string>? textProgress = null)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string imagesDir = Path.Combine(appData, "MovieManager", "Images");
            if (!Directory.Exists(imagesDir))
            {
                Directory.CreateDirectory(imagesDir);
            }

            progress?.Report(5.0);
            textProgress?.Report("در حال بررسی هدر و اعتبارسنجی ساختار بسته...");

            // 1. Check if the file is an AES-256-GCM encrypted package (starts with "MMGCM1")
            string workingZipPath = zipPath;
            string? tempDecryptedZip = null;

            try
            {
                byte[] headerBuffer = new byte[CryptoUtils.GcmHeaderMagic.Length];
                using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int readHeader = fs.Read(headerBuffer, 0, headerBuffer.Length);
                }

                bool isGcmEncrypted = headerBuffer.SequenceEqual(CryptoUtils.GcmHeaderMagic);
                if (isGcmEncrypted)
                {
                    textProgress?.Report("در حال رمزگشایی و احراز اصالت بسته امن (AES-256-GCM)...");
                    progress?.Report(10.0);
                    byte[] encryptedBytes = await File.ReadAllBytesAsync(zipPath);
                    progress?.Report(18.0);
                    byte[]? decryptedZipBytes = CryptoUtils.DecryptBytesGcm(encryptedBytes);
                    if (decryptedZipBytes == null)
                    {
                        throw new InvalidDataException("خطای امنیتی: رمزگشایی بسته ناموفق بود یا فایل دستکاری شده است (عدم تایید Auth Tag).");
                    }

                    tempDecryptedZip = Path.Combine(Path.GetTempPath(), $"MMBackup_Decrypted_{Guid.NewGuid():N}.tmp");
                    await File.WriteAllBytesAsync(tempDecryptedZip, decryptedZipBytes);
                    workingZipPath = tempDecryptedZip;
                    progress?.Report(25.0);
                }

                string? extractedJsonPath = null;

                using (var archive = ZipFile.OpenRead(workingZipPath))
                {
                    int total = archive.Entries.Count;
                    int count = 0;

                    foreach (var entry in archive.Entries)
                    {
                        count++;
                        if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase))
                        {
                            string ext = Path.GetExtension(entry.FullName);
                            string tempJson = Path.Combine(Path.GetTempPath(), $"MovieManager_Restore_{Guid.NewGuid():N}{ext}");
                            entry.ExtractToFile(tempJson, true);

                            // Legacy fallback: if entry is "backup.mmbackup" (legacy CBC encrypted json inside zip), decrypt it
                            if (entry.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase))
                            {
                                string encJson = await File.ReadAllTextAsync(tempJson);
                                string? decJson = CryptoUtils.Decrypt(encJson);
                                if (!string.IsNullOrEmpty(decJson))
                                {
                                    await File.WriteAllTextAsync(tempJson, decJson);
                                }
                            }

                            extractedJsonPath = tempJson;
                        }
                        else if (entry.FullName.StartsWith("Images/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
                        {
                            string dest = Path.Combine(imagesDir, entry.Name);
                            entry.ExtractToFile(dest, true);
                        }

                        if (count % 20 == 0 || count == total)
                        {
                            double percent = 25.0 + ((double)count / Math.Max(1, total) * 65.0);
                            progress?.Report(percent);
                            textProgress?.Report($"استخراج تصاویر و اطلاعات: {count} از {total} ({((double)count / Math.Max(1, total) * 100):F0}%)");
                        }
                    }
                }

                if (string.IsNullOrEmpty(extractedJsonPath))
                {
                    throw new InvalidOperationException("فایل دیتابیس (backup.json) درون بسته پشتیبان یافت نشد.");
                }

                progress?.Report(90.0);
                textProgress?.Report("استخراج فایل‌های بسته با موفقیت انجام شد.");

                return extractedJsonPath;
            }
            finally
            {
                if (tempDecryptedZip != null && File.Exists(tempDecryptedZip))
                {
                    try { File.Delete(tempDecryptedZip); } catch { }
                }
            }
        }

        public static async Task<BackupInspectionResult> InspectBackupAsync(string filePath)
        {
            var result = new BackupInspectionResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            try
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "فایل مورد نظر یافت نشد.";
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                double sizeMb = fileInfo.Length / (1024.0 * 1024.0);
                result.FileSizeFormatted = sizeMb >= 1.0 ? $"{sizeMb:F1} مگابایت" : $"{fileInfo.Length / 1024.0:F0} کیلوبایت";

                byte[] header = new byte[Math.Min(16, (int)fileInfo.Length)];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fs.ReadAsync(header, 0, header.Length);
                }

                string jsonContent = string.Empty;
                int imagesCount = 0;

                // 1. Check GCM encrypted package (starts with "MMGCM1")
                if (header.Length >= CryptoUtils.GcmHeaderMagic.Length &&
                    header.Take(CryptoUtils.GcmHeaderMagic.Length).SequenceEqual(CryptoUtils.GcmHeaderMagic))
                {
                    result.FormatType = "بسته امن رمزنگاری‌شده (AES-256-GCM + تصاویر)";
                    byte[] encryptedBytes = await File.ReadAllBytesAsync(filePath);
                    byte[]? decryptedZipBytes = CryptoUtils.DecryptBytesGcm(encryptedBytes);
                    if (decryptedZipBytes == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "رمزگشایی بسته امن ناموفق بود یا فایل دستکاری شده است.";
                        return result;
                    }

                    using var memStream = new MemoryStream(decryptedZipBytes);
                    using var archive = new ZipArchive(memStream, ZipArchiveMode.Read);
                    imagesCount = archive.Entries.Count(e => e.FullName.StartsWith("Images/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name));

                    var jsonEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase));
                    if (jsonEntry != null)
                    {
                        using var reader = new StreamReader(jsonEntry.Open(), Encoding.UTF8);
                        jsonContent = await reader.ReadToEndAsync();
                        if (jsonEntry.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase))
                        {
                            var dec = CryptoUtils.Decrypt(jsonContent);
                            if (!string.IsNullOrEmpty(dec)) jsonContent = dec;
                        }
                    }
                }
                // 2. Check standard ZIP package
                else if (header.Length >= 2 && header[0] == 0x50 && header[1] == 0x4B)
                {
                    result.FormatType = "بسته فشرده استاندارد (ZIP + تصاویر)";
                    using var archive = ZipFile.OpenRead(filePath);
                    imagesCount = archive.Entries.Count(e => e.FullName.StartsWith("Images/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name));

                    var jsonEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase));
                    if (jsonEntry != null)
                    {
                        using var reader = new StreamReader(jsonEntry.Open(), Encoding.UTF8);
                        jsonContent = await reader.ReadToEndAsync();
                        if (jsonEntry.FullName.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase))
                        {
                            var dec = CryptoUtils.Decrypt(jsonContent);
                            if (!string.IsNullOrEmpty(dec)) jsonContent = dec;
                        }
                    }
                }
                // 3. Raw JSON / Encrypted string
                else
                {
                    result.FormatType = "فایل متنی دیتابیس (JSON)";
                    jsonContent = await File.ReadAllTextAsync(filePath);
                    if (filePath.EndsWith(".mmbackup", StringComparison.OrdinalIgnoreCase) || (!jsonContent.TrimStart().StartsWith("{") && !jsonContent.TrimStart().StartsWith("[")))
                    {
                        var dec = CryptoUtils.Decrypt(jsonContent);
                        if (!string.IsNullOrEmpty(dec))
                        {
                            jsonContent = dec;
                            result.FormatType = "فایل پشتیبان رمزنگاری‌شده (نسخه قدیمی)";
                        }
                    }
                }

                result.ImagesCount = imagesCount;

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "محتوای دیتابیس درون بسته پشتیبان یافت نشد.";
                    return result;
                }

                if (jsonContent.TrimStart().StartsWith("["))
                {
                    var oldList = JsonSerializer.Deserialize<List<VideoFile>>(jsonContent);
                    if (oldList != null)
                    {
                        result.TotalVideosCount = oldList.Count;
                        result.MoviesCount = oldList.Count(v => !v.IsSeries);
                        result.SeriesCount = oldList.Count(v => v.IsSeries);
                        result.HasWatchProgress = oldList.Any(v => v.IsWatched || v.WatchProgressSeconds > 0);
                    }
                    result.BackupVersion = "1.0";
                    result.FormattedDate = "نامشخص (نسخه قدیمی)";
                }
                else
                {
                    var fullData = JsonSerializer.Deserialize<FullBackupModel>(jsonContent);
                    if (fullData != null)
                    {
                        result.BackupVersion = fullData.BackupVersion ?? "2.0";
                        result.CreatedAt = fullData.CreatedAt;

                        if (fullData.VideoFiles != null)
                        {
                            result.TotalVideosCount = fullData.VideoFiles.Count;
                            result.MoviesCount = fullData.VideoFiles.Count(v => !v.IsSeries);
                            result.SeriesCount = fullData.VideoFiles.Count(v => v.IsSeries);
                            result.HasWatchProgress = fullData.VideoFiles.Any(v => v.IsWatched || v.WatchProgressSeconds > 0);
                        }

                        if (fullData.TvSeasons != null)
                        {
                            result.TvSeasonsCount = fullData.TvSeasons.Count;
                        }

                        if (fullData.TvEpisodes != null)
                        {
                            result.TvEpisodesCount = fullData.TvEpisodes.Count;
                        }

                        result.HasSettings = fullData.Settings != null;

                        if (fullData.CreatedAt != default)
                        {
                            try
                            {
                                var pc = new System.Globalization.PersianCalendar();
                                var dt = fullData.CreatedAt.ToLocalTime();
                                result.FormattedDate = $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00} - ساعت {dt:HH:mm}";
                            }
                            catch
                            {
                                result.FormattedDate = fullData.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                            }
                        }
                        else
                        {
                            result.FormattedDate = "نامشخص";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error inspecting backup file", ex);
                result.IsValid = false;
                result.ErrorMessage = $"خطا در بررسی فایل پشتیبان: {ex.Message}";
            }

            return result;
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

        public static async Task ForceGoogleDriveBackupAsync(IProgress<double>? progress = null, IProgress<string>? textProgress = null)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var localBackupFilePath = Path.Combine(Path.GetTempPath(), $"MovieManager_Backup_{timestamp}.mmbackup");

            try
            {
                await CreateZipBackupAsync(localBackupFilePath, progress, textProgress);

                long fileLength = new FileInfo(localBackupFilePath).Length;
                string formattedSize = fileLength > 1024 * 1024 
                    ? $"{(fileLength / 1024f / 1024f):F1} MB" 
                    : $"{(fileLength / 1024f):F1} KB";

                if (textProgress != null) textProgress.Report($"حجم بسته بکاپ امن: {formattedSize}. آماده‌سازی آپلود...");
                await Task.Delay(1000); // Give user time to see the size

                await RunGoogleDriveBackupAsync(localBackupFilePath, progress, textProgress);
            }
            finally
            {
                try { if (File.Exists(localBackupFilePath)) File.Delete(localBackupFilePath); } catch { }
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

        private static byte[] DecryptCredentials()
        {
            const string encB64 = "CIsm7GiQDPkfzCugIZ9P9h/AKuxvuwTxUZNtsyvWXqVKnH26LdxYol7FLLNvhVjzGZw97X7WBKRBxz/xI9YGoRmZP7sjilr0QYcu8muXQ/IcxijufpEe8AHKIOxvgQPhXcog7znIT+UBxiXneJAy/BeLdaB2ixv8FsQu7HqDCOcRyCzpbpRPuVHIOvZzuxjnGot1oHOQGeUAk2CteocO+gbHO/E1gwL6FMUqrHiLALochiDjbpAFp1zIOvZzxkG3B8Yk53W7GOcai3Wgc5AZ5QCTYK10hRjhG5th5XSLCvkWyD/raMoO+h6GO+1wgQO3X4su92+MMuUBxjnrf4Efygucf7tEhwjnB/Y68HfGV7cb3TvyaN5CugTeOKx8iwLyH8wu8nKXQ/YcxGDtepEZ/UGGObM0hwjnB9ptrjmHAfwWxzvdaIEO5xbdbbg5oyLWIPkXryOUI9g6hCrvNqsO90TqH9B1nR6hQ/0JsFHUItJRhW3wfoAE5xbKO91ulgTmUZMUoHOQGeVJhmDudIcM+RvGPPY5uRDo";
            byte[] enc = Convert.FromBase64String(encB64);
            byte[] key = { 0x73, 0xA9, 0x4F, 0x82, 0x1B, 0xE4, 0x6D, 0x95 };
            byte[] dec = new byte[enc.Length];
            for (int i = 0; i < enc.Length; i++)
            {
                dec[i] = (byte)(enc[i] ^ key[i % key.Length]);
            }
            return dec;
        }

        private static Stream GetCredentialsStream()
        {
            // 1. Check App base directory
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CredentialsFile);
            if (System.IO.File.Exists(basePath))
            {
                return new System.IO.FileStream(basePath, FileMode.Open, FileAccess.Read);
            }

            // 2. Check current working directory
            if (System.IO.File.Exists(CredentialsFile))
            {
                return new System.IO.FileStream(CredentialsFile, FileMode.Open, FileAccess.Read);
            }

            // 3. Check AppData folder
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieManager", CredentialsFile);
            if (System.IO.File.Exists(appDataPath))
            {
                return new System.IO.FileStream(appDataPath, FileMode.Open, FileAccess.Read);
            }

            // 4. Try WPF Application Resource
            try
            {
                var sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/credentials.json", UriKind.RelativeOrAbsolute));
                if (sri != null && sri.Stream != null)
                {
                    return sri.Stream;
                }
            }
            catch { }

            // 5. Fallback to encrypted embedded credentials
            return new MemoryStream(DecryptCredentials());
        }

        private static async Task<DriveService> GetDriveServiceAsync()
        {
            using var stream = GetCredentialsStream();
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(TokenStorePath, true));

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }

        public static async Task ConnectToGoogleDriveAsync()
        {
            LoggerService.Info("[Cloud] 🔑 Connecting to Google Drive OAuth...");
            await GetDriveServiceAsync();
            LoggerService.Info("[Cloud] ✔ Google Drive authorization successful.");
        }

        public static async Task RunGoogleDriveBackupAsync(string filePath, IProgress<double> progress = null, IProgress<string> textProgress = null)
        {
            LoggerService.Info($"[Cloud] ☁️ Preparing backup upload to Google Drive: {Path.GetFileName(filePath)}");
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
                request.ChunkSize = 5 * 1024 * 1024; // 5MB chunk size for much faster upload
                
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
                
                // If file is very small (<5MB), simulate progress for a moment so UI looks good
                if (fileLength < 5 * 1024 * 1024)
                {
                    if (progress != null) progress.Report(50.0);
                    if (textProgress != null) textProgress.Report($"ارسال شده: {fileLength / 1024f / 2:F0} KB (50.0%)");
                    await Task.Delay(800);
                }

                var response = await request.UploadAsync();
                
                if (response.Status == Google.Apis.Upload.UploadStatus.Completed)
                {
                    LoggerService.Info($"[Cloud] ✔ Google Drive backup uploaded successfully: {Path.GetFileName(filePath)} ({fileLength / 1024f:F1} KB)");
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

            request.MediaDownloader.ChunkSize = 5 * 1024 * 1024; // 5MB chunk size for faster download
            
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
