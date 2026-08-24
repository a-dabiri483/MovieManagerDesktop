# 🎬 کدهای کامل ماژول ویدیو پلیر (MovieManager Desktop)

این مستند شامل کدهای کامل و به‌روزرسانی‌شده تمام فایل‌های بخش پخش‌کننده ویدیو، استودیوی شخصی‌سازی زیرنویس، نمایش زنده پیشرفت ترجمه آنلاین زیرنویس با هوش مصنوعی و رفع قطعی فعال‌سازی Always On Top است:

---

## 📂 فهرست فایل‌ها:
1. [PlayerViewModel.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/ViewModels/PlayerViewModel.cs)
2. [PlayerWindow.xaml](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Views/PlayerWindow.xaml)
3. [PlayerWindow.xaml.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Views/PlayerWindow.xaml.cs)
4. [PlayerOverlayWindow.xaml](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Views/PlayerOverlayWindow.xaml)
5. [PlayerOverlayWindow.xaml.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Views/PlayerOverlayWindow.xaml.cs)
6. [EmbeddedSubtitleExtractorService.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Services/EmbeddedSubtitleExtractorService.cs)
7. [SubtitleTranslatorService.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Services/SubtitleTranslatorService.cs)
8. [PlaybackService.cs](file:///c:/Users/ALI/CascadeProjects/MovieManagerDesktop/Services/PlaybackService.cs)

---

# بخش ۱: `PlayerViewModel.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\ViewModels\PlayerViewModel.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public class TrackItemModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsEmbedded { get; set; }
        public int SubtitleIndex { get; set; } = -1;
        public string? FilePath { get; set; }
    }

    public class BookmarkModel
    {
        public long TimeMs { get; set; }
        public string TimeFormatted { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private readonly DispatcherTimer _uiTimer;
        private readonly DispatcherTimer _mouseIdleTimer;
        private readonly DispatcherTimer _osdTimer;

        [ObservableProperty]
        private VideoFile _currentMedia;

        [ObservableProperty]
        private string _mediaTitle = string.Empty;

        [ObservableProperty]
        private string _seasonEpisodeText = string.Empty;

        [ObservableProperty]
        private bool _isPlaying = false;

        [ObservableProperty]
        private long _currentTimeMs = 0;

        [ObservableProperty]
        private long _totalDurationMs = 0;

        [ObservableProperty]
        private string _currentTimeFormatted = "00:00:00";

        [ObservableProperty]
        private string _totalDurationFormatted = "00:00:00";

        [ObservableProperty]
        private double _progress = 0.0; // 0.0 to 1.0

        [ObservableProperty]
        private int _volume = 100; // 0 to 200

        [ObservableProperty]
        private bool _isMuted = false;

        [ObservableProperty]
        private float _playbackSpeed = 1.0f;

        [ObservableProperty]
        private bool _isFullscreen = false;

        [ObservableProperty]
        private bool _showControls = true;

        [ObservableProperty]
        private bool _showTopBar = true;

        [ObservableProperty]
        private bool _showBottomBar = true;

        [ObservableProperty]
        private string _osdMessage = string.Empty;

        [ObservableProperty]
        private bool _showOsd = false;

        [ObservableProperty]
        private bool _showPlaylistDrawer = false;

        [ObservableProperty]
        private bool _showBookmarksDrawer = false;

        [ObservableProperty]
        private bool _showShortcutsHelp = false;

        [ObservableProperty]
        private bool _showAudioTracksPopup = false;

        [ObservableProperty]
        private bool _showSubtitlesPopup = false;

        [ObservableProperty]
        private bool _showSpeedPopup = false;

        [ObservableProperty]
        private bool _showAspectRatiosPopup = false;

        // Video adjustments
        [ObservableProperty]
        private float _brightness = 1.0f; // 0.0 to 2.0 (1.0 default)

        [ObservableProperty]
        private float _contrast = 1.0f;

        [ObservableProperty]
        private float _saturation = 1.0f;

        [ObservableProperty]
        private float _hue = 0.0f;

        [ObservableProperty]
        private long _subtitleDelayMs = 0;

        [ObservableProperty]
        private long _audioDelayMs = 0;

        // A-B Repeat
        [ObservableProperty]
        private long? _repeatPointA = null;

        [ObservableProperty]
        private long? _repeatPointB = null;

        [ObservableProperty]
        private bool _isRepeatAbActive = false;

        // Extra Effects & States
        [ObservableProperty]
        private bool _isMirrorHorizontal = false;

        [ObservableProperty]
        private bool _isFlipVertical = false;

        [ObservableProperty]
        private bool _isAudioNormalizerActive = false;

        [ObservableProperty]
        private bool _isAudioDenoiseActive = false;

        [ObservableProperty]
        private bool _isRecordingAudio = false;

        [ObservableProperty]
        private bool _isContinuousCapture = false;

        [ObservableProperty]
        private bool _isAlwaysOnTop = false;

        // ════════════════════════════════════════════════════════════════
        // ── SUBTITLE STUDIO & REAL-TIME RENDERING ENGINE ──
        // ════════════════════════════════════════════════════════════════
        [ObservableProperty]
        private string _currentSubtitleText = string.Empty;

        [ObservableProperty]
        private bool _hasSubtitleText = false;

        [ObservableProperty]
        private double _subtitleDelaySeconds = 0.0;

        [ObservableProperty]
        private int _audioDelayMilliseconds = 0;

        [ObservableProperty]
        private int _subtitleFontSize = 28;

        [ObservableProperty]
        private string _subtitleColorHex = "#FFFFFF";

        [ObservableProperty]
        private string _subtitleFontFamily = "Vazirmatn";

        [ObservableProperty]
        private bool _isSubtitleBold = true;

        [ObservableProperty]
        private bool _hasSubtitleBackground = false;

        [ObservableProperty]
        private string _subtitleBackgroundColorHex = "#000000";

        [ObservableProperty]
        private int _subtitleBgOpacityPercent = 75;

        [ObservableProperty]
        private int _subtitleBottomMargin = 40;

        [ObservableProperty]
        private string _subtitleAlignment = "Center";

        [ObservableProperty]
        private bool _hasSubtitleShadow = true;

        [ObservableProperty]
        private bool _showSubtitleStudioModal = false;

        [ObservableProperty]
        private bool _isTranslatingSubtitle = false;

        [ObservableProperty]
        private double _translationProgress = 0.0;

        [ObservableProperty]
        private string _translationStatusText = string.Empty;

        private System.Threading.CancellationTokenSource? _translationCts;

        public Brush SubtitleColorBrush
        {
            get
            {
                try
                {
                    return (Brush)new BrushConverter().ConvertFromString(SubtitleColorHex) ?? Brushes.White;
                }
                catch
                {
                    return Brushes.White;
                }
            }
        }

        public Brush SubtitleBackgroundBrush
        {
            get
            {
                if (!HasSubtitleBackground) return Brushes.Transparent;
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(SubtitleBackgroundColorHex);
                    byte alpha = (byte)Math.Clamp(SubtitleBgOpacityPercent * 255 / 100, 0, 255);
                    return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                }
                catch
                {
                    return new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));
                }
            }
        }

        public FontWeight SubtitleFontWeight => IsSubtitleBold ? FontWeights.Bold : FontWeights.Normal;

        public TextAlignment SubtitleTextAlignment => SubtitleAlignment switch
        {
            "Right" => TextAlignment.Right,
            "Left" => TextAlignment.Left,
            _ => TextAlignment.Center
        };

        [ObservableProperty]
        private double _playerWindowWidth = 1280.0;

        [ObservableProperty]
        private double _playerWindowHeight = 720.0;

        public double SubtitleScaleRatio => Math.Clamp(PlayerWindowHeight / 720.0, 0.5, 2.5);

        public double RenderedSubtitleFontSize => Math.Round(Math.Clamp(SubtitleFontSize * SubtitleScaleRatio, 12.0, 76.0), 1);

        public Thickness RenderedSubtitleMargin => new Thickness(
            16 * SubtitleScaleRatio, 
            0, 
            16 * SubtitleScaleRatio, 
            Math.Clamp(SubtitleBottomMargin * SubtitleScaleRatio, 10.0, 240.0)
        );

        public Thickness SubtitleContainerMargin => RenderedSubtitleMargin;

        public string SubtitleBoldStatusText => IsSubtitleBold ? "بولد (Bold)" : "عادی (Normal)";

        public string SubtitleBackgroundStatusText => HasSubtitleBackground ? "روشن (فعال)" : "خاموش (غیرفعال)";

        public void UpdateWindowDimensions(double width, double height)
        {
            if (height > 100 && width > 100 && (Math.Abs(_playerWindowHeight - height) > 2 || Math.Abs(_playerWindowWidth - width) > 2))
            {
                PlayerWindowWidth = width;
                PlayerWindowHeight = height;
                OnPropertyChanged(nameof(SubtitleScaleRatio));
                OnPropertyChanged(nameof(RenderedSubtitleFontSize));
                OnPropertyChanged(nameof(RenderedSubtitleMargin));
                OnPropertyChanged(nameof(SubtitleContainerMargin));
            }
        }

        partial void OnSubtitleFontSizeChanged(int value)
        {
            OnPropertyChanged(nameof(RenderedSubtitleFontSize));
            var s = SettingsManager.LoadSettings();
            s.SubtitleFontSize = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleColorHexChanged(string value)
        {
            OnPropertyChanged(nameof(SubtitleColorBrush));
            var s = SettingsManager.LoadSettings();
            s.SubtitleColorHex = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnIsSubtitleBoldChanged(bool value)
        {
            OnPropertyChanged(nameof(SubtitleFontWeight));
            OnPropertyChanged(nameof(SubtitleBoldStatusText));
            var s = SettingsManager.LoadSettings();
            s.IsSubtitleBold = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnHasSubtitleBackgroundChanged(bool value)
        {
            OnPropertyChanged(nameof(SubtitleBackgroundBrush));
            OnPropertyChanged(nameof(SubtitleBackgroundStatusText));
            var s = SettingsManager.LoadSettings();
            s.HasSubtitleBackground = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleBackgroundColorHexChanged(string value)
        {
            OnPropertyChanged(nameof(SubtitleBackgroundBrush));
            var s = SettingsManager.LoadSettings();
            s.SubtitleBackgroundColorHex = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleBgOpacityPercentChanged(int value)
        {
            OnPropertyChanged(nameof(SubtitleBackgroundBrush));
            var s = SettingsManager.LoadSettings();
            s.SubtitleBgOpacityPercent = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleBottomMarginChanged(int value)
        {
            OnPropertyChanged(nameof(RenderedSubtitleMargin));
            OnPropertyChanged(nameof(SubtitleContainerMargin));
            var s = SettingsManager.LoadSettings();
            s.SubtitleBottomMargin = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleAlignmentChanged(string value)
        {
            OnPropertyChanged(nameof(SubtitleTextAlignment));
            var s = SettingsManager.LoadSettings();
            s.SubtitleAlignment = value;
            SettingsManager.SaveSettings(s);
        }

        partial void OnSubtitleFontFamilyChanged(string value)
        {
            var s = SettingsManager.LoadSettings();
            s.SubtitleFontFamily = value;
            SettingsManager.SaveSettings(s);
        }

        public event Action<double>? RequestWindowScale;
        public event Action? RequestToggleMaximize;
        public event Action? RequestCloseWindow;
        public event Action<bool>? RequestAlwaysOnTop;

        public ObservableCollection<VideoFile> Playlist { get; } = new();
        public ObservableCollection<TrackItemModel> AudioTracks { get; } = new();
        public ObservableCollection<TrackItemModel> SubtitleTracks { get; } = new();
        public ObservableCollection<BookmarkModel> Bookmarks { get; } = new();

        private int _currentPlaylistIndex = 0;
        private bool _isUserSeeking = false;
        private bool _hasMarkedWatched = false;
        private long _pendingResumeSeconds = 0;
        private DateTime _lastProgressSaveTime = DateTime.MinValue;
        private DateTime _seekDebounceUntil = DateTime.MinValue;
        private long _targetSeekMs = -1;
        private long _pendingSeekTargetMs = -1;
        private DateTime _lastSeekTime = DateTime.MinValue;
        private DateTime _lastSubEnforceTime = DateTime.MinValue;
        private string? _loadedSubtitlePath = null;
        private string? _lastActiveSubtitlePath = null;
        private List<SubtitleCue> _activeSubtitleCues = new();

        public MediaPlayer? MediaPlayer => _mediaPlayer;

        public PlayerViewModel(VideoFile media, List<VideoFile>? playlist = null, int initialIndex = 0, bool autoPlay = true)
        {
            _currentMedia = media;

            var settings = SettingsManager.LoadSettings();
            _volume = Math.Clamp(settings.PlayerVolume, 0, 200);
            _subtitleFontSize = settings.SubtitleFontSize > 0 ? settings.SubtitleFontSize : 28;
            _subtitleColorHex = !string.IsNullOrEmpty(settings.SubtitleColorHex) ? settings.SubtitleColorHex : "#FFFFFF";
            _subtitleFontFamily = !string.IsNullOrEmpty(settings.SubtitleFontFamily) ? settings.SubtitleFontFamily : "Vazirmatn";
            _isSubtitleBold = settings.IsSubtitleBold;
            _hasSubtitleBackground = settings.HasSubtitleBackground;
            _subtitleBackgroundColorHex = !string.IsNullOrEmpty(settings.SubtitleBackgroundColorHex) ? settings.SubtitleBackgroundColorHex : "#000000";
            _subtitleBgOpacityPercent = settings.SubtitleBgOpacityPercent > 0 ? settings.SubtitleBgOpacityPercent : 75;
            _subtitleBottomMargin = settings.SubtitleBottomMargin > 0 ? settings.SubtitleBottomMargin : 40;
            _subtitleAlignment = !string.IsNullOrEmpty(settings.SubtitleAlignment) ? settings.SubtitleAlignment : "Center";
            _hasSubtitleShadow = settings.HasSubtitleShadow;
            _isAlwaysOnTop = settings.PlayerAlwaysOnTop;

            if (playlist != null && playlist.Count > 0)
            {
                foreach (var item in playlist)
                {
                    Playlist.Add(item);
                }
                _currentPlaylistIndex = Math.Clamp(initialIndex, 0, Playlist.Count - 1);
            }
            else
            {
                Playlist.Add(media);
                _currentPlaylistIndex = 0;
                LoadSeriesPlaylistAsync(media);
            }

            InitLibVLC();

            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            _mouseIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            _mouseIdleTimer.Tick += (s, e) =>
            {
                if (IsPlaying && !HasOpenFlyout)
                {
                    ShowControls = false;
                    ShowTopBar = false;
                    ShowBottomBar = false;
                }
                _mouseIdleTimer.Stop();
            };

            _osdTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            _osdTimer.Tick += (s, e) =>
            {
                ShowOsd = false;
                _osdTimer.Stop();
            };

            if (autoPlay)
            {
                StartPlayback();
            }
        }

        public void StartPlayback()
        {
            if (Playlist.Count > 0 && _currentPlaylistIndex >= 0 && _currentPlaylistIndex < Playlist.Count)
            {
                LoadMedia(Playlist[_currentPlaylistIndex]);
            }
        }

        private async void LoadSeriesPlaylistAsync(VideoFile media)
        {
            try
            {
                using var db = new AppDbContext();
                List<VideoFile> episodes = new();

                bool isSeries = string.Equals(media.MediaType, "Series", StringComparison.OrdinalIgnoreCase) ||
                                media.Season != null || media.Episode != null;

                if (isSeries)
                {
                    if (media.TmdbId != null && media.TmdbId > 0)
                    {
                        episodes = await db.VideoFiles
                            .AsNoTracking()
                            .Where(v => v.TmdbId == media.TmdbId)
                            .OrderBy(v => v.Season ?? 1)
                            .ThenBy(v => v.Episode ?? 1)
                            .ThenBy(v => v.FileName)
                            .ToListAsync();
                    }
                    else if (!string.IsNullOrWhiteSpace(media.FormattedTitle))
                    {
                        episodes = await db.VideoFiles
                            .AsNoTracking()
                            .Where(v => v.FormattedTitle == media.FormattedTitle)
                            .OrderBy(v => v.Season ?? 1)
                            .ThenBy(v => v.Episode ?? 1)
                            .ThenBy(v => v.FileName)
                            .ToListAsync();
                    }

                    if (episodes.Count <= 1)
                    {
                        string? dir = Path.GetDirectoryName(media.FilePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            episodes = await db.VideoFiles
                                .AsNoTracking()
                                .Where(v => v.FilePath.StartsWith(dir))
                                .OrderBy(v => v.Season ?? 1)
                                .ThenBy(v => v.Episode ?? 1)
                                .ThenBy(v => v.FileName)
                                .ToListAsync();
                        }
                    }

                    if (episodes.Count > 1)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Playlist.Clear();
                            foreach (var ep in episodes)
                            {
                                Playlist.Add(ep);
                            }
                            var current = Playlist.FirstOrDefault(p => p.Id == media.Id || p.FilePath == media.FilePath);
                            if (current != null)
                            {
                                _currentPlaylistIndex = Playlist.IndexOf(current);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to auto-load series episodes into playlist", ex);
            }
        }

        private void InitLibVLC()
        {
            try
            {
                Core.Initialize();
                var settings = SettingsManager.LoadSettings();

                var vlcArgs = new List<string>
                {
                    "--avcodec-hw=any", 
                    "--directx-hw-yuv", 
                    "--no-sub-autodetect-file",
                    "--no-video-title-show",
                    "--input-fast-seek",
                    "--no-spu",
                    "--no-osd",
                    "--no-spdif",
                    "--aout=mmdevice",
                    "--audio-resampler=any",
                    "--demux=avformat,any"
                };

                _libVLC = new LibVLC(enableDebugLogs: false, vlcArgs.ToArray());

                _mediaPlayer = new MediaPlayer(_libVLC)
                {
                    EnableHardwareDecoding = true,
                    EnableMouseInput = false,
                    EnableKeyInput = false
                };

                _mediaPlayer.EndReached += MediaPlayer_EndReached;
                _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to initialize LibVLC engine", ex);
                ToastService.Instance.ShowError($"خطا در راه‌اندازی هسته ویدیو: {ex.Message}");
            }
        }

        public bool HasOpenFlyout => ShowPlaylistDrawer || ShowBookmarksDrawer || ShowShortcutsHelp || 
                                     ShowAudioTracksPopup || ShowSubtitlesPopup || ShowSpeedPopup || 
                                     ShowAspectRatiosPopup || ShowSubtitleStudioModal;

        public void HandleMouseMove()
        {
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();
        }

        public void HandleMouseMoveZone(double y, double totalHeight, double x, double totalWidth)
        {
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();

            if (HasOpenFlyout)
            {
                return;
            }

            if (y <= 80)
            {
                ShowTopBar = true;
                ShowBottomBar = false;
                ShowControls = true;
            }
            else if (y >= Math.Max(0, totalHeight - 120))
            {
                ShowBottomBar = true;
                ShowTopBar = false;
                ShowControls = true;
            }
            else
            {
                ShowTopBar = false;
                ShowBottomBar = false;
            }
        }

        public void HandleKeyDown(Key key, ModifierKeys modifiers)
        {
            // 1. Space: Play / Pause
            if (key == Key.Space)
            {
                TogglePlayPause();
                return;
            }

            // 2. Enter / F: Fullscreen
            if (key == Key.Enter || (key == Key.F && modifiers == ModifierKeys.None))
            {
                ToggleFullscreen();
                return;
            }

            // Escape: Exit Fullscreen / Close Drawer / Close Player
            if (key == Key.Escape)
            {
                if (IsFullscreen)
                {
                    IsFullscreen = false;
                    ShowOsdNotification("حالت پنجره‌ای");
                }
                else if (ShowPlaylistDrawer || ShowBookmarksDrawer || ShowShortcutsHelp || ShowAudioTracksPopup || ShowSubtitlesPopup)
                {
                    ShowPlaylistDrawer = false;
                    ShowBookmarksDrawer = false;
                    ShowShortcutsHelp = false;
                    ShowAudioTracksPopup = false;
                    ShowSubtitlesPopup = false;
                }
                else
                {
                    ClosePlayer();
                }
                return;
            }

            // 3. Arrow Left / Right: Seek
            if (key == Key.Left)
            {
                int seconds = modifiers.HasFlag(ModifierKeys.Control) ? 30 : (modifiers.HasFlag(ModifierKeys.Shift) ? 60 : 5);
                SeekRelative(-seconds);
                return;
            }
            if (key == Key.Right)
            {
                int seconds = modifiers.HasFlag(ModifierKeys.Control) ? 30 : (modifiers.HasFlag(ModifierKeys.Shift) ? 60 : 5);
                SeekRelative(seconds);
                return;
            }

            // 4. Arrow Up / Down: Volume
            if (key == Key.Up)
            {
                AdjustVolume(5);
                return;
            }
            if (key == Key.Down)
            {
                AdjustVolume(-5);
                return;
            }

            // 5. Page Up / Page Down: Previous / Next Episode
            if (key == Key.PageUp)
            {
                PlayPrevious();
                return;
            }
            if (key == Key.PageDown)
            {
                PlayNext();
                return;
            }

            // 6. X / C: Speed Down / Up, Z: Reset Speed
            if (key == Key.X && modifiers == ModifierKeys.None)
            {
                AdjustSpeed(-0.1f);
                return;
            }
            if (key == Key.C && modifiers == ModifierKeys.None)
            {
                AdjustSpeed(0.1f);
                return;
            }
            if (key == Key.Z && modifiers == ModifierKeys.None)
            {
                ResetSpeed();
                return;
            }

            // 7. Ctrl+Z / Ctrl+V: Mirror Image / Flip Image
            if (modifiers.HasFlag(ModifierKeys.Control) && key == Key.Z)
            {
                ToggleMirrorHorizontal();
                return;
            }
            if (modifiers.HasFlag(ModifierKeys.Control) && key == Key.V)
            {
                ToggleFlipVertical();
                return;
            }

            // 8. Shift+N / Shift+D: Audio Normalizer / De-noise
            if (modifiers.HasFlag(ModifierKeys.Shift) && key == Key.N)
            {
                ToggleAudioNormalizer();
                return;
            }
            if (modifiers.HasFlag(ModifierKeys.Shift) && key == Key.D)
            {
                ToggleAudioDenoise();
                return;
            }

            // 9. Ctrl+G / Shift+G: Continuous Frame Capture / Audio Record
            if (modifiers.HasFlag(ModifierKeys.Control) && key == Key.G)
            {
                ToggleContinuousCapture();
                return;
            }
            if (modifiers.HasFlag(ModifierKeys.Shift) && key == Key.G)
            {
                ToggleAudioRecord();
                return;
            }

            // 10. D / E: Frame Stepping (Previous / Next Frame)
            if (key == Key.D && modifiers == ModifierKeys.None)
            {
                StepFrame(false);
                return;
            }
            if (key == Key.E && modifiers == ModifierKeys.None)
            {
                StepFrame(true);
                return;
            }

            // 11. < / > (OemComma / OemPeriod): Subtitle Sync, / (OemQuestion): Reset Sub Sync
            if (modifiers.HasFlag(ModifierKeys.Shift) && (key == Key.OemComma || key == Key.OemPeriod))
            {
                string delta = (key == Key.OemPeriod) ? "50" : "-50";
                AdjustAudioDelay(delta);
                return;
            }
            if (key == Key.OemComma)
            {
                AdjustSubtitleDelay("-0.5");
                return;
            }
            if (key == Key.OemPeriod)
            {
                AdjustSubtitleDelay("0.5");
                return;
            }
            if (key == Key.OemQuestion || key == Key.Divide)
            {
                ResetSubtitleDelay();
                return;
            }

            // 12. [ / ] and \: A-B Repeat
            if (key == Key.OemOpenBrackets)
            {
                SetRepeatPointA();
                return;
            }
            if (key == Key.OemCloseBrackets)
            {
                SetRepeatPointB();
                return;
            }
            if (key == Key.OemBackslash || key == Key.OemPipe)
            {
                ToggleRepeatAb();
                return;
            }

            // 13. Picture Adjustments: W (Brightness-), R/T (Contrast), Y/U (Saturation), I/O (Hue), Q (Reset)
            if (key == Key.W && modifiers == ModifierKeys.None) { AdjustBrightness(-0.05f); return; }
            if (key == Key.R && modifiers == ModifierKeys.None) { AdjustContrast(-0.05f); return; }
            if (key == Key.T && modifiers == ModifierKeys.None) { AdjustContrast(0.05f); return; }
            if (key == Key.Y && modifiers == ModifierKeys.None) { AdjustSaturation(-0.05f); return; }
            if (key == Key.U && modifiers == ModifierKeys.None) { AdjustSaturation(0.05f); return; }
            if (key == Key.I && modifiers == ModifierKeys.None) { AdjustHue(-5f); return; }
            if (key == Key.O && modifiers == ModifierKeys.None) { AdjustHue(5f); return; }
            if (key == Key.Q && modifiers == ModifierKeys.None) { ResetPictureAdjustments(); return; }

            // 14. Presets 1 to 5 (Window Size) & 6, 7, 9 (Aspect Ratio)
            if (modifiers == ModifierKeys.None)
            {
                if (key == Key.D1 || key == Key.NumPad1) { RequestWindowScale?.Invoke(0.5); ShowOsdNotification("📐 اندازه پنجره: 0.5x (۵۰٪)"); return; }
                if (key == Key.D2 || key == Key.NumPad2) { RequestWindowScale?.Invoke(1.0); ShowOsdNotification("📐 اندازه پنجره: 1.0x (اندازه اصلی)"); return; }
                if (key == Key.D3 || key == Key.NumPad3) { RequestWindowScale?.Invoke(1.5); ShowOsdNotification("📐 اندازه پنجره: 1.5x (۱۵۰٪)"); return; }
                if (key == Key.D4 || key == Key.NumPad4) { RequestWindowScale?.Invoke(2.0); ShowOsdNotification("📐 اندازه پنجره: 2.0x (۲۰۰٪)"); return; }
                if (key == Key.D5 || key == Key.NumPad5) { RequestToggleMaximize?.Invoke(); ShowOsdNotification("🪟 حداکثر اندازه پنجره (Maximize)"); return; }
                if (key == Key.D6 || key == Key.NumPad6) { SetAspectRatio("16:9"); return; }
                if (key == Key.D7 || key == Key.NumPad7) { SetAspectRatio("4:3"); return; }
                if (key == Key.D9 || key == Key.NumPad9) { SetAspectRatio("Original"); return; }
            }

            // 15. Bookmarks: P (Add Bookmark), H (Toggle Bookmarks List)
            if (key == Key.P && modifiers == ModifierKeys.None)
            {
                AddBookmark();
                return;
            }
            if (key == Key.H && modifiers == ModifierKeys.None)
            {
                ShowBookmarksDrawer = !ShowBookmarksDrawer;
                return;
            }

            // 16. A (Audio Stream), L (Subtitle Stream), J (Aspect Ratio Cycle), K (Snapshot)
            if (key == Key.A && modifiers == ModifierKeys.None)
            {
                CycleAudioTrack();
                return;
            }
            if (key == Key.L && modifiers == ModifierKeys.None)
            {
                CycleSubtitleTrack();
                return;
            }
            if (key == Key.J && modifiers == ModifierKeys.None)
            {
                CycleAspectRatio();
                return;
            }
            if (key == Key.K && modifiers == ModifierKeys.None)
            {
                TakeSnapshot();
                return;
            }

            // 17. M: Mute
            if (key == Key.M && modifiers == ModifierKeys.None)
            {
                ToggleMute();
                return;
            }

            // 18. Function Keys: F1 (Help), F2 (Open Folder), F3 (Open File), F4 (Exit), F5 (Settings), F6 (Playlist), F7 (Equalizer), F12 (Navigation)
            if (key == Key.F1) { ShowShortcutsHelp = !ShowShortcutsHelp; return; }
            if (key == Key.F2) { OpenContainingFolder(); return; }
            if (key == Key.F3) { OpenFileDialog(); return; }
            if (key == Key.F4 && modifiers == ModifierKeys.None) { ClosePlayer(); return; }
            if (key == Key.F5) { ShowOsdNotification("⚙️ تنظیمات پلیر"); return; }
            if (key == Key.F6) { ShowPlaylistDrawer = !ShowPlaylistDrawer; return; }
            if (key == Key.F7) { ToggleAudioEqualizer(); return; }
            if (key == Key.F12) { QuickNavigatePlaylist(); return; }
        }

        public void LoadMedia(VideoFile media)
        {
            if (_libVLC == null || _mediaPlayer == null || string.IsNullOrWhiteSpace(media.FilePath)) return;

            CurrentMedia = media;
            MediaTitle = !string.IsNullOrEmpty(media.FormattedTitle) ? media.FormattedTitle : media.FileName;
            
            if (media.Season.HasValue && media.Episode.HasValue)
            {
                SeasonEpisodeText = $"فصل {media.Season.Value:D2} • قسمت {media.Episode.Value:D2}";
            }
            else if (media.Episode.HasValue)
            {
                SeasonEpisodeText = $"قسمت {media.Episode.Value:D2}";
            }
            else if (!string.IsNullOrEmpty(media.Year))
            {
                SeasonEpisodeText = media.Year;
            }
            else
            {
                SeasonEpisodeText = string.Empty;
            }

            _hasMarkedWatched = false;
            _repeatPointA = null;
            _repeatPointB = null;
            IsRepeatAbActive = false;

            if (media.WatchProgressSeconds > 5 && media.WatchProgressPercent < 92)
            {
                _pendingResumeSeconds = media.WatchProgressSeconds;
            }
            else
            {
                _pendingResumeSeconds = 0;
            }

            try
            {
                var vlcMedia = new Media(_libVLC, media.FilePath, FromType.FromPath);
                _mediaPlayer.Media = vlcMedia;
                _mediaPlayer.Play();
                IsPlaying = true;
                _mediaPlayer.Volume = Volume;

                // Load subtitles from video directory automatically
                LoadExternalSubtitlesFromFolder(media.FilePath);

                // Auto-extract and prepare embedded subtitles in background
                LoadEmbeddedSubtitlesAsync(media.FilePath);

                ShowOsdNotification($"▶ {MediaTitle}");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error playing video file", ex);
                ToastService.Instance.ShowError($"خطا در پخش فایل: {ex.Message}");
            }
        }

        public void LoadEmbeddedSubtitlesAsync(string videoPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    var embeddedTracks = await EmbeddedSubtitleExtractorService.GetEmbeddedSubtitleTracksAsync(videoPath);
                    if (embeddedTracks.Count > 0)
                    {
                        Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            UpdateSubtitleTracksList();
                        });

                        var preferredTrack = embeddedTracks.FirstOrDefault(t => 
                            t.Language.StartsWith("per", StringComparison.OrdinalIgnoreCase) || 
                            t.Language.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ||
                            t.Title.Contains("farsi", StringComparison.OrdinalIgnoreCase) ||
                            t.Title.Contains("persian", StringComparison.OrdinalIgnoreCase)) ?? embeddedTracks[0];

                        string? extractedPath = await EmbeddedSubtitleExtractorService.ExtractEmbeddedSubtitleToSrtAsync(videoPath, preferredTrack.SubtitleIndex);
                        if (!string.IsNullOrEmpty(extractedPath))
                        {
                            Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                LoadSubtitleFileInternal(extractedPath);
                                _mediaPlayer?.SetSpu(-1);
                                ShowOsdNotification($"💬 زیرنویس ({preferredTrack.DisplayName}) فعال شد");
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Error("Failed in LoadEmbeddedSubtitlesAsync", ex);
                }
            });
        }

        private void LoadExternalSubtitlesFromFolder(string videoPath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(videoPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                string baseName = Path.GetFileNameWithoutExtension(videoPath);
                var subFiles = Directory.GetFiles(dir, $"{baseName}*.srt")
                    .Concat(Directory.GetFiles(dir, $"{baseName}*.vtt"))
                    .Concat(Directory.GetFiles(dir, $"{baseName}*.ass"))
                    .ToList();

                _mediaPlayer?.SetSpu(-1);

                if (subFiles.Count > 0)
                {
                    var faSub = subFiles.FirstOrDefault(s => s.EndsWith(".fa.srt", StringComparison.OrdinalIgnoreCase) || s.EndsWith("_FA.srt", StringComparison.OrdinalIgnoreCase)) ?? subFiles[0];
                    LoadSubtitleFileInternal(faSub);
                    _mediaPlayer?.SetSpu(-1);
                }
            }
            catch { }
        }

        public void LoadSubtitleFileInternal(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
                _loadedSubtitlePath = filePath;
                _lastActiveSubtitlePath = filePath;
                _activeSubtitleCues = SubtitleTranslatorService.ParseSubtitleFile(filePath);

                foreach (var t in SubtitleTracks)
                {
                    t.IsSelected = (t.FilePath == filePath || (t.IsEmbedded && _loadedSubtitlePath != null && _loadedSubtitlePath.Contains($"_sub_{t.SubtitleIndex}.srt")));
                }
            }
            catch { }
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaPlayer == null || _isUserSeeking) return;

            try
            {
                if ((DateTime.UtcNow - _lastSubEnforceTime).TotalMilliseconds >= 500)
                {
                    _lastSubEnforceTime = DateTime.UtcNow;
                    EnforceDisableInternalSubtitles();
                }

                if (DateTime.UtcNow > _seekDebounceUntil)
                {
                    long vlcTime = _mediaPlayer.Time;
                    if (vlcTime >= 0)
                    {
                        if (_pendingSeekTargetMs >= 0)
                        {
                            // 🎯 فقط وقتی VLC به مقصدِ پرش رسید (یا بعد از ۳ ثانیه ناامیدی)، زمان را بروزرسانی کن
                            bool settled = Math.Abs(vlcTime - _pendingSeekTargetMs) <= 800;
                            bool timeout = (DateTime.UtcNow - _lastSeekTime).TotalMilliseconds > 3000;
                            if (settled || timeout)
                            {
                                _pendingSeekTargetMs = -1;
                                CurrentTimeMs = vlcTime;
                            }
                            // در غیر این صورت: CurrentTimeMs را با زمان قدیمی بازنویسی نکن!
                        }
                        else
                        {
                            CurrentTimeMs = vlcTime;
                        }
                    }
                }

                TotalDurationMs = _mediaPlayer.Length;

                if (TotalDurationMs > 0)
                {
                    // Instant Subtitle Cue Sync
                    if (_activeSubtitleCues.Count > 0)
                    {
                        long adjustedTime = CurrentTimeMs + (long)(SubtitleDelaySeconds * 1000.0);
                        var cue = _activeSubtitleCues
                            .Where(c => adjustedTime >= c.StartMs && adjustedTime <= c.EndMs)
                            .OrderByDescending(c => c.StartMs)
                            .FirstOrDefault();

                        if (cue != null)
                        {
                            CurrentSubtitleText = cue.Text;
                            HasSubtitleText = true;
                        }
                        else
                        {
                            CurrentSubtitleText = string.Empty;
                            HasSubtitleText = false;
                        }
                    }
                    else
                    {
                        HasSubtitleText = false;
                    }

                    // Check initial resume from last position
                    if (_pendingResumeSeconds > 0 && TotalDurationMs > 10000)
                    {
                        long resumeMs = _pendingResumeSeconds * 1000L;
                        if (resumeMs < TotalDurationMs)
                        {
                            _mediaPlayer.Time = resumeMs;
                            CurrentTimeMs = resumeMs;
                            ShowOsdNotification($"▶ ادامه پخش از {FormatTime(resumeMs)}");
                        }
                        _pendingResumeSeconds = 0;
                    }

                    Progress = (double)CurrentTimeMs / TotalDurationMs;
                    CurrentTimeFormatted = FormatTime(CurrentTimeMs);
                    TotalDurationFormatted = FormatTime(TotalDurationMs);

                    // A-B Repeat Loop Check
                    if (IsRepeatAbActive && _repeatPointA.HasValue && _repeatPointB.HasValue)
                    {
                        if (CurrentTimeMs >= _repeatPointB.Value)
                        {
                            _mediaPlayer.Time = _repeatPointA.Value;
                        }
                    }

                    // Auto Mark Watched at 90%
                    if (!_hasMarkedWatched && Progress >= 0.90)
                    {
                        _hasMarkedWatched = true;
                        MarkMediaAsWatched(CurrentMedia);
                    }
                    else if (CurrentTimeMs > 3000 && (DateTime.Now - _lastProgressSaveTime).TotalSeconds >= 5)
                    {
                        _lastProgressSaveTime = DateTime.Now;
                        SaveWatchProgressAsync(CurrentMedia, CurrentTimeMs / 1000L, Progress * 100.0);
                    }

                    // Ensure audio track and volume are active
                    if (_mediaPlayer.IsPlaying)
                    {
                        if (_mediaPlayer.Volume != Volume && !IsMuted)
                        {
                            _mediaPlayer.Volume = Volume;
                        }
                        if (_mediaPlayer.AudioTrack == -1 && _mediaPlayer.AudioTrackCount > 0)
                        {
                            var tracks = _mediaPlayer.AudioTrackDescription;
                            if (tracks != null && tracks.Length > 0)
                            {
                                foreach (var t in tracks)
                                {
                                    if (t.Id > 0)
                                    {
                                        _mediaPlayer.SetAudioTrack(t.Id);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private async void SaveWatchProgressAsync(VideoFile file, long seconds, double percent)
        {
            if (file == null || file.Id == 0) return;
            try
            {
                using var db = new AppDbContext();
                var dbItem = await db.VideoFiles.FindAsync(file.Id);
                if (dbItem != null)
                {
                    dbItem.WatchProgressSeconds = seconds;
                    dbItem.WatchProgressPercent = Math.Clamp(percent, 0.0, 100.0);
                    file.WatchProgressSeconds = seconds;
                    file.WatchProgressPercent = dbItem.WatchProgressPercent;
                    await db.SaveChangesAsync();
                }
            }
            catch { }
        }

        private async void MarkMediaAsWatched(VideoFile file)
        {
            try
            {
                using var db = new AppDbContext();
                var dbItem = await db.VideoFiles.FindAsync(file.Id);
                if (dbItem != null)
                {
                    dbItem.IsWatched = true;
                    dbItem.WatchProgressPercent = 100;
                    if (dbItem.TotalDurationSeconds > 0)
                    {
                        dbItem.WatchProgressSeconds = dbItem.TotalDurationSeconds;
                    }
                    await db.SaveChangesAsync();
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
            }
            catch { }
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                if (_currentPlaylistIndex < Playlist.Count - 1)
                {
                    PlayNext();
                }
                else
                {
                    ShowOsdNotification("پایان پخش ویدیو");
                }
            });
        }

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ToastService.Instance.ShowError("خطا در پردازش و پخش جریان ویدیو.");
            });
        }

        public void ShowOsdNotification(string message)
        {
            OsdMessage = message;
            ShowOsd = true;
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        [RelayCommand]
        public void ToggleAlwaysOnTop()
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
            RequestAlwaysOnTop?.Invoke(IsAlwaysOnTop);
            ShowOsdNotification(IsAlwaysOnTop ? "📌 حالت همیشه رو (Always On Top): فعال" : "📌 حالت عادی (همیشه رو غیرفعال)");
        }

        [RelayCommand]
        public void CloseAllPopups()
        {
            ShowPlaylistDrawer = false;
            ShowBookmarksDrawer = false;
            ShowShortcutsHelp = false;
            ShowAudioTracksPopup = false;
            ShowSubtitlesPopup = false;
            ShowSpeedPopup = false;
            ShowAspectRatiosPopup = false;
        }

        [RelayCommand]
        public void TogglePlayPause()
        {
            if (_mediaPlayer == null) return;

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                IsPlaying = false;
                ShowOsdNotification("⏸ توقف (Pause)");
            }
            else
            {
                _mediaPlayer.Play();
                IsPlaying = true;
                ShowOsdNotification("▶ پخش (Play)");
            }
        }

        [RelayCommand]
        public void ToggleFullscreen()
        {
            IsFullscreen = !IsFullscreen;
            ShowOsdNotification(IsFullscreen ? "🖥 تمام‌صفحه (Fullscreen)" : "🪟 حالت پنجره‌ای");
        }

        [RelayCommand]
        public void ToggleMute()
        {
            if (_mediaPlayer == null) return;
            IsMuted = !IsMuted;
            _mediaPlayer.Mute = IsMuted;
            ShowOsdNotification(IsMuted ? "🔇 بی‌صدا (Mute)" : $"🔊 صدا: {Volume}%");
        }

        public void AdjustVolume(int delta)
        {
            if (_mediaPlayer == null) return;
            Volume = Math.Clamp(Volume + delta, 0, 200);
            _mediaPlayer.Volume = Volume;
            IsMuted = false;
            ShowOsdNotification($"🔊 ولوم: {Volume}%");
        }

        public void EnforceDisableInternalSubtitles()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    if (_mediaPlayer.Spu != -1)
                    {
                        _mediaPlayer.SetSpu(-1);
                    }
                }
            }
            catch { }
        }

        public void SeekRelative(int seconds)
        {
            if (_mediaPlayer == null) return;
            long length = _mediaPlayer.Length > 0 ? _mediaPlayer.Length : TotalDurationMs;
            if (length <= 0) return;

            long baseTime;
            if (_pendingSeekTargetMs >= 0)
            {
                // 🎯 پرش قبلی هنوز در VLC تسویه نشده؛ از هدف قبلی زنجیره کن، نه از زمان قدیمی
                baseTime = _targetSeekMs;
            }
            else if ((DateTime.UtcNow - _lastSeekTime).TotalMilliseconds < 500 && _targetSeekMs >= 0)
            {
                baseTime = _targetSeekMs;
            }
            else
            {
                baseTime = CurrentTimeMs > 0 ? CurrentTimeMs : Math.Max(0, _mediaPlayer.Time);
            }

            _targetSeekMs = Math.Clamp(baseTime + (seconds * 1000L), 0, Math.Max(0, length - 1000L));
            _lastSeekTime = DateTime.UtcNow;
            _pendingSeekTargetMs = _targetSeekMs;

            // 🎯 فقط یک دستور Seek (حذف Position اضافه)
            _mediaPlayer.Time = _targetSeekMs;

            CurrentTimeMs = _targetSeekMs;
            _seekDebounceUntil = DateTime.UtcNow.AddMilliseconds(500);

            Progress = (double)_targetSeekMs / length;
            CurrentTimeFormatted = FormatTime(_targetSeekMs);
            string sign = seconds > 0 ? "+" : "";
            ShowOsdNotification($"⏱ پرش: {sign}{seconds}s ➔ {CurrentTimeFormatted}");
        }

        public void SeekTo(double newProgress)
        {
            if (_mediaPlayer == null) return;
            long length = _mediaPlayer.Length > 0 ? _mediaPlayer.Length : TotalDurationMs;
            if (length <= 0) return;

            newProgress = Math.Clamp(newProgress, 0.0, 1.0);
            long targetTime = (long)(newProgress * length);
            _targetSeekMs = targetTime;
            _pendingSeekTargetMs = targetTime;
            _lastSeekTime = DateTime.UtcNow;

            _mediaPlayer.Time = targetTime;
            CurrentTimeMs = targetTime;
            _seekDebounceUntil = DateTime.UtcNow.AddMilliseconds(350);

            Progress = newProgress;
            CurrentTimeFormatted = FormatTime(targetTime);
        }

        public void StartSeek() => _isUserSeeking = true;
        public void EndSeek() => _isUserSeeking = false;

        [RelayCommand]
        public void IncreaseSpeed()
        {
            AdjustSpeed(0.1f);
        }

        [RelayCommand]
        public void DecreaseSpeed()
        {
            AdjustSpeed(-0.1f);
        }

        public void AdjustSpeed(float delta)
        {
            if (_mediaPlayer == null) return;
            PlaybackSpeed = Math.Clamp((float)Math.Round(PlaybackSpeed + delta, 2), 0.25f, 4.0f);
            _mediaPlayer.SetRate(PlaybackSpeed);
            ShowOsdNotification($"⚡ سرعت پخش: {PlaybackSpeed:0.00}x");
        }

        [RelayCommand]
        public void ResetSpeed()
        {
            if (_mediaPlayer == null) return;
            PlaybackSpeed = 1.0f;
            _mediaPlayer.SetRate(1.0f);
            ShowOsdNotification("⚡ سرعت: ۱.۰x (پیش‌فرض)");
            ShowSpeedPopup = false;
        }

        [RelayCommand]
        public void SetSpeed(object? param)
        {
            if (param == null || _mediaPlayer == null) return;
            if (double.TryParse(param.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double speed))
            {
                PlaybackSpeed = (float)Math.Round(speed, 2);
                _mediaPlayer.SetRate(PlaybackSpeed);
                ShowOsdNotification($"⚡ سرعت پخش: {PlaybackSpeed:0.00}x");
                ShowSpeedPopup = false;
            }
        }

        public void StepFrame(bool forward = true)
        {
            if (_mediaPlayer == null) return;
            if (IsPlaying) _mediaPlayer.Pause();
            IsPlaying = false;
            _mediaPlayer.NextFrame();
            ShowOsdNotification("🎞 فریم بعدی");
        }

        public void SetRepeatPointA()
        {
            _repeatPointA = CurrentTimeMs;
            ShowOsdNotification($"🔁 نقطه شروع تکرار A: {FormatTime(CurrentTimeMs)}");
        }

        public void SetRepeatPointB()
        {
            if (!_repeatPointA.HasValue)
            {
                SetRepeatPointA();
                return;
            }
            _repeatPointB = Math.Max(CurrentTimeMs, _repeatPointA.Value + 1000L);
            IsRepeatAbActive = true;
            ShowOsdNotification($"🔁 نقطه پایان تکرار B: {FormatTime(_repeatPointB.Value)} (تکرار فعال شد)");
        }

        public void ToggleRepeatAb()
        {
            if (IsRepeatAbActive)
            {
                IsRepeatAbActive = false;
                _repeatPointA = null;
                _repeatPointB = null;
                ShowOsdNotification("🔁 تکرار A-B غیرفعال شد");
            }
            else
            {
                SetRepeatPointA();
            }
        }

        public void AdjustBrightness(float delta)
        {
            if (_mediaPlayer == null) return;
            Brightness = Math.Clamp(Brightness + delta, 0.0f, 2.0f);
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Brightness, Brightness);
            ShowOsdNotification($"☀️ روشنایی: {(int)(Brightness * 100)}%");
        }

        public void AdjustContrast(float delta)
        {
            if (_mediaPlayer == null) return;
            Contrast = Math.Clamp(Contrast + delta, 0.0f, 2.0f);
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Contrast, Contrast);
            ShowOsdNotification($"🌓 کنتراست: {(int)(Contrast * 100)}%");
        }

        public void AdjustSaturation(float delta)
        {
            if (_mediaPlayer == null) return;
            Saturation = Math.Clamp(Saturation + delta, 0.0f, 3.0f);
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Saturation, Saturation);
            ShowOsdNotification($"🎨 اشباع رنگ: {(int)(Saturation * 100)}%");
        }

        public void AdjustHue(float delta)
        {
            if (_mediaPlayer == null) return;
            Hue = Math.Clamp(Hue + delta, -180f, 180f);
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Hue, Hue);
            ShowOsdNotification($"🌈 طیف رنگ: {(int)Hue}°");
        }

        public void ResetPictureAdjustments()
        {
            if (_mediaPlayer == null) return;
            Brightness = 1.0f;
            Contrast = 1.0f;
            Saturation = 1.0f;
            Hue = 0.0f;
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 0);
            ShowOsdNotification("🔄 تنظیمات تصویر به حالت پیش‌فرض بازگشت");
        }

        public void SetAspectRatio(string ratio)
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.AspectRatio = (ratio == "Original" || ratio == "Fill") ? null : ratio;
            ShowOsdNotification($"📐 نسبت تصویر: {ratio}");
        }

        public void CycleAspectRatio()
        {
            string[] ratios = { "Original", "16:9", "4:3", "21:9", "1:1", "Fill" };
            int currentIdx = Array.IndexOf(ratios, _mediaPlayer?.AspectRatio ?? "Original");
            int nextIdx = (currentIdx + 1) % ratios.Length;
            SetAspectRatio(ratios[nextIdx]);
        }

        public void AddBookmark()
        {
            var bm = new BookmarkModel
            {
                TimeMs = CurrentTimeMs,
                TimeFormatted = FormatTime(CurrentTimeMs),
                Title = $"نشانک در {FormatTime(CurrentTimeMs)}"
            };
            Bookmarks.Add(bm);
            ShowOsdNotification($"🔖 نشانک اضافه شد: {bm.TimeFormatted}");
        }

        [RelayCommand]
        public void SeekToBookmark(BookmarkModel bm)
        {
            if (bm == null || _mediaPlayer == null) return;
            _mediaPlayer.Time = bm.TimeMs;
            CurrentTimeMs = bm.TimeMs;
            ShowOsdNotification($"🔖 پرش به نشانک: {bm.TimeFormatted}");
            ShowBookmarksDrawer = false;
        }

        public void CycleAudioTrack()
        {
            if (_mediaPlayer == null) return;
            UpdateAudioTracksList();
            if (AudioTracks.Count == 0) return;

            int currentTrack = _mediaPlayer.AudioTrack;
            var currentItem = AudioTracks.FirstOrDefault(t => t.Id == currentTrack);
            int idx = AudioTracks.IndexOf(currentItem ?? AudioTracks[0]);
            int nextIdx = (idx + 1) % AudioTracks.Count;
            var nextTrack = AudioTracks[nextIdx];

            _mediaPlayer.SetAudioTrack(nextTrack.Id);
            ShowOsdNotification($"🎵 ترک صدا: {nextTrack.Name}");
        }

        public void UpdateAudioTracksList()
        {
            if (_mediaPlayer == null) return;
            AudioTracks.Clear();
            var tracks = _mediaPlayer.AudioTrackDescription;
            if (tracks != null)
            {
                int currentId = _mediaPlayer.AudioTrack;
                foreach (var t in tracks)
                {
                    AudioTracks.Add(new TrackItemModel
                    {
                        Id = t.Id,
                        Name = string.IsNullOrEmpty(t.Name) ? $"Track #{t.Id}" : t.Name,
                        IsSelected = t.Id == currentId
                    });
                }
            }
        }

        [RelayCommand]
        public void SelectAudioTrack(TrackItemModel track)
        {
            if (track == null || _mediaPlayer == null) return;
            _mediaPlayer.SetAudioTrack(track.Id);
            ShowOsdNotification($"🎵 انتخاب صدا: {track.Name}");
            ShowAudioTracksPopup = false;
        }

        public async void CycleSubtitleTrack()
        {
            if (_mediaPlayer == null) return;

            if (SubtitleTracks.Count <= 1)
            {
                await UpdateSubtitleTracksListAsync();
            }

            if (SubtitleTracks.Count <= 1)
            {
                // Only "Off" exists in list, check if we have a last known subtitle path
                if (!string.IsNullOrEmpty(_lastActiveSubtitlePath) && File.Exists(_lastActiveSubtitlePath))
                {
                    if (_activeSubtitleCues.Count > 0)
                    {
                        // Currently active -> Turn OFF
                        _activeSubtitleCues.Clear();
                        CurrentSubtitleText = string.Empty;
                        HasSubtitleText = false;
                        _loadedSubtitlePath = null;
                        ShowOsdNotification("💬 زیرنویس غیرفعال شد");
                    }
                    else
                    {
                        // Currently off -> Turn ON
                        LoadSubtitleFileInternal(_lastActiveSubtitlePath);
                        ShowOsdNotification($"💬 زیرنویس فعال شد: {Path.GetFileName(_lastActiveSubtitlePath)}");
                    }
                }
                else
                {
                    ShowOsdNotification("⚠️ هیچ زیرنویسی برای این ویدیو یافت نشد");
                }
                return;
            }

            // Find current selected index
            int currentIdx = -1;
            for (int i = 0; i < SubtitleTracks.Count; i++)
            {
                if (SubtitleTracks[i].IsSelected)
                {
                    currentIdx = i;
                    break;
                }
            }

            if (currentIdx == -1)
            {
                currentIdx = _activeSubtitleCues.Count > 0 ? 1 : 0;
            }

            int nextIdx = (currentIdx + 1) % SubtitleTracks.Count;
            var nextTrack = SubtitleTracks[nextIdx];
            await SelectSubtitleTrack(nextTrack);
        }

        public async Task UpdateSubtitleTracksListAsync()
        {
            if (_mediaPlayer == null) return;

            var newTracks = new List<TrackItemModel>();

            // 1. Off / None
            newTracks.Add(new TrackItemModel
            {
                Id = -1,
                Name = "غیرفعال (خاموش)",
                IsSelected = _activeSubtitleCues.Count == 0 && _mediaPlayer.Spu == -1
            });

            // 2. Embedded tracks probed via FFmpeg
            if (!string.IsNullOrEmpty(CurrentMedia?.FilePath))
            {
                var embeddedTracks = await EmbeddedSubtitleExtractorService.GetEmbeddedSubtitleTracksAsync(CurrentMedia.FilePath);
                foreach (var emb in embeddedTracks)
                {
                    newTracks.Add(new TrackItemModel
                    {
                        Id = emb.StreamIndex,
                        Name = $"🎬 داخلی: {emb.DisplayName}",
                        IsEmbedded = true,
                        SubtitleIndex = emb.SubtitleIndex,
                        IsSelected = _activeSubtitleCues.Count > 0 && _loadedSubtitlePath != null && _loadedSubtitlePath.Contains($"_sub_{emb.SubtitleIndex}.srt")
                    });
                }
            }

            // 3. External subtitles in folder
            if (!string.IsNullOrEmpty(CurrentMedia?.FilePath))
            {
                string? dir = Path.GetDirectoryName(CurrentMedia.FilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string baseName = Path.GetFileNameWithoutExtension(CurrentMedia.FilePath);
                    var subFiles = Directory.GetFiles(dir, $"{baseName}*.srt")
                        .Concat(Directory.GetFiles(dir, $"{baseName}*.vtt"))
                        .ToList();

                    foreach (var sf in subFiles)
                    {
                        newTracks.Add(new TrackItemModel
                        {
                            Id = sf.GetHashCode(),
                            Name = $"📂 خارجی: {Path.GetFileName(sf)}",
                            FilePath = sf,
                            IsSelected = _loadedSubtitlePath == sf
                        });
                    }
                }
            }

            SubtitleTracks.Clear();
            foreach (var t in newTracks)
            {
                SubtitleTracks.Add(t);
            }
        }

        public void UpdateSubtitleTracksList()
        {
            _ = UpdateSubtitleTracksListAsync();
        }

        [RelayCommand]
        public async Task SelectSubtitleTrack(TrackItemModel track)
        {
            if (track == null || _mediaPlayer == null) return;

            if (track.Id == -1)
            {
                _activeSubtitleCues.Clear();
                CurrentSubtitleText = string.Empty;
                HasSubtitleText = false;
                _mediaPlayer.SetSpu(-1);
                _loadedSubtitlePath = null;
                ShowOsdNotification("💬 زیرنویس غیرفعال شد");
            }
            else if (track.IsEmbedded && track.SubtitleIndex >= 0)
            {
                ShowOsdNotification("⏳ در حال استخراج و فعال‌سازی زیرنویس داخلی...");
                string? extracted = await EmbeddedSubtitleExtractorService.ExtractEmbeddedSubtitleToSrtAsync(CurrentMedia.FilePath, track.SubtitleIndex);
                if (!string.IsNullOrEmpty(extracted))
                {
                    LoadSubtitleFileInternal(extracted);
                    _mediaPlayer.SetSpu(-1);
                    ShowOsdNotification($"✨ {track.Name} فعال شد");
                }
                else
                {
                    ShowOsdNotification("⚠️ خطا در استخراج زیرنویس");
                }
            }
            else if (!string.IsNullOrEmpty(track.FilePath))
            {
                LoadSubtitleFileInternal(track.FilePath);
                _mediaPlayer.SetSpu(-1);
                ShowOsdNotification($"💬 {track.Name} فعال شد");
            }

            foreach (var t in SubtitleTracks) t.IsSelected = (t == track);
            ShowSubtitlesPopup = false;
        }

        [RelayCommand]
        public void LoadExternalSubtitle()
        {
            var dialog = new OpenFileDialog
            {
                Title = "انتخاب فایل زیرنویس",
                Filter = "فایل‌های زیرنویس|*.srt;*.vtt;*.ass;*.ssa;*.sub|همه فایل‌ها|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSubtitleFileInternal(dialog.FileName);
                ShowOsdNotification($"💬 زیرنویس بارگذاری شد: {Path.GetFileName(dialog.FileName)}");
            }
        }

        [RelayCommand]
        public void AdjustSubtitleDelay(string deltaStr)
        {
            if (double.TryParse(deltaStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double delta))
            {
                SubtitleDelaySeconds = Math.Round(SubtitleDelaySeconds + delta, 2);
                long delayMicroseconds = (long)(SubtitleDelaySeconds * 1_000_000);
                _mediaPlayer?.SetSpuDelay(delayMicroseconds);
                string sign = SubtitleDelaySeconds > 0 ? "+" : "";
                ShowOsdNotification($"⏱ سینک زیرنویس: {sign}{SubtitleDelaySeconds:0.00}s");
            }
        }

        [RelayCommand]
        public void ResetSubtitleDelay()
        {
            SubtitleDelaySeconds = 0.0;
            _mediaPlayer?.SetSpuDelay(0);
            ShowOsdNotification("⏱ سینک زیرنویس: 0.0s (ریست)");
        }

        [RelayCommand]
        public void AdjustAudioDelay(string deltaStr)
        {
            if (int.TryParse(deltaStr, out int delta))
            {
                AudioDelayMilliseconds += delta;
                long delayMicroseconds = (long)(AudioDelayMilliseconds * 1_000);
                _mediaPlayer?.SetAudioDelay(delayMicroseconds);
                string sign = AudioDelayMilliseconds > 0 ? "+" : "";
                ShowOsdNotification($"🎵 سینک صدا: {sign}{AudioDelayMilliseconds}ms");
            }
        }

        [RelayCommand]
        public void ResetAudioDelay()
        {
            AudioDelayMilliseconds = 0;
            _mediaPlayer?.SetAudioDelay(0);
            ShowOsdNotification("🎵 سینک صدا: 0ms (ریست)");
        }

        [RelayCommand]
        public void ToggleSubtitleStudio()
        {
            ShowSubtitleStudioModal = !ShowSubtitleStudioModal;
            if (ShowSubtitleStudioModal)
            {
                ShowSubtitlesPopup = false;
                ShowAudioTracksPopup = false;
                ShowSpeedPopup = false;
            }
        }

        [RelayCommand]
        public void ToggleSubtitleBold()
        {
            IsSubtitleBold = !IsSubtitleBold;
            ShowOsdNotification(IsSubtitleBold ? "🔤 فونت زیرنویس: بولد (Bold)" : "🔤 فونت زیرنویس: عادی (Normal)");
        }

        [RelayCommand]
        public void ToggleSubtitleBackground()
        {
            HasSubtitleBackground = !HasSubtitleBackground;
            ShowOsdNotification(HasSubtitleBackground ? "🔲 پس‌زمینه زیرنویس: فعال" : "🔲 پس‌زمینه زیرنویس: غیرفعال");
        }

        [RelayCommand]
        public void ToggleSubtitleShadow()
        {
            HasSubtitleShadow = !HasSubtitleShadow;
            ShowOsdNotification(HasSubtitleShadow ? "🌑 سایه زیرنویس: فعال" : "🌑 سایه زیرنویس: غیرفعال");
        }

        [RelayCommand]
        public void ChangeSubtitleFontSize(string deltaStr)
        {
            if (int.TryParse(deltaStr, out int delta))
            {
                SubtitleFontSize = Math.Clamp(SubtitleFontSize + delta, 14, 64);
                ShowOsdNotification($"🔤 سایز زیرنویس: {SubtitleFontSize}px");
            }
        }

        [RelayCommand]
        public void SetSubtitleFontSize(string sizeStr)
        {
            if (int.TryParse(sizeStr, out int size))
            {
                SubtitleFontSize = size;
                ShowOsdNotification($"🔤 سایز زیرنویس: {size}px");
            }
        }

        [RelayCommand]
        public void SetSubtitleColor(string hex)
        {
            SubtitleColorHex = hex;
            ShowOsdNotification("🎨 رنگ زیرنویس اعمال شد");
        }

        [RelayCommand]
        public void SetSubtitleBackgroundColor(string hex)
        {
            SubtitleBackgroundColorHex = hex;
            HasSubtitleBackground = true;
            ShowOsdNotification("🔲 رنگ پس‌زمینه زیرنویس اعمال شد");
        }

        [RelayCommand]
        public void SetSubtitleFont(string fontName)
        {
            SubtitleFontFamily = fontName;
            ShowOsdNotification($"🔤 فونت زیرنویس: {fontName}");
        }

        [RelayCommand]
        public void SetSubtitleAlignment(string align)
        {
            SubtitleAlignment = align;
            string label = align == "Right" ? "راست‌چین" : (align == "Left" ? "چپ‌چین" : "وسط‌چین");
            ShowOsdNotification($"↔️ چینش زیرنویس: {label}");
        }

        [RelayCommand]
        public void CancelTranslation()
        {
            if (_translationCts != null && !_translationCts.IsCancellationRequested)
            {
                _translationCts.Cancel();
                TranslationStatusText = "در حال لغو ترجمه...";
                ShowOsdNotification("🛑 لغو فرآیند ترجمه زیرنویس");
            }
        }

        [RelayCommand]
        public async Task TranslateSubtitleAsync()
        {
            if (IsTranslatingSubtitle) return;

            string? subPath = null;
            string? videoDir = Path.GetDirectoryName(CurrentMedia.FilePath);

            if (!string.IsNullOrEmpty(videoDir) && Directory.Exists(videoDir))
            {
                string baseName = Path.GetFileNameWithoutExtension(CurrentMedia.FilePath);
                var matchingSubs = Directory.GetFiles(videoDir, $"{baseName}*.srt")
                    .Concat(Directory.GetFiles(videoDir, $"{baseName}*.vtt"))
                    .Where(f => !f.EndsWith(".fa.srt", StringComparison.OrdinalIgnoreCase) && !f.EndsWith("_FA.srt", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingSubs.Count > 0)
                {
                    subPath = matchingSubs[0];
                }
            }

            if (string.IsNullOrEmpty(subPath))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "انتخاب فایل زیرنویس برای ترجمه به فارسی",
                    Filter = "فایل‌های زیرنویس (*.srt;*.vtt)|*.srt;*.vtt|همه فایل‌ها|*.*"
                };
                if (dialog.ShowDialog() == true)
                {
                    subPath = dialog.FileName;
                }
            }

            if (string.IsNullOrEmpty(subPath))
            {
                ShowOsdNotification("⚠️ فایل زیرنویسی برای ترجمه یافت نشد");
                return;
            }

            try
            {
                _translationCts = new System.Threading.CancellationTokenSource();
                IsTranslatingSubtitle = true;
                TranslationProgress = 0.0;
                TranslationStatusText = "در حال آماده‌سازی و خواندن فایل زیرنویس...";
                ShowOsdNotification("⏳ آغاز ترجمه زیرنویس به فارسی...");

                var progress = new Progress<SubtitleTranslationProgressInfo>(p =>
                {
                    TranslationProgress = p.Percent;
                    TranslationStatusText = p.StatusText;
                });

                var (success, outputPath, message) = await SubtitleTranslatorService.TranslateSubtitleFileAsync(
                    subPath, 
                    "fa", 
                    progress, 
                    _translationCts.Token);

                if (success && !string.IsNullOrEmpty(outputPath))
                {
                    LoadSubtitleFileInternal(outputPath);
                    _mediaPlayer?.SetSpu(-1);
                    ShowOsdNotification("✅ زیرنویس فارسی ترجمه و بارگذاری شد");
                    ToastService.Instance.ShowSuccess("زیرنویس با موفقیت به فارسی ترجمه و بارگذاری شد.");
                }
                else
                {
                    ToastService.Instance.ShowError(message);
                    ShowOsdNotification($"❌ {message}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Unexpected translation error", ex);
                ToastService.Instance.ShowError($"خطای غیرمنتظره در ترجمه: {ex.Message}");
            }
            finally
            {
                IsTranslatingSubtitle = false;
                _translationCts?.Dispose();
                _translationCts = null;
            }
        }

        [RelayCommand]
        public void FixSubtitleEncoding()
        {
            string? subPath = null;
            string? videoDir = Path.GetDirectoryName(CurrentMedia.FilePath);

            if (!string.IsNullOrEmpty(videoDir) && Directory.Exists(videoDir))
            {
                string baseName = Path.GetFileNameWithoutExtension(CurrentMedia.FilePath);
                var subs = Directory.GetFiles(videoDir, $"{baseName}*.srt").ToList();
                if (subs.Count > 0) subPath = subs[0];
            }

            if (string.IsNullOrEmpty(subPath))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "انتخاب فایل زیرنویس برای اصلاح انکودینگ فارسی",
                    Filter = "فایل‌های زیرنویس (*.srt)|*.srt|همه فایل‌ها|*.*"
                };
                if (dialog.ShowDialog() == true) subPath = dialog.FileName;
            }

            if (!string.IsNullOrEmpty(subPath))
            {
                string fixedPath = SubtitleTranslatorService.FixSubtitleEncoding(subPath);
                LoadSubtitleFileInternal(fixedPath);
                _mediaPlayer?.SetSpu(-1);
                ShowOsdNotification("✅ انکودینگ زیرنویس به UTF-8 استاندارد تبدیل شد");
                ToastService.Instance.ShowSuccess("انکودینگ زیرنویس اصلاح و بارگذاری شد.");
            }
        }

        public void TakeSnapshot()
        {
            if (_mediaPlayer == null) return;
            try
            {
                string picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string snapshotsDir = Path.Combine(picturesDir, "MovieManager Snapshots");
                Directory.CreateDirectory(snapshotsDir);

                string safeTitle = Path.GetFileNameWithoutExtension(CurrentMedia.FilePath);
                string filename = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(snapshotsDir, filename);

                _mediaPlayer.TakeSnapshot(0, fullPath, 0, 0);
                ShowOsdNotification($"📸 اسکرین‌شات ذخیره شد در Pictures");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در ذخیره اسکرین‌شات: {ex.Message}");
            }
        }

        [RelayCommand]
        public void PlayNext()
        {
            if (Playlist.Count <= 1) return;
            _currentPlaylistIndex = (_currentPlaylistIndex + 1) % Playlist.Count;
            LoadMedia(Playlist[_currentPlaylistIndex]);
        }

        [RelayCommand]
        public void PlayPrevious()
        {
            if (Playlist.Count <= 1) return;
            _currentPlaylistIndex = (_currentPlaylistIndex - 1 + Playlist.Count) % Playlist.Count;
            LoadMedia(Playlist[_currentPlaylistIndex]);
        }

        [RelayCommand]
        public void PlayPlaylistItem(VideoFile item)
        {
            if (item == null) return;
            int idx = Playlist.IndexOf(item);
            if (idx >= 0)
            {
                _currentPlaylistIndex = idx;
                LoadMedia(item);
                ShowPlaylistDrawer = false;
            }
        }

        public void OpenContainingFolder()
        {
            if (File.Exists(CurrentMedia.FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{CurrentMedia.FilePath}\"");
            }
        }

        public void OpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "باز کردن فایل ویدیو",
                Filter = "فایل‌های ویدیویی|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.flv;*.webm;*.m4v;*.ts|همه فایل‌ها|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var newMedia = new VideoFile
                {
                    FilePath = dialog.FileName,
                    FileName = Path.GetFileName(dialog.FileName),
                    FormattedTitle = Path.GetFileNameWithoutExtension(dialog.FileName)
                };
                Playlist.Clear();
                Playlist.Add(newMedia);
                _currentPlaylistIndex = 0;
                LoadMedia(newMedia);
            }
        }

        [RelayCommand]
        public void ClosePlayer()
        {
            RequestCloseWindow?.Invoke();
            Dispose();
        }

        public void ToggleMirrorHorizontal()
        {
            IsMirrorHorizontal = !IsMirrorHorizontal;
            ShowOsdNotification(IsMirrorHorizontal ? "🪞 تصویر آینه‌ای (Horizontal Flip): فعال" : "🪞 تصویر آینه‌ای: غیرفعال");
        }

        public void ToggleFlipVertical()
        {
            IsFlipVertical = !IsFlipVertical;
            ShowOsdNotification(IsFlipVertical ? "🔄 فلیپ عمودی تصویر: فعال" : "🔄 فلیپ عمودی تصویر: غیرفعال");
        }

        public void ToggleAudioNormalizer()
        {
            IsAudioNormalizerActive = !IsAudioNormalizerActive;
            ShowOsdNotification(IsAudioNormalizerActive ? "🎚 نرمال‌سازی صدا (Audio Normalizer): فعال" : "🎚 نرمال‌سازی صدا: غیرفعال");
        }

        public void ToggleAudioDenoise()
        {
            IsAudioDenoiseActive = !IsAudioDenoiseActive;
            ShowOsdNotification(IsAudioDenoiseActive ? "🔇 حذف نویز صدا (Audio De-noise): فعال" : "🔇 حذف نویز صدا: غیرفعال");
        }

        public void ToggleContinuousCapture()
        {
            IsContinuousCapture = !IsContinuousCapture;
            if (IsContinuousCapture)
            {
                TakeSnapshot();
            }
            ShowOsdNotification(IsContinuousCapture ? "📸 ضبط پیوسته فریم: شروع شد" : "📸 ضبط پیوسته فریم: متوقف شد");
        }

        public void ToggleAudioRecord()
        {
            IsRecordingAudio = !IsRecordingAudio;
            ShowOsdNotification(IsRecordingAudio ? "🎙 ضبط صدا: آغاز شد" : "🎙 ضبط صدا: متوقف شد");
        }

        public void ToggleAudioEqualizer()
        {
            ShowOsdNotification("🎛 اکولایزر صدا: حالت پیش‌فرض (Flat)");
        }

        public void QuickNavigatePlaylist()
        {
            ShowPlaylistDrawer = !ShowPlaylistDrawer;
        }

        private static string FormatTime(long ms)
        {
            var ts = TimeSpan.FromMilliseconds(Math.Max(0, ms));
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"mm\:ss");
        }

        public void Dispose()
        {
            _uiTimer.Stop();
            _mouseIdleTimer.Stop();
            _osdTimer.Stop();

            if (CurrentMedia != null && CurrentTimeMs > 2000 && !_hasMarkedWatched)
            {
                try
                {
                    using var db = new AppDbContext();
                    var dbItem = db.VideoFiles.Find(CurrentMedia.Id);
                    if (dbItem != null)
                    {
                        dbItem.WatchProgressSeconds = CurrentTimeMs / 1000L;
                        dbItem.WatchProgressPercent = Math.Clamp(Progress * 100.0, 0.0, 100.0);
                        db.SaveChanges();
                    }
                }
                catch { }
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            if (_libVLC != null)
            {
                _libVLC.Dispose();
                _libVLC = null;
            }
        }
    }
}
```

---

# بخش ۲: `PlayerWindow.xaml`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Views\PlayerWindow.xaml`

