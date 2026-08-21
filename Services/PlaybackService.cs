using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Services
{
    public static class PlaybackService
    {
        public static void PlayMedia(VideoFile file, List<VideoFile>? playlist = null, int initialIndex = 0)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.FilePath))
            {
                ToastService.Instance.ShowWarning("مسیر فایل ویدیویی نامعتبر است.");
                return;
            }

            if (!File.Exists(file.FilePath))
            {
                ToastService.Instance.ShowError("فایل ویدیو در این مسیر یافت نشد! از بخش «ترمیم هوشمند» برای اصلاح مسیر استفاده کنید.");
                return;
            }

            file.LastPlayedAt = DateTime.Now;
            Task.Run(() =>
            {
                try
                {
                    using var db = new AppDbContext();
                    var dbItem = db.VideoFiles.Find(file.Id);
                    if (dbItem != null)
                    {
                        dbItem.LastPlayedAt = file.LastPlayedAt;
                        db.SaveChanges();
                    }
                }
                catch { }
            });

            var settings = SettingsManager.LoadSettings();

            if (settings.UseInternalPlayer)
            {
                // Open In-App Player in Dedicated Window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var playerWindow = new Views.PlayerWindow(file, playlist, initialIndex);
                    playerWindow.Show();
                    playerWindow.Activate();
                });
            }
            else
            {
                // Play with External Player
                PlayWithExternalPlayer(file.FilePath, settings);
            }
        }

        public static void PlayWithExternalPlayer(string filePath, SettingsModel settings)
        {
            try
            {
                string? playerExe = null;

                switch (settings.ExternalPlayerType)
                {
                    case "PotPlayer":
                        playerExe = FindPotPlayerPath();
                        break;
                    case "VLC":
                        playerExe = FindVlcPath();
                        break;
                    case "Custom":
                        if (!string.IsNullOrWhiteSpace(settings.CustomExternalPlayerPath) && File.Exists(settings.CustomExternalPlayerPath))
                        {
                            playerExe = settings.CustomExternalPlayerPath;
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(playerExe) && File.Exists(playerExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = playerExe,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false
                    });
                }
                else
                {
                    // Fallback to Windows default file association
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error starting external video player", ex);
                ToastService.Instance.ShowError($"خطا در اجرای پلیر خارجی: {ex.Message}");
            }
        }

        public static string? FindPotPlayerPath()
        {
            string[] candidates = {
                @"C:\Program Files\DAUM\PotPlayer\PotPlayer64.exe",
                @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayer.exe",
                @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe",
                @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayerMini.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"PotPlayer\PotPlayer64.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static string? FindVlcPath()
        {
            string[] candidates = {
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
            };

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
