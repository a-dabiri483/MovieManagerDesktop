using System;
using System.Collections.Generic;
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
            if (ViewModel?.Player != null)
            {
                FlyleafVideoView.Player = ViewModel.Player;
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

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
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
                // Don't set overlay to Maximized - transparent windows break with maximized state
                // Instead, manually sync bounds after a short delay to let layout settle
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
