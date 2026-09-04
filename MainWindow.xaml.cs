using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MovieManagerDesktop
{
    public partial class MainWindow : Window
    {
        // Win32 P/Invoke for setting taskbar icon on borderless windows
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const int WM_SETICON = 0x0080;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL = 0;
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

        private IntPtr _hIconBig = IntPtr.Zero;
        private IntPtr _hIconSmall = IntPtr.Zero;

        public MainWindow()
        {
            InitializeComponent();
            
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/logo.png", UriKind.RelativeOrAbsolute);
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch { }

            this.SourceInitialized += MainWindow_SourceInitialized;
            this.Closing += MainWindow_Closing;
            this.Loaded += MainWindow_Loaded;
            this.Activated += (s, e) => MovieManagerDesktop.Services.MpvPlaybackService.SyncOfflineProgress();

            MovieManagerDesktop.Services.NotificationCenterService.Instance.NewNotificationReceived += (s, e) => PlayBellWiggleAnimation();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MovieManagerDesktop.Services.MpvPlaybackService.SyncOfflineProgress();
            if (MovieManagerDesktop.Services.NotificationCenterService.Instance.HasUnread)
            {
                PlayBellWiggleAnimation();
            }

            // Check for software updates in background
            _ = Task.Run(async () =>
            {
                await Task.Delay(3500);
                try
                {
                    var update = await MovieManagerDesktop.Services.UpdateManagerService.CheckForUpdatesAsync(silent: true);
                    if (update != null && update.HasUpdate)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            // 1. Add notification to bell center
                            MovieManagerDesktop.Services.NotificationCenterService.Instance.AddLocalNotification(
                                title: $"بروزرسانی جدید در دسترس است ({update.LatestVersion})",
                                message: string.IsNullOrWhiteSpace(update.Message) ? "نسخه جدید نرم‌افزار آماده دریافت و نصب است." : update.Message,
                                type: update.IsMandatory ? "warning" : "info",
                                actionTitle: "دانلود بروزرسانی",
                                actionUrl: update.DownloadUrl,
                                isPinned: update.IsMandatory
                            );

                            // 2. Open update dialog popup
                            MovieManagerDesktop.Services.UpdateManagerService.ShowUpdateDialog(update);
                        });
                    }
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.LoggerService.Error("[UpdateCheck] Background check failed", ex);
                }
            });
        }

        private void BtnNotificationBell_MouseEnter(object sender, MouseEventArgs e)
        {
            if (MovieManagerDesktop.Services.NotificationCenterService.Instance.HasUnread)
            {
                PlayBellWiggleAnimation();
            }
        }

        private bool _isWiggling = false;

        public async void PlayBellWiggleAnimation()
        {
            if (_isWiggling) return;
            _isWiggling = true;

            try
            {
                while (MovieManagerDesktop.Services.NotificationCenterService.Instance.HasUnread)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var sb = this.FindResource("BellWiggleStoryboard") as System.Windows.Media.Animation.Storyboard;
                            sb?.Begin(this);
                        }
                        catch { }
                    });

                    // Wait for the animation to finish + a short pause
                    await Task.Delay(2000);
                }
            }
            finally
            {
                _isWiggling = false;
            }
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.Visibility == Visibility.Collapsed) return;

            if (MovieManagerDesktop.Services.BackupManager.IsBackupNeeded())
            {
                e.Cancel = true;

                // Show a beautiful modern dialog
                var border = new System.Windows.Controls.Border
                {
                    Background = (System.Windows.Media.Brush)Application.Current.FindResource("CardBackground"),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(40),
                    Width = 380
                };

                var stackPanel = new System.Windows.Controls.StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                
                var icon = new MaterialDesignThemes.Wpf.PackIcon
                {
                    Kind = MaterialDesignThemes.Wpf.PackIconKind.CloudUploadOutline,
                    Width = 64,
                    Height = 64,
                    Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryColor"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                var title = new System.Windows.Controls.TextBlock
                {
                    Text = "در حال تهیه نسخه پشتیبان",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryText"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var subtitle = new System.Windows.Controls.TextBlock
                {
                    Text = "لطفاً منتظر بمانید تا اطلاعات شما با موفقیت ذخیره شود...",
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryText"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 30)
                };

                var progress = new System.Windows.Controls.ProgressBar
                {
                    Style = (Style)Application.Current.FindResource("MaterialDesignCircularProgressBar"),
                    IsIndeterminate = true,
                    Width = 40,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryColor")
                };

                stackPanel.Children.Add(icon);
                stackPanel.Children.Add(title);
                stackPanel.Children.Add(subtitle);
                stackPanel.Children.Add(progress);
                border.Child = stackPanel;

                _ = MaterialDesignThemes.Wpf.DialogHost.Show(border, "RootDialog");

                try
                {
                    var backupTask = MovieManagerDesktop.Services.BackupManager.RunBackupAsync();
                    var delayTask = Task.Delay(2000); // Minimum 2 seconds delay to ensure the beautiful UI is seen
                    await Task.WhenAll(backupTask, delayTask);
                }
                catch { }

                this.Visibility = Visibility.Collapsed;
                Environment.Exit(0);
            }
            else
            {
                // No backup needed, exit normally
                this.Visibility = Visibility.Collapsed;
                Environment.Exit(0);
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            SetTaskbarIconViaWin32();
        }

        private void SetTaskbarIconViaWin32()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
                if (File.Exists(icoPath))
                {
                    _hIconBig = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                    if (_hIconBig != IntPtr.Zero)
                        SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);

                    _hIconSmall = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                    if (_hIconSmall != IntPtr.Zero)
                        SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
                }
                else
                {
                    // Fallback: extract icon associated with the current executable
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        if (sysIcon != null)
                        {
                            var hIcon = sysIcon.Handle;
                            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIcon);
                            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIcon);
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore icon loading errors
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // Removed parallax effect
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                if (e.OriginalSource is DependencyObject depObj)
                {
                    // Do not initiate drag if clicking interactive controls or list items
                    if (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.Primitives.TextBoxBase>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.Slider>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.ComboBox>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.PasswordBox>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.ListBoxItem>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.ListViewItem>(depObj) != null ||
                        FindVisualParent<System.Windows.Controls.TreeViewItem>(depObj) != null)
                    {
                        return;
                    }
                }

                try
                {
                    this.DragMove();
                }
                catch
                {
                    // Ignore drag exceptions if interrupted
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObj = child;
            while (parentObj != null)
            {
                if (parentObj is T parent)
                    return parent;

                if (parentObj is System.Windows.Media.Visual || parentObj is System.Windows.Media.Media3D.Visual3D)
                    parentObj = System.Windows.Media.VisualTreeHelper.GetParent(parentObj);
                else
                    parentObj = LogicalTreeHelper.GetParent(parentObj);
            }
            return null;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            var logWindow = new MovieManagerDesktop.Views.LogViewerWindow();
            logWindow.Show();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                BtnMaximize.Content = "\uE922";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                BtnMaximize.Content = "\uE923";
            }
        }
    }
}