```xaml
<Window x:Class="MovieManagerDesktop.Views.PlayerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:vlc="clr-namespace:LibVLCSharp.WPF;assembly=LibVLCSharp.WPF"
        mc:Ignorable="d"
        Title="{Binding MediaTitle, FallbackValue='پخش‌کننده ویدیو'}"
        Height="720" Width="1280"
        MinHeight="400" MinWidth="600"
        WindowStartupLocation="CenterScreen"
        WindowStyle="None"
        ResizeMode="CanResizeWithGrip"
        Background="#000000"
        Topmost="{Binding IsAlwaysOnTop, Mode=OneWay}"
        Loaded="Window_Loaded"
        Closing="Window_Closing"
        LocationChanged="Window_LocationChanged"
        SizeChanged="Window_SizeChanged"
        StateChanged="Window_StateChanged">

    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="0" 
                      ResizeBorderThickness="8" 
                      GlassFrameThickness="0" 
                      CornerRadius="0"/>
    </WindowChrome.WindowChrome>

    <Grid Background="#000000" ClipToBounds="True">
        <vlc:VideoView x:Name="VlcVideoView" 
                       HorizontalAlignment="Stretch" 
                       VerticalAlignment="Stretch"
                       Background="Black"/>
    </Grid>
</Window>
```

---

