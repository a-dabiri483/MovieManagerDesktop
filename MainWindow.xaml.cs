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
            this.SourceInitialized += MainWindow_SourceInitialized;
            this.Closing += MainWindow_Closing;
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
                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
                if (!File.Exists(icoPath)) return;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // Let Windows pick the best size from multi-layer ICO (0,0 = default system size)
                _hIconBig = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                if (_hIconBig != IntPtr.Zero)
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);

                // Load small icon - let Windows pick from ICO
                _hIconSmall = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                if (_hIconSmall != IntPtr.Zero)
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
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