using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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

        // ════════════════════════════════════════════════════════════════
        // ── ONLINE SUBTITLES MODAL PROPERTIES ──
        // ════════════════════════════════════════════════════════════════
        [ObservableProperty]
        private bool _showOnlineSubtitleModal = false;

        [ObservableProperty]
        private string _subtitleSearchQuery = string.Empty;

        [ObservableProperty]
        private string _subtitleSearchLanguage = "ALL";

        [ObservableProperty]
        private bool _isSearchingOnlineSubtitles = false;

        [ObservableProperty]
        private bool _isDownloadingOnlineSubtitle = false;

        [ObservableProperty]
        private string _onlineSubtitleStatusText = string.Empty;

        public ObservableCollection<OnlineSubtitleResultModel> OnlineSubtitleResults { get; } = new();

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

        // 🎯 Serialized Seek Queue
        private long _targetSeekMs = -1;
        private DateTime _lastSeekTime = DateTime.MinValue;
        private DateTime _seekDebounceUntil = DateTime.MinValue;
        private bool _seekInFlight = false;
        private long _queuedSeekTargetMs = -1;
        private DateTime _lastSeekIssueTime = DateTime.MinValue;
        private long _vlcTimeAtSeekIssue = -1;

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
                Interval = TimeSpan.FromMilliseconds(20)
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
                    "--avcodec-hw=d3d11va,dxva2,any", 
                    "--directx-hw-yuv", 
                    "--no-sub-autodetect-file",
                    "--no-video-title-show",
                    "--input-fast-seek",
                    "--no-drop-late-frames",
                    "--no-skip-frames",
                    "--file-caching=150",
                    "--network-caching=300",
                    "--clock-jitter=0",
                    "--clock-synchro=0",
                    "--avcodec-threads=0",
                    "--avcodec-fast",
                    "--avcodec-skiploopfilter=4",
                    "--no-audio-time-stretch",
                    "--sub-track=-1",
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
                                     ShowAspectRatiosPopup || ShowSubtitleStudioModal || ShowOnlineSubtitleModal;

        public void HandleMouseMove()
        {
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();
        }

        public void HandleMouseMoveZone(double y, double totalHeight, double x, double totalWidth)
        {
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();

            // If a flyout/popup/drawer is open, don't auto-hide bars
            if (HasOpenFlyout)
            {
                return;
            }

            // Top boundary zone (Top 80px) -> show ONLY top bar
            if (y <= 80)
            {
                ShowTopBar = true;
                ShowBottomBar = false;
                ShowControls = true;
            }
            // Bottom boundary zone (Bottom 120px) -> show ONLY bottom bar
            else if (y >= Math.Max(0, totalHeight - 120))
            {
                ShowBottomBar = true;
                ShowTopBar = false;
                ShowControls = true;
            }
            // Middle zone: immediately hide both bars
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
                // Shift + < / >: Audio Sync (+/- 50ms)
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
            _targetSeekMs = -1;
            _queuedSeekTargetMs = -1;
            _seekInFlight = false;
            _lastActiveSubtitlePath = null;

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
                vlcMedia.AddOption(":no-sub-autodetect-file");
                vlcMedia.AddOption(":spu=-1");
                vlcMedia.AddOption(":file-caching=150");
                _mediaPlayer.Media = vlcMedia;
                _mediaPlayer.Play();
                IsPlaying = true;
                _mediaPlayer.Volume = Volume;
                _mediaPlayer.Mute = IsMuted;

                // Load subtitles from video directory automatically
                LoadExternalSubtitlesFromFolder(media.FilePath);

                // Auto-extract and prepare embedded subtitles in background
                LoadEmbeddedSubtitlesAsync(media.FilePath);

                // Record playback start in database & notify Home in real time
                RecordMediaPlaybackStartAsync(media);

                ShowOsdNotification($"▶ {MediaTitle}");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error playing video file", ex);
                ToastService.Instance.ShowError($"خطا در پخش فایل: {ex.Message}");
            }
        }

        private async void RecordMediaPlaybackStartAsync(VideoFile media)
        {
            if (media == null || media.Id == 0) return;
            try
            {
                using var db = new AppDbContext();
                var dbItem = await db.VideoFiles.FindAsync(media.Id);
                if (dbItem != null)
                {
                    dbItem.LastPlayedAt = DateTime.Now;
                    media.LastPlayedAt = dbItem.LastPlayedAt;
                    await db.SaveChangesAsync();
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                }
            }
            catch { }
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
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await UpdateSubtitleTracksListAsync();
                        });

                        // Check if an external subtitle is not already active
                        if (_activeSubtitleCues.Count == 0)
                        {
                            var preferredTrack = embeddedTracks.FirstOrDefault(t => 
                                t.Language.StartsWith("per", StringComparison.OrdinalIgnoreCase) || 
                                t.Language.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ||
                                t.Title.Contains("farsi", StringComparison.OrdinalIgnoreCase) ||
                                t.Title.Contains("persian", StringComparison.OrdinalIgnoreCase)) ?? embeddedTracks[0];

                            string? extractedPath = await EmbeddedSubtitleExtractorService.ExtractEmbeddedSubtitleToSrtAsync(videoPath, preferredTrack.SubtitleIndex);
                            if (!string.IsNullOrEmpty(extractedPath))
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    LoadSubtitleFileInternal(extractedPath);
                                    _mediaPlayer?.SetSpu(-1);
                                    ShowOsdNotification($"💬 زیرنویس ({preferredTrack.DisplayName}) فعال شد");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Error("Failed in LoadEmbeddedSubtitlesAsync", ex);
                }
            });
        }

        public List<string> GetMatchingSubtitleFilesInFolder(string videoPath)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath)) return results;

            try
            {
                string? dir = Path.GetDirectoryName(videoPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return results;

                string baseName = Path.GetFileNameWithoutExtension(videoPath);
                var allSubsInFolder = Directory.GetFiles(dir, "*.srt")
                    .Concat(Directory.GetFiles(dir, "*.vtt"))
                    .Concat(Directory.GetFiles(dir, "*.ass"))
                    .Distinct()
                    .ToList();

                // Extract Season & Episode if available
                int? season = CurrentMedia?.Season;
                int? episode = CurrentMedia?.Episode;

                if (!season.HasValue || !episode.HasValue)
                {
                    var sEpMatch = Regex.Match(baseName, @"(?i)s(\d+)\s*e(\d+)");
                    if (sEpMatch.Success && int.TryParse(sEpMatch.Groups[1].Value, out int sVal) && int.TryParse(sEpMatch.Groups[2].Value, out int eVal))
                    {
                        season = sVal;
                        episode = eVal;
                    }
                }

                foreach (var sf in allSubsInFolder)
                {
                    string sfName = Path.GetFileNameWithoutExtension(sf);
                    string sfNameLower = sfName.ToLowerInvariant();

                    // 1. Direct match: Subtitle file starts with video base name (e.g. MovieName.fa.srt or MovieName.srt)
                    if (sfName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(sf);
                        continue;
                    }

                    // 2. If Series Episode: match season and episode tokens strictly, excluding other episodes
                    if (season.HasValue && episode.HasValue)
                    {
                        string sPad = season.Value.ToString("D2");
                        string ePad = episode.Value.ToString("D2");
                        string sRaw = season.Value.ToString();
                        string eRaw = episode.Value.ToString();

                        var otherEpMatch = Regex.Match(sfName, @"(?i)s(\d+)\s*e(\d+)");
                        if (otherEpMatch.Success)
                        {
                            if (int.TryParse(otherEpMatch.Groups[1].Value, out int subS) && int.TryParse(otherEpMatch.Groups[2].Value, out int subE))
                            {
                                if (subS == season.Value && subE == episode.Value)
                                {
                                    results.Add(sf);
                                }
                                continue; // Skip if it belongs to another episode
                            }
                        }

                        // Also check 1x03, ep03, etc.
                        bool matchSeason = sfNameLower.Contains($"s{sPad}") || sfNameLower.Contains($"s{sRaw}") || sfNameLower.Contains($"season {sRaw}") || sfNameLower.Contains($"season.{sPad}");
                        bool matchEpisode = sfNameLower.Contains($"e{ePad}") || sfNameLower.Contains($"e{eRaw}") || sfNameLower.Contains($"ep{ePad}") || sfNameLower.Contains($"ep{eRaw}") || sfNameLower.Contains($"{season}x{ePad}");

                        if (matchSeason && matchEpisode)
                        {
                            results.Add(sf);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error scanning matching subtitles", ex);
            }

            return results.Distinct().ToList();
        }

        private void LoadExternalSubtitlesFromFolder(string videoPath)
        {
            try
            {
                var subFiles = GetMatchingSubtitleFilesInFolder(videoPath);
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
                var parsed = SubtitleTranslatorService.ParseSubtitleFile(filePath);
                _activeSubtitleCues = parsed.OrderBy(c => c.StartMs).ToList();

                foreach (var t in SubtitleTracks)
                {
                    t.IsSelected = (t.FilePath == filePath || (t.IsEmbedded && _loadedSubtitlePath != null && _loadedSubtitlePath.Contains($"_sub_{t.SubtitleIndex}.srt")));
                }
            }
            catch { }
        }

        private SubtitleCue? FindSubtitleCue(long timeMs)
        {
            if (_activeSubtitleCues.Count == 0) return null;

            int low = 0;
            int high = _activeSubtitleCues.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                var cue = _activeSubtitleCues[mid];

                if (timeMs >= cue.StartMs && timeMs <= cue.EndMs)
                {
                    return cue;
                }
                else if (timeMs < cue.StartMs)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            if (high >= 0 && high < _activeSubtitleCues.Count)
            {
                var cue = _activeSubtitleCues[high];
                if (timeMs >= cue.StartMs && timeMs <= cue.EndMs) return cue;
            }

            return null;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaPlayer == null) return;

            try
            {
                long vlcTime = _mediaPlayer.Time;

                if (_seekInFlight)
                {
                    // 🎯 آیا Seek قبلی تسویه شده؟
                    // ۱. زمان VLC به محدوده هدف رسیده باشد
                    // ۲. یا زمان VLC نسبت به زمان شروع Seek جابه‌جا شده باشد (نشان‌دهنده رندر فریم جدید)
                    // ۳. یا سقف زمانی ۲۰۰ میلی‌ثانیه تمام شده باشد
                    bool timeReachedTarget = vlcTime >= 0 && Math.Abs(vlcTime - _targetSeekMs) <= 1500;
                    bool vlcFrameAdvanced = vlcTime >= 0 && _vlcTimeAtSeekIssue >= 0 && Math.Abs(vlcTime - _vlcTimeAtSeekIssue) > 800;
                    bool issueTimeout = (DateTime.UtcNow - _lastSeekIssueTime).TotalMilliseconds > 200;

                    bool settled = timeReachedTarget || vlcFrameAdvanced;

                    if (settled || issueTimeout)
                    {
                        if (_queuedSeekTargetMs >= 0 && _queuedSeekTargetMs != _targetSeekMs)
                        {
                            // 🎯 اعمال هدف صفشده (کلیکهایی که حین Seek قبلی آمده بودند)
                            _targetSeekMs = _queuedSeekTargetMs;
                            _queuedSeekTargetMs = -1;
                            _lastSeekIssueTime = DateTime.UtcNow;
                            _lastSeekTime = DateTime.UtcNow;
                            _vlcTimeAtSeekIssue = vlcTime;
                            _seekDebounceUntil = DateTime.UtcNow.AddMilliseconds(150);
                            _mediaPlayer.Time = _targetSeekMs;
                        }
                        else
                        {
                            _queuedSeekTargetMs = -1;
                            _seekInFlight = false;
                        }
                    }
                    // 🎯 تا قبل از تسویه، CurrentTimeMs را از VLC بازنویسی نکن!
                }
                else
                {
                    // 🎯 حالت عادی پخش: بروزرسانی زمان از VLC
                    if (DateTime.UtcNow > _seekDebounceUntil && vlcTime >= 0)
                    {
                        CurrentTimeMs = vlcTime;
                    }
                }

                TotalDurationMs = _mediaPlayer.Length;

                if (TotalDurationMs > 0)
                {
                    // Instant Subtitle Cue Sync via Fast O(log N) Binary Search
                    if (_activeSubtitleCues.Count > 0)
                    {
                        long adjustedTime = CurrentTimeMs + (long)(SubtitleDelaySeconds * 1000.0);
                        var cue = FindSubtitleCue(adjustedTime);

                        if (cue != null)
                        {
                            if (CurrentSubtitleText != cue.Text)
                            {
                                CurrentSubtitleText = cue.Text;
                            }
                            if (!HasSubtitleText)
                            {
                                HasSubtitleText = true;
                            }
                        }
                        else
                        {
                            if (HasSubtitleText)
                            {
                                CurrentSubtitleText = string.Empty;
                                HasSubtitleText = false;
                            }
                        }
                    }
                    else
                    {
                        if (HasSubtitleText)
                        {
                            HasSubtitleText = false;
                            CurrentSubtitleText = string.Empty;
                        }
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

                    if (!_isUserSeeking && DateTime.UtcNow > _seekDebounceUntil)
                    {
                        Progress = (double)CurrentTimeMs / TotalDurationMs;
                        CurrentTimeFormatted = FormatTime(CurrentTimeMs);
                    }
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
                    dbItem.LastPlayedAt = DateTime.Now;
                    file.WatchProgressSeconds = seconds;
                    file.WatchProgressPercent = dbItem.WatchProgressPercent;
                    file.LastPlayedAt = dbItem.LastPlayedAt;
                    await db.SaveChangesAsync();
                    WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
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
            // LibVLC fires EndReached on native thread.
            // Dispatch asynchronously to prevent deadlock with LibVLC core pipeline.
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsPlaying = false;
                        if (CurrentMedia != null)
                        {
                            CurrentMedia.IsWatched = true;
                            CurrentMedia.WatchProgressPercent = 100;
                            if (TotalDurationMs > 0)
                            {
                                CurrentMedia.WatchProgressSeconds = TotalDurationMs / 1000L;
                            }
                            MarkMediaAsWatched(CurrentMedia);
                        }

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
                catch (Exception ex)
                {
                    LoggerService.Error("Error handling MediaPlayer_EndReached", ex);
                }
            });
        }

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsPlaying = false;
                    ToastService.Instance.ShowError("خطا در پردازش و پخش جریان ویدیو.");
                });
            });
        }

        public void ShowOsdNotification(string message)
        {
            OsdMessage = message;
            ShowOsd = true;
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════
        // ── CONTROLS & COMMANDS IMPLEMENTATION ──
        // ════════════════════════════════════════════════════════════════

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

            // 🎯 زنجیره از آخرین هدف قصدشده (نه زمان VLC که عقب است)
            long baseTime;
            if (_targetSeekMs >= 0 && (DateTime.UtcNow - _lastSeekTime).TotalMilliseconds < 3000)
            {
                baseTime = _targetSeekMs;
            }
            else
            {
                baseTime = CurrentTimeMs > 0 ? CurrentTimeMs : Math.Max(0, _mediaPlayer.Time);
            }

            _targetSeekMs = Math.Clamp(baseTime + (seconds * 1000L), 0, Math.Max(0, length - 1000L));
            _lastSeekTime = DateTime.UtcNow;

            // 🎯 UI فوراً بروزرسانی شود (پیشنمایش فوری)
            CurrentTimeMs = _targetSeekMs;
            Progress = (double)_targetSeekMs / length;
            CurrentTimeFormatted = FormatTime(_targetSeekMs);
            string sign = seconds > 0 ? "+" : "";
            ShowOsdNotification($"⏱ پرش: {sign}{seconds}s ➔ {CurrentTimeFormatted}");

            IssueSeek();
        }

        /// <summary>
        /// 🎯 ارسال کنترلشده Seek به libvlc — اگر Seek در حال انجام باشد، هدف صف میشود
        /// </summary>
        private void IssueSeek()
        {
            if (_mediaPlayer == null || _targetSeekMs < 0) return;

            if (_seekInFlight)
            {
                // 🎯 Seek قبلی هنوز تسویه نشده؛ هدف جدید را صف کن (هرگز گم نمیشود)
                _queuedSeekTargetMs = _targetSeekMs;
            }
            else
            {
                _seekInFlight = true;
                _queuedSeekTargetMs = -1;
                _lastSeekIssueTime = DateTime.UtcNow;
                _vlcTimeAtSeekIssue = _mediaPlayer.Time;
                _seekDebounceUntil = DateTime.UtcNow.AddMilliseconds(150);
                _mediaPlayer.Time = _targetSeekMs;
            }
        }

        public void SeekTo(double newProgress, bool isFinal = false)
        {
            if (_mediaPlayer == null) return;
            long length = _mediaPlayer.Length > 0 ? _mediaPlayer.Length : TotalDurationMs;
            if (length <= 0) return;
            newProgress = Math.Clamp(newProgress, 0.0, 1.0);

            _targetSeekMs = (long)(newProgress * length);
            _lastSeekTime = DateTime.UtcNow;
            CurrentTimeMs = _targetSeekMs;
            Progress = newProgress;
            CurrentTimeFormatted = FormatTime(_targetSeekMs);
            IssueSeek();
        }

        public void StartSeek() => _isUserSeeking = true;
        public void EndSeek()
        {
            _isUserSeeking = false;
            IssueSeek();  // 🎯 استفاده از IssueSeek به جای set_time مستقیم
        }

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

            // 3. External matching subtitles in folder
            if (!string.IsNullOrEmpty(CurrentMedia?.FilePath))
            {
                var subFiles = GetMatchingSubtitleFilesInFolder(CurrentMedia.FilePath);
                foreach (var sf in subFiles)
                {
                    if (!newTracks.Any(t => t.FilePath == sf))
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

            // 4. Ensure actively loaded subtitle is in the tracks list
            if (!string.IsNullOrEmpty(_loadedSubtitlePath) && File.Exists(_loadedSubtitlePath))
            {
                bool exists = newTracks.Any(t => t.FilePath == _loadedSubtitlePath || (t.IsEmbedded && _loadedSubtitlePath.Contains($"_sub_{t.SubtitleIndex}.srt")));
                if (!exists)
                {
                    newTracks.Add(new TrackItemModel
                    {
                        Id = _loadedSubtitlePath.GetHashCode(),
                        Name = $"🌐 دانلودی / فعال: {Path.GetFileName(_loadedSubtitlePath)}",
                        FilePath = _loadedSubtitlePath,
                        IsSelected = true
                    });
                }
            }

            SubtitleTracks.Clear();
            foreach (var t in newTracks)
            {
                if (_loadedSubtitlePath != null && (t.FilePath == _loadedSubtitlePath || (t.IsEmbedded && _loadedSubtitlePath.Contains($"_sub_{t.SubtitleIndex}.srt"))))
                {
                    t.IsSelected = true;
                }
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
                foreach (var t in SubtitleTracks) t.IsSelected = (t.Id == -1);
                ShowOsdNotification("💬 زیرنویس غیرفعال شد");
                ShowSubtitlesPopup = false;
                return;
            }

            if (track.IsEmbedded && track.SubtitleIndex >= 0)
            {
                ShowOsdNotification("⏳ در حال استخراج و فعال‌سازی زیرنویس داخلی...");
                string? extracted = await EmbeddedSubtitleExtractorService.ExtractEmbeddedSubtitleToSrtAsync(CurrentMedia.FilePath, track.SubtitleIndex);
                if (!string.IsNullOrEmpty(extracted))
                {
                    LoadSubtitleFileInternal(extracted);
                    _mediaPlayer.SetSpu(-1);
                    foreach (var t in SubtitleTracks) t.IsSelected = (t == track);
                    ShowOsdNotification($"✨ {track.Name} فعال شد");
                    ShowSubtitlesPopup = false;
                }
                else
                {
                    foreach (var t in SubtitleTracks) t.IsSelected = false;
                    var offTrack = SubtitleTracks.FirstOrDefault(t => t.Id == -1);
                    if (offTrack != null) offTrack.IsSelected = true;
                    ShowOsdNotification("⚠️ خطا در استخراج زیرنویس داخلی (امکان کلیک مجدد وجود دارد)");
                }
                return;
            }

            if (!string.IsNullOrEmpty(track.FilePath))
            {
                LoadSubtitleFileInternal(track.FilePath);
                _mediaPlayer.SetSpu(-1);
                foreach (var t in SubtitleTracks) t.IsSelected = (t == track);
                ShowOsdNotification($"💬 {track.Name} فعال شد");
                ShowSubtitlesPopup = false;
                return;
            }
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

            // 1. Check currently active / loaded subtitle on player
            if (!string.IsNullOrEmpty(_loadedSubtitlePath) && File.Exists(_loadedSubtitlePath))
            {
                subPath = _loadedSubtitlePath;
            }
            else if (!string.IsNullOrEmpty(_lastActiveSubtitlePath) && File.Exists(_lastActiveSubtitlePath))
            {
                subPath = _lastActiveSubtitlePath;
            }
            // 2. Check if a non-empty subtitle track is selected
            else
            {
                var selectedTrack = SubtitleTracks.FirstOrDefault(t => t.IsSelected && !string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath));
                if (selectedTrack != null)
                {
                    subPath = selectedTrack.FilePath;
                }
                else
                {
                    var anyTrack = SubtitleTracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath));
                    if (anyTrack != null)
                    {
                        subPath = anyTrack.FilePath;
                    }
                }
            }

            // 3. Check video directory for matching non-Persian subtitles
            if (string.IsNullOrEmpty(subPath) && CurrentMedia != null && !string.IsNullOrEmpty(CurrentMedia.FilePath))
            {
                var matchingSubs = GetMatchingSubtitleFilesInFolder(CurrentMedia.FilePath)
                    .Where(f => !f.EndsWith(".fa.srt", StringComparison.OrdinalIgnoreCase) && !f.EndsWith("_FA.srt", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                if (matchingSubs.Count > 0)
                {
                    subPath = matchingSubs[0];
                }
            }

            // 4. Prompt user only if no subtitle could be found
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
                    await UpdateSubtitleTracksListAsync();
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

            // 1. Check currently active / loaded subtitle on player
            if (!string.IsNullOrEmpty(_loadedSubtitlePath) && File.Exists(_loadedSubtitlePath))
            {
                subPath = _loadedSubtitlePath;
            }
            else if (!string.IsNullOrEmpty(_lastActiveSubtitlePath) && File.Exists(_lastActiveSubtitlePath))
            {
                subPath = _lastActiveSubtitlePath;
            }
            else
            {
                var selectedTrack = SubtitleTracks.FirstOrDefault(t => t.IsSelected && !string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath));
                if (selectedTrack != null)
                {
                    subPath = selectedTrack.FilePath;
                }
            }

            if (string.IsNullOrEmpty(subPath) && CurrentMedia != null && !string.IsNullOrEmpty(CurrentMedia.FilePath))
            {
                var matchingSubs = GetMatchingSubtitleFilesInFolder(CurrentMedia.FilePath);
                if (matchingSubs.Count > 0) subPath = matchingSubs[0];
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
                _ = UpdateSubtitleTracksListAsync();
                ShowOsdNotification("✅ انکودینگ زیرنویس به UTF-8 استاندارد تبدیل شد");
                ToastService.Instance.ShowSuccess("انکودینگ زیرنویس با موفقیت اصلاح و روی فیلم فعال شد.");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // ── ONLINE SUBTITLE COMMANDS (SUBDL & SUBSOURCE) ──
        // ════════════════════════════════════════════════════════════════
        [RelayCommand]
        public void OpenOnlineSubtitleModal()
        {
            ShowSubtitlesPopup = false;
            ShowSubtitleStudioModal = false;
            ShowAudioTracksPopup = false;
            ShowPlaylistDrawer = false;
            ShowBookmarksDrawer = false;
            ShowShortcutsHelp = false;

            // Generate intelligent search query from Media Title and Series info
            string query = MediaTitle;
            if (CurrentMedia != null)
            {
                if (CurrentMedia.Season.HasValue && CurrentMedia.Episode.HasValue)
                {
                    string s = CurrentMedia.Season.Value.ToString("D2");
                    string e = CurrentMedia.Episode.Value.ToString("D2");
                    string cleanSeriesTitle = !string.IsNullOrWhiteSpace(CurrentMedia.FormattedTitle) ? CurrentMedia.FormattedTitle : Path.GetFileNameWithoutExtension(CurrentMedia.FileName);
                    cleanSeriesTitle = Regex.Replace(cleanSeriesTitle, @"(?i)s\d+\s*e\d+.*", "").Trim();
                    query = $"{cleanSeriesTitle} S{s}E{e}";
                }
                else if (!string.IsNullOrWhiteSpace(CurrentMedia.FormattedTitle))
                {
                    query = CurrentMedia.FormattedTitle;
                    if (!string.IsNullOrWhiteSpace(CurrentMedia.Year) && !query.Contains(CurrentMedia.Year))
                    {
                        query += $" {CurrentMedia.Year}";
                    }
                }
            }

            SubtitleSearchQuery = query;
            SubtitleSearchLanguage = "ALL";
            ShowOnlineSubtitleModal = true;

            _ = SearchOnlineSubtitlesAsync();
        }

        [RelayCommand]
        public void CloseOnlineSubtitleModal()
        {
            ShowOnlineSubtitleModal = false;
        }

        [RelayCommand]
        public async Task SearchOnlineSubtitlesAsync()
        {
            if (string.IsNullOrWhiteSpace(SubtitleSearchQuery)) return;

            IsSearchingOnlineSubtitles = true;
            OnlineSubtitleStatusText = "در حال جستجوی زیرنویس در SubDL و SubSource...";
            OnlineSubtitleResults.Clear();

            try
            {
                var list = await OnlineSubtitleFetcherService.SearchOnlineSubtitlesAsync(
                    SubtitleSearchQuery,
                    CurrentMedia?.FilePath,
                    SubtitleSearchLanguage);

                foreach (var item in list)
                {
                    OnlineSubtitleResults.Add(item);
                }

                if (OnlineSubtitleResults.Count == 0)
                {
                    OnlineSubtitleStatusText = "هیچ زیرنویسی برای این عبارت یافت نشد.";
                }
                else
                {
                    OnlineSubtitleStatusText = $"{OnlineSubtitleResults.Count} زیرنویس یافت شد.";
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error searching online subtitles", ex);
                OnlineSubtitleStatusText = $"خطا در جستجو: {ex.Message}";
            }
            finally
            {
                IsSearchingOnlineSubtitles = false;
            }
        }

        [RelayCommand]
        public void SetSearchLanguage(string lang)
        {
            SubtitleSearchLanguage = lang;
            _ = SearchOnlineSubtitlesAsync();
        }

        [RelayCommand]
        public async Task DownloadOnlineSubtitle(OnlineSubtitleResultModel item)
        {
            if (item == null || IsDownloadingOnlineSubtitle) return;

            IsDownloadingOnlineSubtitle = true;
            OnlineSubtitleStatusText = $"در حال دانلود و استخراج: {item.Title}...";
            ShowOsdNotification($"📥 در حال دانلود: {item.Title}");

            try
            {
                var (success, filePath, msg) = await OnlineSubtitleFetcherService.DownloadSubtitleAsync(item, CurrentMedia?.FilePath);
                if (success && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    LoadSubtitleFileInternal(filePath);
                    _mediaPlayer?.SetSpu(-1);
                    await UpdateSubtitleTracksListAsync();

                    ShowOsdNotification($"✅ زیرنویس آنلاین فعال شد: {Path.GetFileName(filePath)}");
                    ToastService.Instance.ShowSuccess("زیرنویس با موفقیت دانلود و روی فیلم فعال شد.");
                    ShowOnlineSubtitleModal = false;
                }
                else
                {
                    ShowOsdNotification($"❌ {msg}");
                    ToastService.Instance.ShowError(msg);
                    OnlineSubtitleStatusText = msg;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to download online subtitle", ex);
                ShowOsdNotification($"❌ خطا در دانلود زیرنویس: {ex.Message}");
                ToastService.Instance.ShowError($"خطا در دانلود زیرنویس: {ex.Message}");
                OnlineSubtitleStatusText = $"خطا: {ex.Message}";
            }
            finally
            {
                IsDownloadingOnlineSubtitle = false;
            }
        }

        [RelayCommand]
        public async Task DownloadAndTranslateSubtitle(OnlineSubtitleResultModel item)
        {
            if (item == null || IsDownloadingOnlineSubtitle || IsTranslatingSubtitle) return;

            IsDownloadingOnlineSubtitle = true;
            OnlineSubtitleStatusText = $"در حال دریافت زیرنویس جهت ترجمه: {item.Title}...";
            ShowOsdNotification($"📥 در حال دریافت زیرنویس جهت ترجمه هوشمند...");

            try
            {
                var (success, filePath, msg) = await OnlineSubtitleFetcherService.DownloadSubtitleAsync(item, CurrentMedia?.FilePath);
                if (success && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    LoadSubtitleFileInternal(filePath);
                    _mediaPlayer?.SetSpu(-1);
                    await UpdateSubtitleTracksListAsync();
                    ShowOnlineSubtitleModal = false;

                    // Immediately start translating the downloaded subtitle
                    await TranslateSubtitleAsync();
                }
                else
                {
                    ShowOsdNotification($"❌ {msg}");
                    ToastService.Instance.ShowError(msg);
                    OnlineSubtitleStatusText = msg;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to download and translate online subtitle", ex);
                ShowOsdNotification($"❌ خطا: {ex.Message}");
                ToastService.Instance.ShowError($"خطا در ترجمه زیرنویس: {ex.Message}");
            }
            finally
            {
                IsDownloadingOnlineSubtitle = false;
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

            // Always mark the current episode as watched when skipping to the next episode (via button, PageDown, or auto-advance)
            if (CurrentMedia != null)
            {
                CurrentMedia.IsWatched = true;
                CurrentMedia.WatchProgressPercent = 100;
                if (TotalDurationMs > 0)
                {
                    CurrentMedia.WatchProgressSeconds = TotalDurationMs / 1000L;
                }
                MarkMediaAsWatched(CurrentMedia);
            }

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
                        dbItem.LastPlayedAt = DateTime.Now;
                        db.SaveChanges();
                        WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
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