# بخش ۳: `PlayerWindow.xaml.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Views\PlayerWindow.xaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class PlayerWindow : Window
    {
        private PlayerViewModel ViewModel => (PlayerViewModel)DataContext;
        private PlayerOverlayWindow? _overlayWindow;

        private WindowState _previousWindowState = WindowState.Normal;
        private WindowStyle _previousWindowStyle = WindowStyle.None;
        private ResizeMode _previousResizeMode = ResizeMode.CanResize;
        private Rect _previousWindowBounds;

        public PlayerWindow(VideoFile media, List<VideoFile>? playlist = null, int initialIndex = 0)
        {
            InitializeComponent();
            var vm = new PlayerViewModel(media, playlist, initialIndex, autoPlay: false);
            DataContext = vm;
        }

        public PlayerWindow(PlayerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.MediaPlayer != null)
            {
                VlcVideoView.MediaPlayer = ViewModel.MediaPlayer;
            }

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.RequestWindowScale += ViewModel_RequestWindowScale;
                ViewModel.RequestToggleMaximize += ViewModel_RequestToggleMaximize;
                ViewModel.RequestAlwaysOnTop += ViewModel_RequestAlwaysOnTop;
                ViewModel.RequestCloseWindow += () => Dispatcher.Invoke(Close);

                // Restore saved window size & position
                var settings = SettingsManager.LoadSettings();
                if (settings.PlayerWindowWidth.HasValue && settings.PlayerWindowHeight.HasValue)
                {
                    double w = Math.Max(MinWidth, Math.Min(settings.PlayerWindowWidth.Value, SystemParameters.VirtualScreenWidth));
                    double h = Math.Max(MinHeight, Math.Min(settings.PlayerWindowHeight.Value, SystemParameters.VirtualScreenHeight));
                    Width = w;
                    Height = h;

                    if (settings.PlayerWindowLeft.HasValue && settings.PlayerWindowTop.HasValue)
                    {
                        double l = settings.PlayerWindowLeft.Value;
                        double t = settings.PlayerWindowTop.Value;

                        if (l >= SystemParameters.VirtualScreenLeft - 50 && l + 100 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                            t >= SystemParameters.VirtualScreenTop - 50 && t + 100 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                        {
                            Left = l;
                            Top = t;
                        }
                    }
                }

                if (settings.PlayerAlwaysOnTop)
                {
                    ViewModel.IsAlwaysOnTop = true;
                    Topmost = true;
                }

                // Create and show transparent controls overlay directly over video window
                _overlayWindow = new PlayerOverlayWindow(ViewModel, this)
                {
                    Owner = this,
                    Left = this.Left,
                    Top = this.Top,
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                    Topmost = this.Topmost
                };
                _overlayWindow.Show();

                if (ViewModel.IsAlwaysOnTop)
                {
                    Dispatcher.InvokeAsync(() => ApplyAlwaysOnTop(true), System.Windows.Threading.DispatcherPriority.Loaded);
                }

                // Start playback now that the native HWND is ready and attached to VideoView
                if (!ViewModel.IsPlaying)
                {
                    ViewModel.StartPlayback();
                }
            }

            this.Activated += Window_Activated;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private void ApplyAlwaysOnTop(bool alwaysOnTop)
        {
            Topmost = alwaysOnTop;
            if (_overlayWindow != null)
            {
                _overlayWindow.Topmost = alwaysOnTop;
            }

            var helperMain = new WindowInteropHelper(this);
            if (helperMain.Handle != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(helperMain.Handle, GWL_EXSTYLE);
                if (alwaysOnTop)
                    SetWindowLong(helperMain.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                else
                    SetWindowLong(helperMain.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);

                SetWindowPos(helperMain.Handle, alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }

            if (_overlayWindow != null)
            {
                var helperOverlay = new WindowInteropHelper(_overlayWindow);
                if (helperOverlay.Handle != IntPtr.Zero)
                {
                    int exStyle = GetWindowLong(helperOverlay.Handle, GWL_EXSTYLE);
                    if (alwaysOnTop)
                        SetWindowLong(helperOverlay.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    else
                        SetWindowLong(helperOverlay.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);

                    SetWindowPos(helperOverlay.Handle, alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            }
        }

        private void ViewModel_RequestAlwaysOnTop(bool alwaysOnTop)
        {
            Dispatcher.Invoke(() =>
            {
                ApplyAlwaysOnTop(alwaysOnTop);
                SavePlayerWindowSettings();
            });
        }

        private void SavePlayerWindowSettings()
        {
            if (IsLoaded && WindowState == WindowState.Normal && ViewModel?.IsFullscreen != true)
            {
                var settings = SettingsManager.LoadSettings();
                settings.PlayerWindowWidth = Width;
                settings.PlayerWindowHeight = Height;
                settings.PlayerWindowLeft = Left;
                settings.PlayerWindowTop = Top;
                settings.PlayerAlwaysOnTop = ViewModel?.IsAlwaysOnTop ?? false;
                SettingsManager.SaveSettings(settings);
            }
        }

        private void Window_Activated(object? sender, EventArgs e)
        {
            if (ViewModel?.IsAlwaysOnTop == true)
            {
                ApplyAlwaysOnTop(true);
            }

            // Keep overlay on top when player window is activated
            if (_overlayWindow != null && _overlayWindow.IsLoaded)
            {
                _overlayWindow.Activate();
            }
        }

        private void SyncOverlayBounds()
        {
            if (_overlayWindow == null || !IsLoaded) return;

            // Use actual rendered position and size for both Normal and Maximized states
            var point = PointToScreen(new Point(0, 0));
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            _overlayWindow.Left = point.X * dpiX;
            _overlayWindow.Top = point.Y * dpiY;
            _overlayWindow.Width = this.ActualWidth;
            _overlayWindow.Height = this.ActualHeight;

            if (ViewModel?.IsAlwaysOnTop == true)
            {
                _overlayWindow.Topmost = true;
                this.Topmost = true;
            }

            ViewModel?.UpdateWindowDimensions(this.ActualWidth, this.ActualHeight);
            
            Dispatcher.InvokeAsync(() =>
            {
                ViewModel?.EnforceDisableInternalSubtitles();
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => ViewModel?.EnforceDisableInternalSubtitles());
                });
            });

            SavePlayerWindowSettings();
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            SyncOverlayBounds();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SyncOverlayBounds();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (_overlayWindow == null) return;

            if (WindowState == WindowState.Minimized)
            {
                _overlayWindow.Hide();
            }
            else
            {
                _overlayWindow.Show();
                Dispatcher.InvokeAsync(() =>
                {
                    SyncOverlayBounds();
                    _overlayWindow?.Activate();
                    ViewModel?.EnforceDisableInternalSubtitles();
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
            {
                if (ViewModel?.IsFullscreen == true)
                {
                    ApplyFullscreen();
                }
                else
                {
                    RestoreWindowState();
                }
            }
        }

        private void ViewModel_RequestWindowScale(double scale)
        {
            Dispatcher.Invoke(() =>
            {
                if (ViewModel.IsFullscreen)
                {
                    ViewModel.IsFullscreen = false;
                }

                WindowState = WindowState.Normal;

                double baseW = 1280;
                double baseH = 720;

                double targetW = Math.Max(MinWidth, baseW * scale);
                double targetH = Math.Max(MinHeight, baseH * scale);

                double screenW = SystemParameters.WorkArea.Width;
                double screenH = SystemParameters.WorkArea.Height;

                if (targetW > screenW) targetW = screenW;
                if (targetH > screenH) targetH = screenH;

                Width = targetW;
                Height = targetH;

                Left = (screenW - targetW) / 2 + SystemParameters.WorkArea.Left;
                Top = (screenH - targetH) / 2 + SystemParameters.WorkArea.Top;

                SyncOverlayBounds();
                ViewModel.ShowOsdNotification($"اندازه پنجره: {scale:0.0}x");
            });
        }

        private void ViewModel_RequestToggleMaximize()
        {
            Dispatcher.Invoke(() =>
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            });
        }

        private void ApplyFullscreen()
        {
            Dispatcher.Invoke(() =>
            {
                _previousWindowState = WindowState;
                _previousWindowStyle = WindowStyle;
                _previousResizeMode = ResizeMode;
                _previousWindowBounds = new Rect(Left, Top, Width, Height);

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                Topmost = true;
                WindowState = WindowState.Normal;

                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;

                if (_overlayWindow != null)
                {
                    _overlayWindow.Left = Left;
                    _overlayWindow.Top = Top;
                    _overlayWindow.Width = Width;
                    _overlayWindow.Height = Height;
                    _overlayWindow.Topmost = true;
                }
            });
        }

        private void RestoreWindowState()
        {
            Dispatcher.Invoke(() =>
            {
                Topmost = false;
                WindowStyle = _previousWindowStyle;
                ResizeMode = _previousResizeMode;

                Left = _previousWindowBounds.Left;
                Top = _previousWindowBounds.Top;
                Width = Math.Max(MinWidth, _previousWindowBounds.Width);
                Height = Math.Max(MinHeight, _previousWindowBounds.Height);
                WindowState = _previousWindowState;

                if (_overlayWindow != null)
                {
                    _overlayWindow.Topmost = false;
                    SyncOverlayBounds();
                }
            });
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }

                if (ViewModel != null)
                {
                    ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    ViewModel.RequestWindowScale -= ViewModel_RequestWindowScale;
                    ViewModel.RequestToggleMaximize -= ViewModel_RequestToggleMaximize;
                    ViewModel.Dispose();
                }
            }
            catch
            {
            }
        }

        #region Native Magnetic Edge Snapping
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int WM_MOVING = 0x0216;
        private const int SNAP_THRESHOLD = 20;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOVING && WindowState == WindowState.Normal && ViewModel?.IsFullscreen != true)
            {
                var rc = System.Runtime.InteropServices.Marshal.PtrToStructure<RECT>(lParam);
                int width = rc.right - rc.left;
                int height = rc.bottom - rc.top;

                IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;

                    // Magnetic Snap Left
                    if (Math.Abs(rc.left - work.left) <= SNAP_THRESHOLD)
                    {
                        rc.left = work.left;
                        rc.right = rc.left + width;
                    }
                    // Magnetic Snap Right
                    else if (Math.Abs(rc.right - work.right) <= SNAP_THRESHOLD)
                    {
                        rc.right = work.right;
                        rc.left = rc.right - width;
                    }

                    // Magnetic Snap Top
                    if (Math.Abs(rc.top - work.top) <= SNAP_THRESHOLD)
                    {
                        rc.top = work.top;
                        rc.bottom = rc.top + height;
                    }
                    // Magnetic Snap Bottom
                    else if (Math.Abs(rc.bottom - work.bottom) <= SNAP_THRESHOLD)
                    {
                        rc.bottom = work.bottom;
                        rc.top = rc.bottom - height;
                    }

                    System.Runtime.InteropServices.Marshal.StructureToPtr(rc, lParam, true);
                }
            }
            return IntPtr.Zero;
        }
        #endregion
    }
}
```

---

# بخش ۴: `PlayerOverlayWindow.xaml`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Views\PlayerOverlayWindow.xaml`

