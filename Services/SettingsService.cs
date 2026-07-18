using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using FolderIconManager.WPF.Models;

namespace FolderIconManager.WPF.Services
{
    public sealed class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;

        private readonly string _settingsFilePath;
        private SettingsData _currentSettings = new();

        private SettingsService()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FolderIconManager",
                "settings.json"
            );
            
            LoadSettings();
        }

        public SettingsData CurrentSettings => _currentSettings;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<SettingsData>(json);
                    
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        // تنظیم مقادیر پیش‌فرض اگر خالی باشند
                        _currentSettings.IconDownloadPath ??= Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                            "FolderIcons"
                        );
                        _currentSettings.TmdbApiKeys ??= new List<string>();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // در صورت خطا از تنظیمات پیش‌فرض استفاده می‌کنیم
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            // تنظیمات پیش‌فرض
            _currentSettings = new SettingsData
            {
                IconDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "FolderIcons"
                ),
                AutoApplyIcons = true,
                BackupOriginal = true,
                ShowNotifications = true
            };

            // ذخیره تنظیمات پیش‌فرض
            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                // اطمینان از وجود پوشه
                string? directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw new InvalidOperationException($"خطا در ذخیره تنظیمات: {ex.Message}", ex);
            }
        }

        /// <summary>به‌روزرسانی تمام تنظیمات و ذخیره یک‌باره</summary>
        public void UpdateAll(Models.SettingsData newSettings)
        {
            _currentSettings.IconDownloadPath = newSettings.IconDownloadPath;
            _currentSettings.AutoApplyIcons = newSettings.AutoApplyIcons;
            _currentSettings.BackupOriginal = newSettings.BackupOriginal;
            _currentSettings.ShowNotifications = newSettings.ShowNotifications;
            _currentSettings.TmdbApiKeys = new List<string>(newSettings.TmdbApiKeys);
            SaveSettings();
        }

        public void UpdateIconDownloadPath(string newPath)
        {
            _currentSettings.IconDownloadPath = newPath;
            SaveSettings();
        }

        public void UpdateAutoApplyIcons(bool autoApply)
        {
            _currentSettings.AutoApplyIcons = autoApply;
            SaveSettings();
        }

        public void UpdateBackupOriginal(bool backup)
        {
            _currentSettings.BackupOriginal = backup;
            SaveSettings();
        }

        public void UpdateShowNotifications(bool showNotifications)
        {
            _currentSettings.ShowNotifications = showNotifications;
            SaveSettings();
        }

        public void EnsureIconsDirectory()
        {
            try
            {
                if (!Directory.Exists(_currentSettings.IconDownloadPath))
                {
                    Directory.CreateDirectory(_currentSettings.IconDownloadPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating icons directory: {ex.Message}");
                throw new InvalidOperationException($"خطا در ایجاد پوشه ایکون‌ها: {ex.Message}", ex);
            }
        }
    }
}
