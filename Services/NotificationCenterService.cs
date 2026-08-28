using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Services
{
    public partial class NotificationCenterService : ObservableObject
    {
        private static NotificationCenterService? _instance;
        public static NotificationCenterService Instance => _instance ??= new NotificationCenterService();

        private readonly string _storageFilePath;
        private readonly DispatcherTimer _pollTimer;

        public ObservableCollection<AppNotificationItem> Notifications { get; } = new ObservableCollection<AppNotificationItem>();

        [ObservableProperty]
        private int _unreadCount = 0;

        [ObservableProperty]
        private bool _hasUnread = false;

        public event EventHandler? NewNotificationReceived;

        private NotificationCenterService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "MovieManager");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            _storageFilePath = Path.Combine(appFolder, "notifications_history.json");

            LoadHistory();
            UpdateUnreadCount();

            // Set up polling timer (every 10 minutes)
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(10)
            };
            _pollTimer.Tick += async (s, e) => await CheckServerNotificationsAsync(triggerWiggleOnNew: true);
            _pollTimer.Start();

            // Initial check on startup
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Wait for app initialization
                await CheckServerNotificationsAsync(triggerWiggleOnNew: true);
            });
        }

        public void TriggerWiggle(bool playSound = true)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (playSound)
                {
                        // Fallback to Asterisk sound
                        System.Media.SystemSounds.Asterisk.Play();
                }
                NewNotificationReceived?.Invoke(this, EventArgs.Empty);
            });
        }

        public async Task CheckServerNotificationsAsync(bool triggerWiggleOnNew = true)
        {
            try
            {
                var announcements = await SettingsManager.FetchPublicAnnouncementsAsync();
                if (announcements != null && announcements.Count > 0)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var settings = SettingsManager.LoadSettings();
                        bool hasNewUnread = false;

                        // Ensure they are ordered by CreatedAt so we insert them properly
                        foreach (var ann in announcements.OrderBy(a => a.CreatedAt))
                        {
                            if (string.IsNullOrWhiteSpace(ann.Id)) continue;

                            var existing = Notifications.FirstOrDefault(x => x.Id == ann.Id);
                            bool isDismissedInSettings = settings.DismissedAnnouncementIds.Contains(ann.Id);

                            if (existing != null)
                            {
                                // Update content if modified
                                existing.Title = ann.Title;
                                existing.Message = ann.Message;
                                existing.Type = ann.Type;
                                existing.ActionTitle = ann.ActionTitle;
                                existing.ActionUrl = ann.ActionUrl;
                                if (isDismissedInSettings && !existing.IsRead)
                                {
                                    existing.IsRead = true;
                                }
                            }
                            else
                            {
                                var newItem = new AppNotificationItem
                                {
                                    Id = ann.Id,
                                    Title = ann.Title,
                                    Message = ann.Message,
                                    Type = ann.Type,
                                    ActionTitle = ann.ActionTitle,
                                    ActionUrl = ann.ActionUrl,
                                    ReceivedAt = ann.CreatedAt,
                                    IsRead = isDismissedInSettings
                                };

                                Notifications.Insert(0, newItem);
                                if (!newItem.IsRead)
                                {
                                    hasNewUnread = true;
                                }
                            }
                        }

                        PruneReadNotifications();
                        SaveHistory();
                        UpdateUnreadCount();

                        if (hasNewUnread && triggerWiggleOnNew)
                        {
                            TriggerWiggle(playSound: true);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[NotificationCenter] Check failed: {ex.Message}");
            }
        }

        public void AddLocalNotification(string title, string message, string type = "info", string actionTitle = "", string actionUrl = "")
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var item = new AppNotificationItem
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    ActionTitle = actionTitle,
                    ActionUrl = actionUrl,
                    ReceivedAt = DateTime.Now,
                    IsRead = false
                };

                Notifications.Insert(0, item);
                PruneReadNotifications();
                SaveHistory();
                UpdateUnreadCount();
                TriggerWiggle(playSound: true);
            });
        }

        public void MarkAsRead(AppNotificationItem item)
        {
            if (item == null) return;
            item.IsRead = true;
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                SettingsManager.DismissAnnouncement(item.Id);
            }
            PruneReadNotifications();
            UpdateUnreadCount();
            SaveHistory();
        }

        public void MarkAllAsRead()
        {
            foreach (var item in Notifications)
            {
                item.IsRead = true;
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    SettingsManager.DismissAnnouncement(item.Id);
                }
            }
            PruneReadNotifications();
            UpdateUnreadCount();
            SaveHistory();
        }

        public void Remove(AppNotificationItem item)
        {
            if (item == null) return;
            Notifications.Remove(item);
            UpdateUnreadCount();
            SaveHistory();
        }

        public void ClearAll()
        {
            Notifications.Clear();
            UpdateUnreadCount();
            SaveHistory();
        }

        private void PruneReadNotifications()
        {
            // Keep all unread notifications, and at most the 4 most recent read notifications
            var readItems = Notifications.Where(x => x.IsRead).OrderByDescending(x => x.ReceivedAt).ToList();
            if (readItems.Count > 4)
            {
                var toRemove = readItems.Skip(4).ToList();
                foreach (var item in toRemove)
                {
                    Notifications.Remove(item);
                }
            }
        }

        private void UpdateUnreadCount()
        {
            UnreadCount = Notifications.Count(x => !x.IsRead);
            HasUnread = UnreadCount > 0;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_storageFilePath))
                {
                    string json = File.ReadAllText(_storageFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var items = JsonSerializer.Deserialize<List<AppNotificationItem>>(json);
                        if (items != null)
                        {
                            Notifications.Clear();
                            foreach (var item in items.OrderByDescending(x => x.ReceivedAt))
                            {
                                Notifications.Add(item);
                            }
                            PruneReadNotifications();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[NotificationCenter] Failed to load history: {ex.Message}");
            }
        }

        private void SaveHistory()
        {
            try
            {
                // Keep max 50 recent notifications
                var itemsToSave = Notifications.Take(50).ToList();
                string json = JsonSerializer.Serialize(itemsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[NotificationCenter] Failed to save history: {ex.Message}");
            }
        }
    }
}