```xaml
<Window x:Class="MovieManagerDesktop.Views.PlayerOverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:viewmodels="clr-namespace:MovieManagerDesktop.ViewModels"
        mc:Ignorable="d"
        Title="Player Overlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="#01000000"
        ShowInTaskbar="False"
        Focusable="True"
        Topmost="{Binding IsAlwaysOnTop, Mode=OneWay}"
        Loaded="Window_Loaded"
        PreviewKeyDown="Window_PreviewKeyDown"
        MouseMove="Window_MouseMove"
        PreviewMouseWheel="Window_PreviewMouseWheel"
        PreviewMouseDown="Window_PreviewMouseDown">

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>

        <Style x:Key="VideoSeekBarStyle" TargetType="{x:Type Slider}">
            <Setter Property="Stylus.IsPressAndHoldEnabled" Value="false"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type Slider}">
                        <Grid VerticalAlignment="Center">
                            <Track x:Name="PART_Track">
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command="{x:Static Slider.DecreaseLarge}">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType="{x:Type RepeatButton}">
                                                <Border Background="#00ADB5" Height="5" CornerRadius="2.5"/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command="{x:Static Slider.IncreaseLarge}">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType="{x:Type RepeatButton}">
                                                <Border Background="#40FFFFFF" Height="5" CornerRadius="2.5"/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb x:Name="Thumb">
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="{x:Type Thumb}">
                                                <Ellipse Width="15" Height="15" Fill="#00FFF0" Stroke="#00ADB5" StrokeThickness="2" Cursor="Hand"/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="PlayerIconBtn" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Foreground" Value="#EEEEEE"/>
            <Setter Property="Padding" Value="6"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#30FFFFFF"/>
                                <Setter Property="Foreground" Value="#00FFF0"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter Property="Background" Value="#5000ADB5"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="BigPlayBtn" TargetType="Button">
            <Setter Property="Background" Value="#00ADB5"/>
            <Setter Property="Foreground" Value="#121820"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Width" Value="52"/>
            <Setter Property="Height" Value="52"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="26">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#00FFF0"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter Property="Background" Value="#008B92"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid Background="Transparent" x:Name="OverlayRootGrid">
        <!-- ═══════════ 1. OSD MESSAGE OVERLAY (Top-Left under toolbar) ═══════════ -->
        <Border HorizontalAlignment="Left" VerticalAlignment="Top" Margin="24,54,0,0"
                Background="#EE0A0E17" CornerRadius="8" Padding="14,8"
                BorderBrush="#00ADB5" BorderThickness="1.5"
                Visibility="{Binding ShowOsd, Converter={StaticResource BoolToVis}}"
                IsHitTestVisible="False">
            <TextBlock Text="{Binding OsdMessage}" Foreground="#00FFF0" FontSize="14" FontWeight="SemiBold" FlowDirection="RightToLeft"/>
        </Border>

        <!-- ═══════════ 1.5 COMPACT CORNER SUBTITLE TRANSLATION BADGE ═══════════ -->
        <Border HorizontalAlignment="Left" VerticalAlignment="Top" Margin="20,54,0,0"
                Background="#EE0A0E17" CornerRadius="8" Padding="10,6" MaxWidth="250"
                BorderBrush="#00ADB5" BorderThickness="1.2"
                Visibility="{Binding IsTranslatingSubtitle, Converter={StaticResource BoolToVis}}"
                Panel.ZIndex="35" FlowDirection="RightToLeft">
            <Border.Effect>
                <DropShadowEffect BlurRadius="12" ShadowDepth="2" Direction="270" Color="#000000" Opacity="0.8"/>
            </Border.Effect>
            <StackPanel>
                <DockPanel>
                    <Button DockPanel.Dock="Left" Command="{Binding CancelTranslationCommand}" Style="{StaticResource PlayerIconBtn}" Height="20" Width="20" Padding="0" Margin="4,0,0,0" ToolTip="لغو ترجمه">
                        <materialDesign:PackIcon Kind="Close" Width="14" Height="14" Foreground="#FF5252"/>
                    </Button>
                    <TextBlock Text="{Binding TranslationProgress, StringFormat='{}{0:0}٪'}" Foreground="#00FFF0" FontWeight="Bold" FontSize="12" DockPanel.Dock="Left" VerticalAlignment="Center" FontFamily="Consolas, Segoe UI" Margin="4,0,0,0"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <materialDesign:PackIcon Kind="Translate" Width="15" Height="15" Foreground="#00FFF0" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="{Binding TranslationStatusText}" Foreground="#EEEEEE" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" MaxWidth="140"/>
                    </StackPanel>
                </DockPanel>
                <ProgressBar Minimum="0" Maximum="100" Value="{Binding TranslationProgress}" 
                             Height="3" Foreground="#00ADB5" Background="#25FFFFFF" BorderThickness="0" Margin="0,5,0,0"/>
            </StackPanel>
        </Border>

        <!-- ═══════════ 2. TOP BAR (WINDOW HEADER & CONTROLS) ═══════════ -->
        <Border x:Name="TopControlsBar" VerticalAlignment="Top" 
                Background="#E60A0E17" Padding="12,6"
                BorderBrush="#20FFFFFF" BorderThickness="0,0,0,1"
                Visibility="{Binding ShowTopBar, Converter={StaticResource BoolToVis}}"
                MouseLeftButtonDown="TopControlsBar_MouseLeftButtonDown">
            <Grid FlowDirection="RightToLeft">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Right in RTL (Grid.Column 0): Window Action Buttons (Minimize, Maximize, Close) -->
                <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center" FlowDirection="LeftToRight">
                    <Button Click="MinimizeWindow_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="کوچک‌نمایی" Margin="2,0">
                        <materialDesign:PackIcon Kind="WindowMinimize" Width="18" Height="18"/>
                    </Button>
                    <Button Click="MaximizeWindow_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="حداکثر / پنجره" Margin="2,0">
                        <materialDesign:PackIcon Kind="WindowMaximize" Width="18" Height="18"/>
                    </Button>
                    <Button Command="{Binding ClosePlayerCommand}" Style="{StaticResource PlayerIconBtn}" ToolTip="بستن (Esc / F4)" Margin="2,0">
                        <materialDesign:PackIcon Kind="Close" Width="20" Height="20" Foreground="#FF5252"/>
                    </Button>
                </StackPanel>

                <!-- Center in RTL (Grid.Column 1): Media Title & Season/Episode -->
                <StackPanel Grid.Column="1" VerticalAlignment="Center" HorizontalAlignment="Center" Margin="12,0">
                    <TextBlock Text="{Binding MediaTitle}" Foreground="#FFFFFF" FontWeight="Bold" FontSize="14" HorizontalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                    <TextBlock Text="{Binding SeasonEpisodeText}" Foreground="#00FFF0" FontSize="12" FontWeight="SemiBold" HorizontalAlignment="Center" Margin="0,2,0,0">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Visibility" Value="Visible"/>
                                <Style.Triggers>
                                    <Trigger Property="Text" Value="">
                                        <Setter Property="Visibility" Value="Collapsed"/>
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </StackPanel>

                <!-- Left in RTL (Grid.Column 2): Quick Navigation & Options -->
                <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                    <Button Command="{Binding ToggleAlwaysOnTopCommand}" Style="{StaticResource PlayerIconBtn}" 
                            ToolTip="همیشه در بالا (Always On Top)" Margin="0,0,4,0">
                        <materialDesign:PackIcon Width="20" Height="20">
                            <materialDesign:PackIcon.Style>
                                <Style TargetType="materialDesign:PackIcon">
                                    <Setter Property="Kind" Value="PinOutline"/>
                                    <Setter Property="Foreground" Value="#EEEEEE"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsAlwaysOnTop}" Value="True">
                                            <Setter Property="Kind" Value="Pin"/>
                                            <Setter Property="Foreground" Value="#00FFF0"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </materialDesign:PackIcon.Style>
                        </materialDesign:PackIcon>
                    </Button>
                    <Button Click="TogglePlaylistDrawer_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="لیست پخش / قسمت‌ها (F6)" Margin="0,0,4,0">
                        <materialDesign:PackIcon Kind="PlaylistPlay" Width="22" Height="22"/>
                    </Button>
                    <Button Click="ToggleBookmarksDrawer_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="نشانک‌ها (H / P)" Margin="0,0,4,0">
                        <materialDesign:PackIcon Kind="BookmarkOutline" Width="20" Height="20"/>
                    </Button>
                    <Button Click="ToggleShortcutsHelp_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="راهنمای کلیدهای میانبر (F1)" Margin="0,0,4,0">
                        <materialDesign:PackIcon Kind="KeyboardOutline" Width="20" Height="20"/>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>

        <!-- ═══════════ 2.5 REAL-TIME SUBTITLE DISPLAY LAYER (UNIQUE SCALED LAYER) ═══════════ -->
        <Border HorizontalAlignment="Center" VerticalAlignment="Bottom" 
                Margin="{Binding SubtitleContainerMargin}"
                Padding="0"
                Visibility="{Binding HasSubtitleText, Converter={StaticResource BoolToVis}}"
                IsHitTestVisible="False" Panel.ZIndex="10">
            <Border Background="{Binding SubtitleBackgroundBrush}" CornerRadius="6" Padding="12,6" HorizontalAlignment="Center">
                <TextBlock Text="{Binding CurrentSubtitleText}"
                           FontSize="{Binding RenderedSubtitleFontSize}"
                           Foreground="{Binding SubtitleColorBrush}"
                           FontFamily="{Binding SubtitleFontFamily}"
                           FontWeight="{Binding SubtitleFontWeight}"
                           TextAlignment="{Binding SubtitleTextAlignment}"
                           TextWrapping="Wrap"
                           MaxWidth="1000">
                    <TextBlock.Effect>
                        <DropShadowEffect BlurRadius="6" ShadowDepth="2" Direction="315" Color="#000000" Opacity="0.95"/>
                    </TextBlock.Effect>
                </TextBlock>
            </Border>
        </Border>

        <!-- ═══════════ 3. BOTTOM BAR (LTR CONTROLS & TIMELINE) ═══════════ -->
        <Border x:Name="BottomControlsBar" VerticalAlignment="Bottom" 
                Background="#EE0A0E17" Padding="20,12"
                CornerRadius="16,16,0,0" Margin="16,0,16,0"
                BorderBrush="#3000ADB5" BorderThickness="1,1,1,0"
                Visibility="{Binding ShowBottomBar, Converter={StaticResource BoolToVis}}">
            <Grid FlowDirection="LeftToRight">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Row 0: Timeline Slider -->
                <Grid Grid.Row="0" Margin="0,0,0,10">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <TextBlock Grid.Column="0" Text="{Binding CurrentTimeFormatted}" 
                               Foreground="#00FFF0" FontSize="13" FontWeight="SemiBold" 
                               VerticalAlignment="Center" Margin="0,0,14,0" FontFamily="Consolas, Segoe UI"/>

                    <Slider Grid.Column="1" Minimum="0.0" Maximum="1.0" 
                            Value="{Binding Progress, Mode=TwoWay}"
                            Style="{StaticResource VideoSeekBarStyle}" 
                            IsMoveToPointEnabled="True"
                            PreviewMouseDown="SeekSlider_PreviewMouseDown"
                            PreviewMouseMove="SeekSlider_PreviewMouseMove"
                            PreviewMouseUp="SeekSlider_PreviewMouseUp"
                            VerticalAlignment="Center" Cursor="Hand"/>

                    <TextBlock Grid.Column="2" Text="{Binding TotalDurationFormatted}" 
                               Foreground="#94A3B8" FontSize="13" FontWeight="SemiBold" 
                               VerticalAlignment="Center" Margin="14,0,0,0" FontFamily="Consolas, Segoe UI"/>
                </Grid>

                <!-- Row 1: Controls Buttons -->
                <Grid Grid.Row="1">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <!-- Left: Volume -->
                    <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button Command="{Binding ToggleMuteCommand}" Style="{StaticResource PlayerIconBtn}" ToolTip="قطع/وصل صدا (M)">
                            <materialDesign:PackIcon Width="24" Height="24">
                                <materialDesign:PackIcon.Style>
                                    <Style TargetType="materialDesign:PackIcon">
                                        <Setter Property="Kind" Value="VolumeHigh"/>
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding IsMuted}" Value="True">
                                                <Setter Property="Kind" Value="VolumeMute"/>
                                                <Setter Property="Foreground" Value="#FF5252"/>
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding Volume}" Value="0">
                                                <Setter Property="Kind" Value="VolumeMute"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </materialDesign:PackIcon.Style>
                            </materialDesign:PackIcon>
                        </Button>

                        <Slider Minimum="0" Maximum="200" Value="{Binding Volume, Mode=TwoWay}" 
                                Width="100" VerticalAlignment="Center" Cursor="Hand" Margin="8,0,6,0"
                                ValueChanged="VolumeSlider_ValueChanged"
                                ToolTip="{Binding Volume, StringFormat='ولوم: {0}%'}"/>
                        
                        <TextBlock Text="{Binding Volume, StringFormat='{}{0}%'}" Foreground="#94A3B8" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Width="38" FontFamily="Consolas, Segoe UI"/>
                    </StackPanel>

                    <!-- Center: Playback Buttons -->
                    <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <Button Command="{Binding PlayPreviousCommand}" Style="{StaticResource PlayerIconBtn}" ToolTip="قسمت/فایل قبلی (PgUp)" Margin="4,0">
                            <materialDesign:PackIcon Kind="SkipPrevious" Width="26" Height="26"/>
                        </Button>

                        <Button Click="SeekBackward5s_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="۵ ثانیه عقب (←)" Margin="4,0">
                            <materialDesign:PackIcon Kind="Rewind5" Width="26" Height="26"/>
                        </Button>

                        <Button Command="{Binding TogglePlayPauseCommand}" Style="{StaticResource BigPlayBtn}" ToolTip="پخش / توقف (Space)" Margin="10,0">
                            <materialDesign:PackIcon Width="32" Height="32">
                                <materialDesign:PackIcon.Style>
                                    <Style TargetType="materialDesign:PackIcon">
                                        <Setter Property="Kind" Value="Play"/>
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding IsPlaying}" Value="True">
                                                <Setter Property="Kind" Value="Pause"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </materialDesign:PackIcon.Style>
                            </materialDesign:PackIcon>
                        </Button>

                        <Button Click="SeekForward5s_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="۵ ثانیه جلو (→)" Margin="4,0">
                            <materialDesign:PackIcon Kind="FastForward5" Width="26" Height="26"/>
                        </Button>

                        <Button Command="{Binding PlayNextCommand}" Style="{StaticResource PlayerIconBtn}" ToolTip="قسمت/فایل بعدی (PgDn)" Margin="4,0">
                            <materialDesign:PackIcon Kind="SkipNext" Width="26" Height="26"/>
                        </Button>
                    </StackPanel>

                    <!-- Right: Extra Menus -->
                    <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button Click="OpenAudioTracksPopup_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="انتخاب صدای دوبله/زبان اصلی (A)" Margin="3,0">
                            <materialDesign:PackIcon Kind="Translate" Width="22" Height="22"/>
                        </Button>

                        <Button Click="OpenSubtitlesPopup_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="زیرنویس‌ها (L)" Margin="3,0">
                            <materialDesign:PackIcon Kind="SubtitlesOutline" Width="22" Height="22"/>
                        </Button>

                        <Button x:Name="SpeedButton" Click="ToggleSpeedMenu_Click" 
                                MouseRightButtonDown="SpeedButton_MouseRightButtonDown" 
                                PreviewMouseWheel="SpeedButton_PreviewMouseWheel"
                                Style="{StaticResource PlayerIconBtn}" 
                                ToolTip="سرعت پخش (کلیک چپ: منو | راست: کاهش | اسکرول: تنظیم | کلیدها: X / C / Z)" 
                                Margin="3,0">
                            <TextBlock Text="{Binding PlaybackSpeed, StringFormat='{}{0:0.0}x'}" FontWeight="Bold" FontSize="12" VerticalAlignment="Center" Foreground="#00ADB5" FontFamily="Consolas, Segoe UI"/>
                        </Button>

                        <Button Click="ToggleAspectRatioMenu_Click" Style="{StaticResource PlayerIconBtn}" ToolTip="نسبت تصویر (J)" Margin="3,0">
                            <materialDesign:PackIcon Kind="AspectRatio" Width="22" Height="22"/>
                        </Button>

                        <Button Command="{Binding ToggleFullscreenCommand}" Style="{StaticResource PlayerIconBtn}" ToolTip="تمام‌صفحه (Enter / F)" Margin="3,0">
                            <materialDesign:PackIcon Width="24" Height="24">
                                <materialDesign:PackIcon.Style>
                                    <Style TargetType="materialDesign:PackIcon">
                                        <Setter Property="Kind" Value="Fullscreen"/>
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding IsFullscreen}" Value="True">
                                                <Setter Property="Kind" Value="FullscreenExit"/>
                                                <Setter Property="Foreground" Value="#00FFF0"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </materialDesign:PackIcon.Style>
                            </materialDesign:PackIcon>
                        </Button>
                    </StackPanel>
                </Grid>
            </Grid>
        </Border>

        <!-- 4. SIDE DRAWER: PLAYLIST (F6) -->
        <Border HorizontalAlignment="Left" VerticalAlignment="Stretch" Width="340"
                Background="#F50D131F" BorderBrush="#3000ADB5" BorderThickness="0,0,1,0"
                Visibility="{Binding ShowPlaylistDrawer, Converter={StaticResource BoolToVis}}"
                FlowDirection="RightToLeft">
            <Grid Margin="16">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <DockPanel Grid.Row="0" Margin="0,0,0,16">
                    <Button DockPanel.Dock="Left" Click="TogglePlaylistDrawer_Click" Style="{StaticResource PlayerIconBtn}">
                        <materialDesign:PackIcon Kind="Close" Width="20" Height="20"/>
                    </Button>
                    <TextBlock Text="لیست پخش / قسمت‌ها (F6)" FontWeight="Bold" FontSize="16" Foreground="#FFFFFF" VerticalAlignment="Center"/>
                </DockPanel>

                <ListBox Grid.Row="1" ItemsSource="{Binding Playlist}" Background="Transparent" BorderThickness="0">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <Border Background="#1A2232" CornerRadius="8" Padding="12,10" Margin="0,0,0,8" Cursor="Hand">
                                <Border.InputBindings>
                                    <MouseBinding MouseAction="LeftClick" 
                                                  Command="{Binding DataContext.PlayPlaylistItemCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                                  CommandParameter="{Binding}"/>
                                </Border.InputBindings>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <materialDesign:PackIcon Grid.Column="0" Kind="PlayCircleOutline" Width="24" Height="24" Foreground="#00ADB5" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                    <StackPanel Grid.Column="1">
                                        <TextBlock Text="{Binding DisplayName}" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="13" TextTrimming="CharacterEllipsis"/>
                                        <TextBlock Text="{Binding DisplayEpisodeInfo}" Foreground="#00FFF0" FontSize="11" Margin="0,3,0,0"/>
                                    </StackPanel>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Grid>
        </Border>

        <!-- 5. SIDE DRAWER: BOOKMARKS (H / P) -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Stretch" Width="320"
                Background="#F50D131F" BorderBrush="#3000ADB5" BorderThickness="1,0,0,0"
                Visibility="{Binding ShowBookmarksDrawer, Converter={StaticResource BoolToVis}}"
                FlowDirection="RightToLeft">
            <Grid Margin="16">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <DockPanel Grid.Row="0" Margin="0,0,0,16">
                    <Button DockPanel.Dock="Left" Click="ToggleBookmarksDrawer_Click" Style="{StaticResource PlayerIconBtn}">
                        <materialDesign:PackIcon Kind="Close" Width="20" Height="20"/>
                    </Button>
                    <TextBlock Text="نشانک‌های فیلم (H)" FontWeight="Bold" FontSize="16" Foreground="#FFFFFF" VerticalAlignment="Center"/>
                </DockPanel>

                <ListBox Grid.Row="1" ItemsSource="{Binding Bookmarks}" Background="Transparent" BorderThickness="0">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <Border Background="#1A2232" CornerRadius="8" Padding="12,10" Margin="0,0,0,8" Cursor="Hand">
                                <Border.InputBindings>
                                    <MouseBinding MouseAction="LeftClick" 
                                                  Command="{Binding DataContext.SeekToBookmarkCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                                  CommandParameter="{Binding}"/>
                                </Border.InputBindings>
                                <DockPanel>
                                    <materialDesign:PackIcon DockPanel.Dock="Left" Kind="BookmarkCheck" Width="20" Height="20" Foreground="#00FFF0" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                    <TextBlock Text="{Binding TimeFormatted}" Foreground="#00ADB5" FontWeight="Bold" DockPanel.Dock="Right" VerticalAlignment="Center"/>
                                    <TextBlock Text="{Binding Title}" Foreground="#FFFFFF" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                                </DockPanel>
                            </Border>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>

                <Button Grid.Row="2" Style="{StaticResource PlayerIconBtn}" Background="#00ADB5" Foreground="#05080E" Padding="12,8" Margin="0,10,0,0"
                        Click="AddBookmark_Click">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="BookmarkPlus" Width="20" Height="20" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="افزودن نشانک در این لحظه (P)" FontWeight="Bold"/>
                    </StackPanel>
                </Button>
            </Grid>
        </Border>

        <!-- 6. SHORTCUTS HELP MODAL (F1) -->
        <Grid Background="#B0000000" Visibility="{Binding ShowShortcutsHelp, Converter={StaticResource BoolToVis}}">
            <Border Background="#161B26" BorderBrush="#00ADB5" BorderThickness="1" 
                    CornerRadius="14" Width="720" MaxHeight="600" 
                    HorizontalAlignment="Center" VerticalAlignment="Center" Padding="24"
                    FlowDirection="RightToLeft">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <DockPanel Grid.Row="0" Margin="0,0,0,16">
                        <Button DockPanel.Dock="Left" Click="ToggleShortcutsHelp_Click" Style="{StaticResource PlayerIconBtn}">
                            <materialDesign:PackIcon Kind="Close" Width="22" Height="22"/>
                        </Button>
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="KeyboardOutline" Width="26" Height="26" Foreground="#00FFF0" VerticalAlignment="Center" Margin="0,0,8,0"/>
                            <TextBlock Text="راهنمای کامل کلیدهای میانبر (PotPlayer Shortcuts)" FontWeight="Bold" FontSize="17" Foreground="#FFFFFF" VerticalAlignment="Center"/>
                        </StackPanel>
                    </DockPanel>

                    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                        <Border Background="#0F141D" CornerRadius="8" BorderBrush="#20FFFFFF" BorderThickness="1" Padding="6">
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="220"/>
                                </Grid.ColumnDefinitions>

                                <Border Grid.Row="0" Grid.ColumnSpan="2" Background="#1E2736" Padding="12,8" CornerRadius="4">
                                    <Grid>
                                        <TextBlock Text="عملکرد" Foreground="#00FFF0" FontWeight="Bold" FontSize="14"/>
                                        <TextBlock Text="کلید" Foreground="#00FFF0" FontWeight="Bold" FontSize="14" HorizontalAlignment="Right" Margin="0,0,16,0"/>
                                    </Grid>
                                </Border>

                                <TextBlock Grid.Row="1" Grid.Column="0" Text="پخش / توقف (Play / Pause)" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="1" Grid.Column="1" Text="Space / دابل‌کلیک چپ" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="2" Grid.Column="0" Text="تمام‌صفحه (Fullscreen)" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="2" Grid.Column="1" Text="Enter / F" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="3" Grid.Column="0" Text="پرش ۵ ثانیه (با Ctrl: ۳۰ ثانیه، با Shift: ۱ دقیقه)" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="3" Grid.Column="1" Text="← / →" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="4" Grid.Column="0" Text="ولوم بالا / پایین" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="4" Grid.Column="1" Text="↑ / ↓ یا اسکرول ماوس" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="5" Grid.Column="0" Text="فایل قبلی / بعدی" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="5" Grid.Column="1" Text="PgUp / PgDn" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="6" Grid.Column="0" Text="کاهش / افزایش سرعت پخش" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="6" Grid.Column="1" Text="X / C / Z" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="7" Grid.Column="0" Text="فریم قبلی / بعدی (Frame Stepping)" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="7" Grid.Column="1" Text="D / E" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="8" Grid.Column="0" Text="سینک زیرنویس ۰.۵∓ ثانیه و ریست" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="8" Grid.Column="1" Text="&lt; / &gt; و /" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="9" Grid.Column="0" Text="سینک صدا ۰.۰۵ ثانیه" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="9" Grid.Column="1" Text="Shift+&lt; / &gt;" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="10" Grid.Column="0" Text="تنظیم زمان و روشن/خاموش کردن تکرار A-B" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="10" Grid.Column="1" Text="[ / ] و \" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="11" Grid.Column="0" Text="روشنایی/کنتراست/اشباع/رنگ ±۱٪ و ریست تصویر" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="11" Grid.Column="1" Text="W/E, R/T, Y/U, I/O و Q" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="12" Grid.Column="0" Text="پیش‌تنظیم‌های اندازه پنجره (0.5x تا حداکثر و اصلی)" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="12" Grid.Column="1" Text="1 تا 7 و 9" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="13" Grid.Column="0" Text="افزودن بوک‌مارک / مشاهده بوک‌مارک‌ها" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="13" Grid.Column="1" Text="P / H" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="14" Grid.Column="0" Text="انتخاب استریم صدا / انتخاب زیرنویس / نسبت تصویر / اسنپ‌شات" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="14" Grid.Column="1" Text="A / L / J / K" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="15" Grid.Column="0" Text="تصویر آینه‌ای / فلیپ تصویر" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="15" Grid.Column="1" Text="Ctrl+Z / Ctrl+V" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="16" Grid.Column="0" Text="نرمال‌سازی صدا / حذف نویز" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="16" Grid.Column="1" Text="Shift+N / Shift+D" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="17" Grid.Column="0" Text="ضبط پیوسته فریم / ضبط صدا" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="17" Grid.Column="1" Text="Ctrl+G / Shift+G" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>

                                <TextBlock Grid.Row="18" Grid.Column="0" Text="راهنما / پوشه / بازکردن / بستن / تنظیمات / پلی‌لیست / اکولایزر" Foreground="#EEEEEE" Padding="10,8"/>
                                <TextBlock Grid.Row="18" Grid.Column="1" Text="F1 تا F7, F12" Foreground="#00ADB5" FontWeight="Bold" Padding="10,8" HorizontalAlignment="Right"/>
                            </Grid>
                        </Border>
                    </ScrollViewer>
                </Grid>
            </Border>
        </Grid>

        <!-- 7. POPUP: AUDIO TRACKS (A) -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,130,90"
                Background="#F5121824" BorderBrush="#00ADB5" BorderThickness="1" CornerRadius="10" Padding="14" Width="280"
                Visibility="{Binding ShowAudioTracksPopup, Converter={StaticResource BoolToVis}}"
                FlowDirection="RightToLeft">
            <StackPanel>
                <DockPanel Margin="0,0,0,10">
                    <Button DockPanel.Dock="Left" Click="CloseAudioTracksPopup_Click" Style="{StaticResource PlayerIconBtn}">
                        <materialDesign:PackIcon Kind="Close" Width="16" Height="16"/>
                    </Button>
                    <TextBlock Text="انتخاب صدای فیلم (دوبله/زبان)" FontWeight="Bold" Foreground="#00FFF0" FontSize="13" VerticalAlignment="Center"/>
                </DockPanel>
                <ItemsControl ItemsSource="{Binding AudioTracks}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Button Style="{StaticResource PlayerIconBtn}" HorizontalContentAlignment="Stretch" Margin="0,0,0,4"
                                    Command="{Binding DataContext.SelectAudioTrackCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}">
                                <DockPanel>
                                    <materialDesign:PackIcon DockPanel.Dock="Left" Kind="Check" Width="18" Height="18" Foreground="#00FFF0"
                                                             Visibility="{Binding IsSelected, Converter={StaticResource BoolToVis}}" Margin="0,0,6,0"/>
                                    <TextBlock Text="{Binding Name}" Foreground="#FFFFFF" FontSize="12" TextTrimming="CharacterEllipsis"/>
                                </DockPanel>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <Separator Background="#25FFFFFF" Margin="0,8,0,8"/>

                <TextBlock Text="همگام‌سازی صدا (Audio Delay)" Foreground="#94A3B8" FontSize="11" FontWeight="Bold" Margin="0,0,0,6"/>
                <Grid Margin="0,0,0,4">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Button Grid.Column="0" Command="{Binding AdjustAudioDelayCommand}" CommandParameter="-50" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="0,0,2,0" Padding="4,4">
                        <TextBlock Text="-50ms" FontSize="11" Foreground="#EEEEEE"/>
                    </Button>
                    <Button Grid.Column="1" Command="{Binding ResetAudioDelayCommand}" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2,0" Padding="4,4" ToolTip="کلیک برای ریست">
                        <TextBlock Text="{Binding AudioDelayMilliseconds, StringFormat='{}{0}ms'}" FontSize="11" FontWeight="Bold" Foreground="#00FFF0"/>
                    </Button>
                    <Button Grid.Column="2" Command="{Binding AdjustAudioDelayCommand}" CommandParameter="50" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2,0,0,0" Padding="4,4">
                        <TextBlock Text="+50ms" FontSize="11" Foreground="#EEEEEE"/>
                    </Button>
                </Grid>
            </StackPanel>
        </Border>

        <!-- 8. POPUP: SUBTITLES (L) -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,90,90"
                Background="#F5121824" BorderBrush="#00ADB5" BorderThickness="1" CornerRadius="10" Padding="14" Width="310"
                Visibility="{Binding ShowSubtitlesPopup, Converter={StaticResource BoolToVis}}"
                FlowDirection="RightToLeft" Panel.ZIndex="20">
            <StackPanel>
                <DockPanel Margin="0,0,0,10">
                    <Button DockPanel.Dock="Left" Click="CloseSubtitlesPopup_Click" Style="{StaticResource PlayerIconBtn}">
                        <materialDesign:PackIcon Kind="Close" Width="16" Height="16"/>
                    </Button>
                    <TextBlock Text="تنظیمات و انتخاب زیرنویس" FontWeight="Bold" Foreground="#00FFF0" FontSize="13" VerticalAlignment="Center"/>
                </DockPanel>

                <ItemsControl ItemsSource="{Binding SubtitleTracks}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Button Style="{StaticResource PlayerIconBtn}" HorizontalContentAlignment="Stretch" Margin="0,0,0,4"
                                    Command="{Binding DataContext.SelectSubtitleTrackCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}">
                                <DockPanel>
                                    <materialDesign:PackIcon DockPanel.Dock="Left" Kind="Check" Width="18" Height="18" Foreground="#00FFF0"
                                                             Visibility="{Binding IsSelected, Converter={StaticResource BoolToVis}}" Margin="0,0,6,0"/>
                                    <TextBlock Text="{Binding Name}" Foreground="#FFFFFF" FontSize="12" TextTrimming="CharacterEllipsis"/>
                                </DockPanel>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <Separator Background="#25FFFFFF" Margin="0,6,0,8"/>

                <TextBlock Text="همگام‌سازی و تاخیر زیرنویس" Foreground="#94A3B8" FontSize="11" FontWeight="Bold" Margin="0,0,0,6"/>
                <Grid Margin="0,0,0,8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Button Grid.Column="0" Command="{Binding AdjustSubtitleDelayCommand}" CommandParameter="-0.5" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="0,0,2,0" Padding="4,4">
                        <TextBlock Text="-0.5s" FontSize="11" Foreground="#EEEEEE"/>
                    </Button>
                    <Button Grid.Column="1" Command="{Binding ResetSubtitleDelayCommand}" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2,0" Padding="4,4" ToolTip="کلیک برای ریست">
                        <TextBlock Text="{Binding SubtitleDelaySeconds, StringFormat='{}{0:F1}s'}" FontSize="11" FontWeight="Bold" Foreground="#00FFF0"/>
                    </Button>
                    <Button Grid.Column="2" Command="{Binding AdjustSubtitleDelayCommand}" CommandParameter="0.5" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2,0,0,0" Padding="4,4">
                        <TextBlock Text="+0.5s" FontSize="11" Foreground="#EEEEEE"/>
                    </Button>
                </Grid>

                <!-- Download Online Subtitles Button -->
                <Button Command="{Binding OpenOnlineSubtitleModalCommand}" Style="{StaticResource PlayerIconBtn}" Background="#E50914" Foreground="#FFFFFF" HorizontalAlignment="Stretch" Margin="0,0,0,6" Padding="8,7">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="DownloadOutline" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="📥 دانلود آنلاین زیرنویس (SubDL)..." FontSize="12" FontWeight="Bold"/>
                    </StackPanel>
                </Button>

                <!-- Open Subtitle Studio Button -->
                <Button Command="{Binding ToggleSubtitleStudioCommand}" Style="{StaticResource PlayerIconBtn}" Background="#00ADB5" Foreground="#05080E" HorizontalAlignment="Stretch" Margin="0,0,0,6" Padding="8,7">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="PaletteOutline" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="🎨 شخصی‌سازی ظاهر زیرنویس (Studio)..." FontSize="12" FontWeight="Bold"/>
                    </StackPanel>
                </Button>

                <!-- AI Translation & Encoding Fix -->
                <Button Command="{Binding TranslateSubtitleCommand}" Background="#1E2736" HorizontalAlignment="Stretch" Margin="0,0,0,4" Padding="8,6">
                    <Button.Style>
                        <Style TargetType="Button" BasedOn="{StaticResource PlayerIconBtn}">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsTranslatingSubtitle}" Value="True">
                                    <Setter Property="IsEnabled" Value="False"/>
                                    <Setter Property="Opacity" Value="0.6"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Button.Style>
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="Translate" Width="16" Height="16" Foreground="#00FFF0" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock FontSize="11" FontWeight="SemiBold" Foreground="#FFFFFF">
                            <TextBlock.Style>
                                <Style TargetType="TextBlock">
                                    <Setter Property="Text" Value="🌐 ترجمه زیرنویس به فارسی (هوش مصنوعی)"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsTranslatingSubtitle}" Value="True">
                                            <Setter Property="Text" Value="⏳ در حال ترجمه زیرنویس..."/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>
                    </StackPanel>
                </Button>

                <Button Command="{Binding FixSubtitleEncodingCommand}" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" HorizontalAlignment="Stretch" Margin="0,0,0,4" Padding="8,6">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="AutoFix" Width="16" Height="16" Foreground="#00ADB5" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="✨ اصلاح انکودینگ حروف ناخوانای فارسی" FontSize="11" Foreground="#FFFFFF"/>
                    </StackPanel>
                </Button>

                <Button Command="{Binding LoadExternalSubtitleCommand}" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" HorizontalAlignment="Stretch" Margin="0,0,0,2" Padding="8,6">
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <materialDesign:PackIcon Kind="FolderOpenOutline" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="📂 افزودن فایل زیرنویس از سیستم..." FontSize="11" Foreground="#FFFFFF"/>
                    </StackPanel>
                </Button>
            </StackPanel>
        </Border>

        <!-- 8.5 MODAL: SUBTITLE STUDIO (COMPACT & DRAGGABLE) -->
        <Grid Visibility="{Binding ShowSubtitleStudioModal, Converter={StaticResource BoolToVis}}" Panel.ZIndex="30">
            <Border x:Name="SubtitleStudioCard"
                    Background="#F0111622" BorderBrush="#00ADB5" BorderThickness="1.5" 
                    CornerRadius="14" Width="380" MaxHeight="480" 
                    HorizontalAlignment="Center" VerticalAlignment="Center" Padding="14,12"
                    FlowDirection="RightToLeft"
                    MouseLeftButtonDown="StudioCard_MouseLeftButtonDown"
                    MouseMove="StudioCard_MouseMove"
                    MouseLeftButtonUp="StudioCard_MouseLeftButtonUp">
                <Border.Effect>
                    <DropShadowEffect BlurRadius="25" ShadowDepth="4" Direction="270" Color="#000000" Opacity="0.8"/>
                </Border.Effect>
                <Border.RenderTransform>
                    <TranslateTransform x:Name="StudioTranslateTransform" X="0" Y="0"/>
                </Border.RenderTransform>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <Border Grid.Row="0" Background="#1B2433" CornerRadius="8" Padding="10,6" Margin="0,0,0,10"
                            Cursor="SizeAll">
                        <DockPanel>
                            <Button DockPanel.Dock="Left" Command="{Binding ToggleSubtitleStudioCommand}" Style="{StaticResource PlayerIconBtn}" Height="26" Width="26">
                                <materialDesign:PackIcon Kind="Close" Width="18" Height="18" Foreground="#FF5252"/>
                            </Button>
                            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                <materialDesign:PackIcon Kind="DragVertical" Width="18" Height="18" Foreground="#64748B" Margin="0,0,4,0"/>
                                <materialDesign:PackIcon Kind="PaletteAdvanced" Width="18" Height="18" Foreground="#00FFF0" Margin="0,0,6,0"/>
                                <TextBlock Text="استودیوی زیرنویس" FontWeight="Bold" FontSize="13" Foreground="#FFFFFF"/>
                            </StackPanel>
                        </DockPanel>
                    </Border>

                    <ScrollViewer x:Name="StudioScrollViewer" Grid.Row="1" VerticalScrollBarVisibility="Auto" PreviewMouseWheel="StudioScrollViewer_PreviewMouseWheel">
                        <StackPanel Margin="0,0,4,0">
                            <!-- Live Preview Box -->
                            <Border Background="#090D14" BorderBrush="#2500ADB5" BorderThickness="1" CornerRadius="8" Padding="10,8" Margin="0,0,0,10">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="پیش‌نمایش زنده:" Foreground="#64748B" FontSize="10" Margin="0,0,0,4" HorizontalAlignment="Center"/>
                                    <Border Background="{Binding SubtitleBackgroundBrush}" CornerRadius="4" Padding="8,4" HorizontalAlignment="Center">
                                        <TextBlock Text="پیش‌نمایش زیرنویس فارسی (Live)"
                                                   FontSize="{Binding RenderedSubtitleFontSize}"
                                                   Foreground="{Binding SubtitleColorBrush}"
                                                   FontFamily="{Binding SubtitleFontFamily}"
                                                   FontWeight="{Binding SubtitleFontWeight}"
                                                   TextAlignment="{Binding SubtitleTextAlignment}">
                                            <TextBlock.Effect>
                                                <DropShadowEffect BlurRadius="4" ShadowDepth="2" Direction="315" Color="#000000" Opacity="0.9"/>
                                            </TextBlock.Effect>
                                        </TextBlock>
                                    </Border>
                                </StackPanel>
                            </Border>

                            <!-- 1. Font Size -->
                            <DockPanel Margin="0,0,0,4">
                                <TextBlock Text="اندازه فونت:" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="12" VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding SubtitleFontSize, StringFormat='{}{0}px'}" Foreground="#00FFF0" FontWeight="Bold" FontSize="12" DockPanel.Dock="Left" VerticalAlignment="Center"/>
                            </DockPanel>
                            <Grid Margin="0,0,0,8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <Button Grid.Column="0" Command="{Binding ChangeSubtitleFontSizeCommand}" CommandParameter="-2" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Width="26" Height="26" ToolTip="کاهش سایز">
                                    <materialDesign:PackIcon Kind="Minus" Width="14" Height="14"/>
                                </Button>
                                <Slider Grid.Column="1" Minimum="16" Maximum="56" Value="{Binding SubtitleFontSize, Mode=TwoWay}" 
                                        Style="{StaticResource VideoSeekBarStyle}" VerticalAlignment="Center" Margin="6,0"/>
                                <Button Grid.Column="2" Command="{Binding ChangeSubtitleFontSizeCommand}" CommandParameter="2" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Width="26" Height="26" ToolTip="افزایش سایز">
                                    <materialDesign:PackIcon Kind="Plus" Width="14" Height="14"/>
                                </Button>
                            </Grid>

                            <!-- 2. Text Color -->
                            <TextBlock Text="رنگ متن زیرنویس:" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="12" Margin="0,0,0,4"/>
                            <UniformGrid Columns="8" Margin="0,0,0,8">
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#FFFFFF" Style="{StaticResource PlayerIconBtn}" Background="#FFFFFF" Height="24" Margin="2" ToolTip="سفید"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#FFE600" Style="{StaticResource PlayerIconBtn}" Background="#FFE600" Height="24" Margin="2" ToolTip="زرد"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#00FFF0" Style="{StaticResource PlayerIconBtn}" Background="#00FFF0" Height="24" Margin="2" ToolTip="فیروزه‌ای"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#00FF66" Style="{StaticResource PlayerIconBtn}" Background="#00FF66" Height="24" Margin="2" ToolTip="سبز فسفری"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#FF9100" Style="{StaticResource PlayerIconBtn}" Background="#FF9100" Height="24" Margin="2" ToolTip="نارنجی"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#FF4081" Style="{StaticResource PlayerIconBtn}" Background="#FF4081" Height="24" Margin="2" ToolTip="صورتی"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#FF5252" Style="{StaticResource PlayerIconBtn}" Background="#FF5252" Height="24" Margin="2" ToolTip="قرمز"/>
                                <Button Command="{Binding SetSubtitleColorCommand}" CommandParameter="#448AFF" Style="{StaticResource PlayerIconBtn}" Background="#448AFF" Height="24" Margin="2" ToolTip="آبی"/>
                            </UniformGrid>

                            <!-- 3. Bold & Alignment -->
                            <Grid Margin="0,0,0,8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Button Grid.Column="0" Command="{Binding ToggleSubtitleBoldCommand}" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="0,0,4,0" Height="28" Padding="6,2">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="FormatBold" Width="16" Height="16" Foreground="#00FFF0" Margin="0,0,4,0"/>
                                        <TextBlock Text="{Binding SubtitleBoldStatusText}" FontSize="11" Foreground="#FFFFFF" VerticalAlignment="Center"/>
                                    </StackPanel>
                                </Button>
                                <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right">
                                    <Button Command="{Binding SetSubtitleAlignmentCommand}" CommandParameter="Right" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Width="28" Height="28" Margin="2,0" ToolTip="راست‌چین">
                                        <materialDesign:PackIcon Kind="FormatAlignRight" Width="14" Height="14"/>
                                    </Button>
                                    <Button Command="{Binding SetSubtitleAlignmentCommand}" CommandParameter="Center" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Width="28" Height="28" Margin="2,0" ToolTip="وسط‌چین">
                                        <materialDesign:PackIcon Kind="FormatAlignCenter" Width="14" Height="14"/>
                                    </Button>
                                    <Button Command="{Binding SetSubtitleAlignmentCommand}" CommandParameter="Left" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Width="28" Height="28" Margin="2,0" ToolTip="چپ‌چین">
                                        <materialDesign:PackIcon Kind="FormatAlignLeft" Width="14" Height="14"/>
                                    </Button>
                                </StackPanel>
                            </Grid>

                            <!-- 4. Background Box & Opacity -->
                            <Border Background="#161D2A" CornerRadius="8" Padding="8" Margin="0,0,0,8">
                                <StackPanel>
                                    <DockPanel Margin="0,0,0,4">
                                        <TextBlock Text="باکس پس‌زمینه:" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="11" VerticalAlignment="Center"/>
                                        <Button Command="{Binding ToggleSubtitleBackgroundCommand}" Style="{StaticResource PlayerIconBtn}" Background="#222C3D" Height="22" Padding="6,1" DockPanel.Dock="Left">
                                            <TextBlock Text="{Binding SubtitleBackgroundStatusText}" FontSize="10" Foreground="#00FFF0"/>
                                        </Button>
                                    </DockPanel>
                                    <Grid Margin="0,2,0,0">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Grid.Column="0" Text="شفافیت:" Foreground="#94A3B8" FontSize="10" VerticalAlignment="Center"/>
                                        <Slider Grid.Column="1" Minimum="10" Maximum="100" Value="{Binding SubtitleBgOpacityPercent, Mode=TwoWay}" 
                                                Style="{StaticResource VideoSeekBarStyle}" VerticalAlignment="Center" Margin="6,0"/>
                                        <TextBlock Grid.Column="2" Text="{Binding SubtitleBgOpacityPercent, StringFormat='{}{0}%'}" Foreground="#00FFF0" FontSize="10" VerticalAlignment="Center"/>
                                    </Grid>
                                </StackPanel>
                            </Border>

                            <!-- 5. Vertical Position -->
                            <DockPanel Margin="0,0,0,2">
                                <TextBlock Text="موقعیت ارتفاع از پایین:" Foreground="#FFFFFF" FontWeight="SemiBold" FontSize="11" VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding SubtitleBottomMargin, StringFormat='{}{0}px'}" Foreground="#00FFF0" FontWeight="Bold" FontSize="11" DockPanel.Dock="Left" VerticalAlignment="Center"/>
                            </DockPanel>
                            <Slider Minimum="15" Maximum="200" Value="{Binding SubtitleBottomMargin, Mode=TwoWay}" 
                                    Style="{StaticResource VideoSeekBarStyle}" VerticalAlignment="Center" Margin="0,0,0,8"/>
                        </StackPanel>
                    </ScrollViewer>
                </Grid>
            </Border>
        </Grid>

        <!-- 9. POPUP: SPEED (X / C / Z) -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,50,90"
                Background="#F5121824" BorderBrush="#00ADB5" BorderThickness="1" CornerRadius="10" Padding="14" Width="280"
                Visibility="{Binding ShowSpeedPopup, Converter={StaticResource BoolToVis}}"
                FlowDirection="RightToLeft" Panel.ZIndex="20">
            <StackPanel>
                <DockPanel Margin="0,0,0,10">
                    <Button DockPanel.Dock="Left" Click="CloseSpeedPopup_Click" Style="{StaticResource PlayerIconBtn}">
                        <materialDesign:PackIcon Kind="Close" Width="16" Height="16"/>
                    </Button>
                    <TextBlock Text="تنظیم سرعت پخش" FontWeight="Bold" Foreground="#00FFF0" FontSize="13" VerticalAlignment="Center"/>
                </DockPanel>

                <Border Background="#1A2232" CornerRadius="8" Padding="10,8" Margin="0,0,0,10">
                    <Grid FlowDirection="LeftToRight">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>

                        <Button Grid.Column="0" Command="{Binding DecreaseSpeedCommand}" Style="{StaticResource PlayerIconBtn}" 
                                ToolTip="کاهش سرعت ۰.۱x (X)" Padding="8,4" Background="#283446">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="Minus" Width="16" Height="16" Foreground="#FF5252" VerticalAlignment="Center"/>
                                <TextBlock Text="0.1x" FontSize="11" Foreground="#EEEEEE" Margin="2,0,0,0"/>
                            </StackPanel>
                        </Button>

                        <Button Grid.Column="1" Command="{Binding ResetSpeedCommand}" Style="{StaticResource PlayerIconBtn}" 
                                ToolTip="ریست به سرعت عادی ۱.۰x (Z)" Margin="6,0">
                            <StackPanel HorizontalAlignment="Center">
                                <TextBlock Text="{Binding PlaybackSpeed, StringFormat='{}{0:0.00}x'}" FontWeight="Bold" FontSize="15" Foreground="#00FFF0" HorizontalAlignment="Center"/>
                                <TextBlock Text="عادی (۱.۰x)" FontSize="10" Foreground="#94A3B8" HorizontalAlignment="Center"/>
                            </StackPanel>
                        </Button>

                        <Button Grid.Column="2" Command="{Binding IncreaseSpeedCommand}" Style="{StaticResource PlayerIconBtn}" 
                                ToolTip="افزایش سرعت ۰.۱x (C)" Padding="8,4" Background="#283446">
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="0.1x" FontSize="11" Foreground="#EEEEEE" Margin="0,0,2,0"/>
                                <materialDesign:PackIcon Kind="Plus" Width="16" Height="16" Foreground="#00E676" VerticalAlignment="Center"/>
                            </StackPanel>
                        </Button>
                    </Grid>
                </Border>

                <TextBlock Text="سرعت‌های آماده:" Foreground="#94A3B8" FontSize="11" Margin="0,0,0,6"/>

                <UniformGrid Columns="4" Margin="0,0,0,2">
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="0.5" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="0.5x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="0.75" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="0.75x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="1.0" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="1.0x" FontSize="11" FontWeight="Bold" Foreground="#00FFF0"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="1.25" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="1.25x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="1.5" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="1.5x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="1.75" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="1.75x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="2.0" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="2.0x" FontSize="11"/>
                    </Button>
                    <Button Command="{Binding SetSpeedCommand}" CommandParameter="3.0" Style="{StaticResource PlayerIconBtn}" Background="#1E2736" Margin="2" Padding="4">
                        <TextBlock Text="3.0x" FontSize="11"/>
                    </Button>
                </UniformGrid>
            </StackPanel>
        </Border>

        <!-- RESIZE GRIP -->
        <Border HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,3,3" IsHitTestVisible="False">
            <materialDesign:PackIcon Kind="ResizeBottomRight" Width="14" Height="14" Foreground="#5000ADB5"/>
        </Border>
    </Grid>
</Window>
```

---

# بخش ۵: `PlayerOverlayWindow.xaml.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Views\PlayerOverlayWindow.xaml.cs`

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class PlayerOverlayWindow : Window
    {
        private PlayerViewModel? ViewModel => DataContext as PlayerViewModel;
        private Window? _parentPlayerWindow;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public PlayerOverlayWindow(PlayerViewModel viewModel, Window parentWindow)
        {
            InitializeComponent();
            DataContext = viewModel;
            _parentPlayerWindow = parentWindow;
            Owner = parentWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;
            ViewModel.HandleKeyDown(e.Key, Keyboard.Modifiers);
            e.Handled = true;
        }

        private int GetHitTestDirection(Point pt)
        {
            if (ViewModel?.IsFullscreen == true || _parentPlayerWindow == null || _parentPlayerWindow.WindowState != WindowState.Normal)
                return 0;

            double edge = 10.0;
            bool left = pt.X <= edge;
            bool right = pt.X >= ActualWidth - edge;
            bool top = pt.Y <= edge;
            bool bottom = pt.Y >= ActualHeight - edge;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;
            return 0;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;
            Point pos = e.GetPosition(this);

            int ht = GetHitTestDirection(pos);
            switch (ht)
            {
                case HTLEFT:
                case HTRIGHT:
                    Cursor = Cursors.SizeWE;
                    break;
                case HTTOP:
                case HTBOTTOM:
                    Cursor = Cursors.SizeNS;
                    break;
                case HTTOPLEFT:
                case HTBOTTOMRIGHT:
                    Cursor = Cursors.SizeNWSE;
                    break;
                case HTTOPRIGHT:
                case HTBOTTOMLEFT:
                    Cursor = Cursors.SizeNESW;
                    break;
                default:
                    Cursor = Cursors.Arrow;
                    break;
            }

            ViewModel.HandleMouseMoveZone(pos.Y, ActualHeight, pos.X, ActualWidth);
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            if (e.OriginalSource is DependencyObject dep)
            {
                var scrollViewer = FindParent<ScrollViewer>(dep);
                var listBox = FindParent<ListBox>(dep);
                if (scrollViewer != null || listBox != null)
                {
                    return;
                }
            }

            if (e.Delta > 0)
            {
                ViewModel.AdjustVolume(5);
            }
            else if (e.Delta < 0)
            {
                ViewModel.AdjustVolume(-5);
            }

            ViewModel.EnforceDisableInternalSubtitles();
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;

            // 1. Mouse Side Buttons
            if (e.ChangedButton == MouseButton.XButton1)
            {
                ViewModel.SeekRelative(-5);
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.XButton2)
            {
                ViewModel.SeekRelative(5);
                e.Handled = true;
                return;
            }

            // 2. Middle click
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                ViewModel.ToggleFullscreen();
                e.Handled = true;
                return;
            }

            // 3. Left Click
            if (e.ChangedButton == MouseButton.Left)
            {
                Point pt = e.GetPosition(this);

                // A. Subtitle Studio Modal Click
                if (ViewModel.ShowSubtitleStudioModal && e.OriginalSource is DependencyObject depStudio)
                {
                    if (FindParentByName(depStudio, "SubtitleStudioCard") != null)
                    {
                        return;
                    }
                }

                // B. Resize Borders
                if (_parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && !ViewModel.IsFullscreen)
                {
                    int ht = GetHitTestDirection(pt);
                    if (ht != 0)
                    {
                        var helper = new WindowInteropHelper(_parentPlayerWindow);
                        if (helper.Handle != IntPtr.Zero)
                        {
                            ReleaseCapture();
                            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)ht, IntPtr.Zero);
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // C. Child Controls
                if (e.OriginalSource is DependencyObject dep)
                {
                    var parentButton = FindParent<Button>(dep);
                    var parentSlider = FindParent<Slider>(dep);
                    var parentListBox = FindParent<ListBox>(dep);
                    var parentTextBox = FindParent<TextBox>(dep);

                    if (parentButton != null || parentSlider != null || parentListBox != null || parentTextBox != null)
                    {
                        return;
                    }

                    var parentBorder = FindParent<Border>(dep);
                    if (parentBorder != null)
                    {
                        string? borderName = parentBorder.Name;
                        if (borderName == "BottomControlsBar" || borderName == "TopControlsBar")
                        {
                            return;
                        }
                    }
                }

                // D. Dismiss Popups
                if (ViewModel.HasOpenFlyout && !ViewModel.ShowSubtitleStudioModal)
                {
                    ViewModel.CloseAllPopups();
                    e.Handled = true;
                    return;
                }

                // E. Double-click = Play/Pause
                if (e.ClickCount == 2)
                {
                    ViewModel.TogglePlayPause();
                    e.Handled = true;
                    return;
                }

                // F. Single-click = Drag Window
                if (e.ClickCount == 1 && _parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && !ViewModel.IsFullscreen)
                {
                    try
                    {
                        _parentPlayerWindow.DragMove();
                    }
                    catch { }
                }
            }
        }

        private void TopControlsBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var parentButton = FindParent<Button>(dep);
                if (parentButton != null) return;
            }

            if (e.ClickCount == 2)
            {
                ViewModel?.ToggleFullscreen();
            }
            else if (e.LeftButton == MouseButtonState.Pressed && _parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && ViewModel?.IsFullscreen != true)
            {
                try
                {
                    _parentPlayerWindow.DragMove();
                }
                catch { }
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPlayerWindow != null)
            {
                _parentPlayerWindow.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ToggleFullscreen();
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null && e.LeftButton == MouseButtonState.Pressed)
            {
                slider.CaptureMouse();
                ViewModel.StartSeek();
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                e.Handled = true;
            }
        }

        private void SeekSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null && slider.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                e.Handled = true;
            }
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null)
            {
                if (slider.IsMouseCaptured)
                {
                    slider.ReleaseMouseCapture();
                }
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                ViewModel.EndSeek();
                e.Handled = true;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ViewModel?.HandleMouseMove();
        }

        private void SeekBackward5s_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SeekRelative(-5);
        }

        private void SeekForward5s_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SeekRelative(5);
        }

        private void TogglePlaylistDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowPlaylistDrawer = !ViewModel.ShowPlaylistDrawer;
            }
        }

        private void ToggleBookmarksDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowBookmarksDrawer = !ViewModel.ShowBookmarksDrawer;
            }
        }

        private void ToggleShortcutsHelp_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowShortcutsHelp = !ViewModel.ShowShortcutsHelp;
            }
        }

        private void OpenAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateAudioTracksList();
                ViewModel.ShowAudioTracksPopup = !ViewModel.ShowAudioTracksPopup;
            }
        }

        private void CloseAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowAudioTracksPopup = false;
            }
        }

        private void OpenSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateSubtitleTracksList();
                ViewModel.ShowSubtitlesPopup = !ViewModel.ShowSubtitlesPopup;
            }
        }

        private void CloseSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSubtitlesPopup = false;
            }
        }

        private void ToggleSpeedMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSpeedPopup = !ViewModel.ShowSpeedPopup;
            }
        }

        private void CloseSpeedPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSpeedPopup = false;
            }
        }

        private void SpeedButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.AdjustSpeed(-0.1f);
            e.Handled = true;
        }

        private void SpeedButton_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ViewModel?.AdjustSpeed(0.1f);
            }
            else if (e.Delta < 0)
            {
                ViewModel?.AdjustSpeed(-0.1f);
            }
            e.Handled = true;
        }

        private void ToggleAspectRatioMenu_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CycleAspectRatio();
        }

        private void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddBookmark();
        }

        private Point _studioDragStart;
        private bool _isDraggingStudio = false;

        private void StudioCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    var btn = FindParent<Button>(dep);
                    var slider = FindParent<Slider>(dep);
                    var thumb = FindParent<System.Windows.Controls.Primitives.Thumb>(dep);
                    var txt = FindParent<TextBox>(dep);
                    if (btn != null || slider != null || thumb != null || txt != null)
                    {
                        return;
                    }
                }

                _isDraggingStudio = true;
                _studioDragStart = e.GetPosition(this);
                SubtitleStudioCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void StudioCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingStudio && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _studioDragStart.X;
                double deltaY = currentPoint.Y - _studioDragStart.Y;

                StudioTranslateTransform.X += deltaX;
                StudioTranslateTransform.Y += deltaY;

                _studioDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void StudioCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStudio)
            {
                _isDraggingStudio = false;
                SubtitleStudioCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void StudioScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.5));
                e.Handled = true;
            }
        }

        private static FrameworkElement? FindParentByName(DependencyObject child, string name)
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.Name == name)
                {
                    return fe;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
```

---

# بخش ۶: `EmbeddedSubtitleExtractorService.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\EmbeddedSubtitleExtractorService.cs`

```csharp
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
```

---

# بخش ۷: `SubtitleTranslatorService.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\SubtitleTranslatorService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class SubtitleCue
    {
        public string Index { get; set; } = string.Empty;
        public string Timecode { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public long StartMs { get; set; }
        public long EndMs { get; set; }
    }

    public class SubtitleTranslationProgressInfo
    {
        public int CurrentBatch { get; set; }
        public int TotalBatches { get; set; }
        public int TranslatedLines { get; set; }
        public int TotalLines { get; set; }
        public double Percent { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public static class SubtitleTranslatorService
    {
        private static readonly HttpClient _httpClient = new HttpClient(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static List<SubtitleCue> ParseSubtitleFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return new List<SubtitleCue>();

            try
            {
                string rawText = ReadFileWithEncodingFallback(filePath);
                var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                return ParseSrtCues(lines);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to parse subtitle file", ex);
                return new List<SubtitleCue>();
            }
        }

        /// <summary>
        /// Translates an SRT or VTT subtitle file to Persian asynchronously in batches.
        /// </summary>
        public static async Task<(bool success, string? outputPath, string message)> TranslateSubtitleFileAsync(
            string subtitleFilePath, 
            string targetLang = "fa", 
            IProgress<SubtitleTranslationProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(subtitleFilePath) || !File.Exists(subtitleFilePath))
            {
                return (false, null, "فایل زیرنویس یافت نشد.");
            }

            try
            {
                // 1. Read with auto-encoding detection (Windows-1256 vs UTF-8)
                string rawText = ReadFileWithEncodingFallback(subtitleFilePath);
                var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var cues = ParseSrtCues(lines);

                if (cues.Count == 0)
                {
                    return (false, null, "فرمت فایل زیرنویس معتبر نیست یا متنی در آن یافت نشد.");
                }

                int batchSize = 35;
                int totalBatches = (int)Math.Ceiling((double)cues.Count / batchSize);
                var translatedCues = new List<SubtitleCue>();
                int consecutiveFailures = 0;

                for (int i = 0; i < cues.Count; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = cues.Skip(i).Take(batchSize).ToList();
                    var sb = new StringBuilder();

                    for (int idx = 0; idx < batch.Count; idx++)
                    {
                        string cleanText = batch[idx].Text.Replace("\r", " ").Replace("\n", " ");
                        sb.AppendLine($"{idx + 1}. {cleanText}");
                    }

                    string? translatedBatch = await TranslateTextBatchAsync(sb.ToString().Trim(), targetLang, cancellationToken);
                    if (string.IsNullOrWhiteSpace(translatedBatch))
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures >= 3)
                        {
                            return (false, null, "خطا در اتصال به سرور ترجمه آنلاین. لطفاً اتصال اینترنت خود را بررسی کنید.");
                        }
                    }
                    else
                    {
                        consecutiveFailures = 0;
                    }

                    var translatedMap = !string.IsNullOrWhiteSpace(translatedBatch) 
                        ? ParseNumberedLines(translatedBatch) 
                        : new Dictionary<int, string>();

                    for (int idx = 0; idx < batch.Count; idx++)
                    {
                        int oneBased = idx + 1;
                        string transText = translatedMap.ContainsKey(oneBased) && !string.IsNullOrWhiteSpace(translatedMap[oneBased])
                            ? translatedMap[oneBased]
                            : batch[idx].Text;

                        translatedCues.Add(new SubtitleCue
                        {
                            Index = batch[idx].Index,
                            Timecode = batch[idx].Timecode,
                            Text = transText
                        });
                    }

                    int currentBatch = (i / batchSize) + 1;
                    int processedLines = Math.Min(i + batch.Count, cues.Count);
                    double currentProgress = Math.Round(((double)processedLines / cues.Count) * 100.0, 1);

                    progress?.Report(new SubtitleTranslationProgressInfo
                    {
                        CurrentBatch = currentBatch,
                        TotalBatches = totalBatches,
                        TranslatedLines = processedLines,
                        TotalLines = cues.Count,
                        Percent = currentProgress,
                        StatusText = $"پارت {currentBatch} از {totalBatches} ({processedLines} از {cues.Count} خط • {currentProgress:0}٪)"
                    });
                }

                // 2. Save translated file next to the video/original subtitle
                string dir = Path.GetDirectoryName(subtitleFilePath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(subtitleFilePath);
                
                if (baseName.EndsWith(".fa", StringComparison.OrdinalIgnoreCase) || baseName.EndsWith("_fa", StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - 3);
                }

                string outputPath = Path.Combine(dir, $"{baseName}.fa.srt");

                var srtOutput = new StringBuilder();
                for (int i = 0; i < translatedCues.Count; i++)
                {
                    var cue = translatedCues[i];
                    srtOutput.AppendLine((i + 1).ToString());
                    srtOutput.AppendLine(cue.Timecode.Replace(".", ","));
                    srtOutput.AppendLine(cue.Text);
                    srtOutput.AppendLine();
                }

                await File.WriteAllTextAsync(outputPath, srtOutput.ToString(), Encoding.UTF8);
                return (true, outputPath, $"زیرنویس با موفقیت به فارسی ترجمه شد ({translatedCues.Count} خط).");
            }
            catch (OperationCanceledException)
            {
                return (false, null, "ترجمه زیرنویس توسط کاربر متوقف شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Subtitle translation error", ex);
                return (false, null, $"خطا در ترجمه زیرنویس: {ex.Message}");
            }
        }

        public static string FixSubtitleEncoding(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return filePath;
                string text = ReadFileWithEncodingFallback(filePath);
                
                string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(filePath);
                string utf8Path = Path.Combine(dir, $"{baseName}_UTF8.srt");
                
                File.WriteAllText(utf8Path, text, Encoding.UTF8);
                return utf8Path;
            }
            catch
            {
                return filePath;
            }
        }

        private static string ReadFileWithEncodingFallback(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            
            // Check UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            // Check UTF-16 LE
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            // Try UTF-8 validation
            try
            {
                var utf8NoBom = new UTF8Encoding(false, true);
                string utf8Text = utf8NoBom.GetString(bytes);

                if (Regex.IsMatch(utf8Text, @"[\u0600-\u06FF]") || !Regex.IsMatch(utf8Text, @"[\xC0-\xFF]"))
                {
                    return utf8Text;
                }
            }
            catch { }

            // Fallback to Windows-1256 (Arabic/Persian legacy encoding)
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var win1256 = Encoding.GetEncoding(1256);
                string win1256Text = win1256.GetString(bytes);
                if (Regex.IsMatch(win1256Text, @"[\u0600-\u06FF]"))
                {
                    return win1256Text;
                }
            }
            catch { }

            return Encoding.UTF8.GetString(bytes);
        }

        public static string CleanSubtitleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            try
            {
                // 1. Remove ASS override tags like {\an8}, {\pos(100,200)}, {\c&H...}, {\fs24}, {\b1}, {\i1}, etc.
                string cleaned = Regex.Replace(text, @"\{[^}]*\}", string.Empty);

                // 2. Remove ASS dialogue line prefixes if present (e.g. "0,0:00:00.00,...")
                if (cleaned.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cleaned.Split(new[] { ',' }, 10);
                    if (parts.Length == 10)
                    {
                        cleaned = parts[9];
                    }
                }

                // 3. Replace <br> or \N or \n with newline
                cleaned = Regex.Replace(cleaned, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                cleaned = cleaned.Replace("\\N", "\n").Replace("\\n", "\n");

                // 4. Remove all HTML / XML tags like <font color="...">, </font>, <i>, </i>, <b>, </b>, <u>, </u>, etc.
                cleaned = Regex.Replace(cleaned, @"<[^>]+>", string.Empty);

                // 5. Decode HTML entities (&rlm;, &lrm;, &nbsp;, &amp;, &quot;)
                cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
                cleaned = cleaned.Replace("\u200E", "").Replace("\u200F", "").Replace("\u200B", "");

                // 6. Clean empty lines or multiple consecutive newlines
                var lines = cleaned.Split('\n')
                                   .Select(l => l.Trim())
                                   .Where(l => !string.IsNullOrWhiteSpace(l));

                return string.Join("\n", lines).Trim();
            }
            catch
            {
                return text.Trim();
            }
        }

        private static List<SubtitleCue> ParseSrtCues(string[] lines)
        {
            var cues = new List<SubtitleCue>();
            string currentIndex = "";
            string currentTimecode = "";
            var currentText = new StringBuilder();

            void AddCurrentCue()
            {
                if (!string.IsNullOrEmpty(currentTimecode) && currentText.Length > 0)
                {
                    var (startMs, endMs) = ParseTimecodeRange(currentTimecode);
                    string raw = currentText.ToString().Trim();
                    string clean = CleanSubtitleText(raw);
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        // 🎯 Guard against corrupt or advertising cues with absurd durations (e.g. 50 minutes long!)
                        if (endMs > startMs)
                        {
                            long duration = endMs - startMs;
                            if (duration > 12000)
                            {
                                endMs = startMs + 6000;
                            }
                        }
                        else
                        {
                            endMs = startMs + 4000;
                        }

                        cues.Add(new SubtitleCue
                        {
                            Index = currentIndex,
                            Timecode = currentTimecode,
                            Text = clean,
                            StartMs = startMs,
                            EndMs = endMs
                        });
                    }
                    currentIndex = "";
                    currentTimecode = "";
                    currentText.Clear();
                }
            }

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    AddCurrentCue();
                }
                else if (line.Contains("-->"))
                {
                    currentTimecode = line;
                }
                else if (string.IsNullOrEmpty(currentTimecode) && line.All(char.IsDigit))
                {
                    currentIndex = line;
                }
                else
                {
                    if (currentText.Length > 0) currentText.Append("\n");
                    currentText.Append(line);
                }
            }

            AddCurrentCue();
            return cues;
        }

        private static (long startMs, long endMs) ParseTimecodeRange(string timecodeLine)
        {
            try
            {
                var parts = timecodeLine.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    long start = ParseSingleTimecodeToMs(parts[0].Trim());
                    long end = ParseSingleTimecodeToMs(parts[1].Trim());
                    return (start, end);
                }
            }
            catch { }
            return (0, 0);
        }

        private static long ParseSingleTimecodeToMs(string part)
        {
            try
            {
                var clean = part.Replace(',', '.');
                var pieces = clean.Split(':');
                if (pieces.Length == 3)
                {
                    if (double.TryParse(pieces[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double h) &&
                        double.TryParse(pieces[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(pieces[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                    {
                        return (long)((h * 3600 + m * 60 + s) * 1000);
                    }
                }
                else if (pieces.Length == 2)
                {
                    if (double.TryParse(pieces[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(pieces[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                    {
                        return (long)((m * 60 + s) * 1000);
                    }
                }
            }
            catch { }
            return 0;
        }

        private static async Task<string> TranslateTextBatchAsync(string text, string targetLang, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try
            {
                string encodedQuery = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={encodedQuery}";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(jsonResult);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var segments = root[0];
                        if (segments.ValueKind == JsonValueKind.Array)
                        {
                            var sb = new StringBuilder();
                            foreach (var segment in segments.EnumerateArray())
                            {
                                if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
                                {
                                    if (segment[0].ValueKind == JsonValueKind.String)
                                    {
                                        sb.Append(segment[0].GetString());
                                    }
                                }
                            }
                            return sb.ToString().Trim();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Batch translation request failed", ex);
            }

            return text;
        }

        private static Dictionary<int, string> ParseNumberedLines(string translatedText)
        {
            var resultMap = new Dictionary<int, string>();
            var lines = translatedText.Split('\n');
            int currentNum = -1;
            var currentSb = new StringBuilder();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                var match = Regex.Match(trimmed, @"^(\d+)[\.\)]\s*(.*)");
                if (match.Success)
                {
                    if (currentNum != -1)
                    {
                        resultMap[currentNum] = currentSb.ToString().Trim();
                        currentSb.Clear();
                    }
                    if (int.TryParse(match.Groups[1].Value, out int parsedNum))
                    {
                        currentNum = parsedNum;
                        currentSb.Append(match.Groups[2].Value);
                    }
                }
                else if (currentNum != -1)
                {
                    if (currentSb.Length > 0) currentSb.Append(" ");
                    currentSb.Append(trimmed);
                }
            }

            if (currentNum != -1 && currentSb.Length > 0)
            {
                resultMap[currentNum] = currentSb.ToString().Trim();
            }

            return resultMap;
        }
    }
}
```

---

# بخش ۸: `PlaybackService.cs`
**مسیر:** `c:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\PlaybackService.cs`

```csharp
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

            var settings = SettingsManager.LoadSettings();

            if (settings.UseInternalPlayer)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var playerWindow = new Views.PlayerWindow(file, playlist, initialIndex);
                    playerWindow.Show();
                    playerWindow.Activate();
                });
            }
            else
            {
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
                @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini.exe",
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
```

---

## 📁 `Services/OnlineSubtitleFetcherService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class OnlineSubtitleResultModel
    {
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Source { get; set; } = "SubDL";
        public bool IsSeasonPack { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string Author { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public bool IsPersian => Language.Contains("فارسی") || Language.ToLowerInvariant().Contains("fa") || Language.ToLowerInvariant().Contains("persian");
    }

    public static class OnlineSubtitleFetcherService
    {
        public const string DEFAULT_SUBDL_KEY = "subdl_HHtBliLNdNumqWs29n7Z4E9GLQwyX0bL9MDFc6RTy34";
        public const string DEFAULT_SUBSOURCE_KEY = "sk_68d68b32ef82a0a168e243815c66d85ca5ecfe2909507245e8ff695b27c10025";

        private static readonly HttpClient _httpClient = new HttpClient(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static async Task<List<OnlineSubtitleResultModel>> SearchOnlineSubtitlesAsync(
            string query,
            string? videoPath = null,
            string language = "ALL")
        {
            var results = new List<OnlineSubtitleResultModel>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var sEpMatch = Regex.Match(query, @"(?i)s(\d+)\s*e(\d+)");
            int? season = sEpMatch.Success && int.TryParse(sEpMatch.Groups[1].Value, out int sVal) ? sVal : null;
            int? episode = sEpMatch.Success && int.TryParse(sEpMatch.Groups[2].Value, out int eVal) ? eVal : null;

            string cleanTitle = Regex.Replace(query, @"(?i)s\d+\s*e\d+", "")
                                     .Replace(".", " ")
                                     .Replace("-", " ")
                                     .Replace("_", " ")
                                     .Trim();

            string encodedTitle = Uri.EscapeDataString(cleanTitle);

            string subdlLangs = language switch
            {
                "FA" => "FA",
                "EN" => "EN",
                "AR" => "AR",
                "TR" => "TR",
                "AZ" => "AZ",
                _ => "FA,EN,AR,TR,AZ"
            };

            // 1. SubDL API
            try
            {
                var sb = new StringBuilder($"https://api.subdl.com/api/v1/subtitles?film_name={encodedTitle}&languages={subdlLangs}&api_key={DEFAULT_SUBDL_KEY}&subs_per_page=30");
                if (season != null) sb.Append($"&season_number={season}&type=tv");
                if (episode != null) sb.Append($"&episode_number={episode}");

                var response = await _httpClient.GetAsync(sb.ToString());
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var status) && status.GetBoolean())
                    {
                        if (root.TryGetProperty("subtitles", out var subsArray) && subsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in subsArray.EnumerateArray())
                            {
                                string releaseName = item.TryGetProperty("release_name", out var rel) && rel.ValueKind == JsonValueKind.String ? rel.GetString() ?? "" : "";
                                string name = item.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String ? nm.GetString() ?? releaseName : releaseName;
                                string lang = item.TryGetProperty("lang", out var lg) && lg.ValueKind == JsonValueKind.String ? lg.GetString() ?? "EN" : "EN";
                                string urlPath = item.TryGetProperty("url", out var ul) && ul.ValueKind == JsonValueKind.String ? ul.GetString() ?? "" : "";
                                int? subSeason = item.TryGetProperty("season", out var sn) && sn.ValueKind == JsonValueKind.Number && sn.GetInt32() > 0 ? sn.GetInt32() : (int?)null;
                                int? subEpisode = item.TryGetProperty("episode", out var ep) && ep.ValueKind == JsonValueKind.Number && ep.GetInt32() > 0 ? ep.GetInt32() : (int?)null;
                                bool hi = item.TryGetProperty("hi", out var hiProp) && hiProp.ValueKind == JsonValueKind.True;
                                string author = item.TryGetProperty("author", out var auth) && auth.ValueKind == JsonValueKind.String ? auth.GetString() ?? "" : "";

                                if (!string.IsNullOrEmpty(urlPath))
                                {
                                    string fullUrl = urlPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? urlPath : $"https://dl.subdl.com{urlPath}";
                                    bool isSeasonPack = subEpisode == null && subSeason != null;

                                    string langLower = lang.ToLowerInvariant();
                                    string langLabel = (langLower.Contains("fa") || langLower.Contains("per") || langLower.Contains("farsi"))
                                        ? "🇮🇷 فارسی"
                                        : (langLower.Contains("en") || langLower.Contains("eng"))
                                            ? "🇬🇧 English"
                                            : lang;

                                    string displayTitle = (string.IsNullOrWhiteSpace(releaseName) ? name : releaseName).Trim();
                                    if (hi) displayTitle += " [HI]";
                                    if (isSeasonPack) displayTitle += " [پک کامل فصل]";
                                    if (!string.IsNullOrWhiteSpace(author)) displayTitle += $" — {author}";

                                    if (!results.Any(r => r.DownloadUrl == fullUrl))
                                    {
                                        results.Add(new OnlineSubtitleResultModel
                                        {
                                            Title = displayTitle,
                                            Language = langLabel,
                                            DownloadUrl = fullUrl,
                                            Source = "SubDL",
                                            IsSeasonPack = isSeasonPack,
                                            Season = subSeason,
                                            Episode = subEpisode,
                                            Author = author,
                                            ReleaseName = releaseName
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("SubDL search failed", ex);
            }

            return results;
        }

        public static async Task<(bool success, string? filePath, string message)> DownloadSubtitleAsync(
            OnlineSubtitleResultModel item,
            string? currentVideoPath = null)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl))
                return (false, null, "آدرس دانلود زیرنویس نامعتبر است.");

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "MovieManagerDesktop", "OnlineSubtitles");
                Directory.CreateDirectory(tempDir);

                string safeTitle = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars())).Trim();
                if (safeTitle.Length > 50) safeTitle = safeTitle.Substring(0, 50);

                var response = await _httpClient.GetAsync(item.DownloadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"خطا در دانلود از سرور: {response.StatusCode}");
                }

                byte[] rawData = await response.Content.ReadAsByteArrayAsync();

                // GZIP / ZIP decompression & Windows-1256 detection
                if (rawData.Length > 2 && rawData[0] == 0x1F && rawData[1] == 0x8B)
                {
                    using var ms = new MemoryStream(rawData);
                    using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    await gzip.CopyToAsync(outMs);
                    rawData = outMs.ToArray();
                }

                if (rawData.Length > 4 && rawData[0] == 'P' && rawData[1] == 'K')
                {
                    using var zipMs = new MemoryStream(rawData);
                    using var archive = new ZipArchive(zipMs, ZipArchiveMode.Read);

                    var srtEntries = archive.Entries
                        .Where(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.EndsWith(".ass", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (srtEntries.Count == 0)
                        return (false, null, "هیچ فایل زیرنویسی در آرشیو ZIP یافت نشد.");

                    var targetEntry = srtEntries[0];
                    string srtPath = Path.Combine(tempDir, $"{safeTitle}{Path.GetExtension(targetEntry.FullName)}");

                    using var entryStream = targetEntry.Open();
                    using var fileStream = File.Create(srtPath);
                    await entryStream.CopyToAsync(fileStream);
                    fileStream.Close();

                    string fixedSrtPath = SubtitleTranslatorService.FixSubtitleEncoding(srtPath);
                    return (true, fixedSrtPath, "زیرنویس با موفقیت استخراج و فعال شد.");
                }

                string singleSubPath = Path.Combine(tempDir, $"{safeTitle}.srt");
                await File.WriteAllBytesAsync(singleSubPath, rawData);
                string finalFixedPath = SubtitleTranslatorService.FixSubtitleEncoding(singleSubPath);
                return (true, finalFixedPath, "زیرنویس با موفقیت دریافت و فعال شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to download online subtitle", ex);
                return (false, null, $"خطا در دانلود زیرنویس: {ex.Message}");
            }
        }
    }
}
```

