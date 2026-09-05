using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace MovieManagerDesktop.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();

            // Responsive sizing for 720p and smaller screens
            double workH = SystemParameters.WorkArea.Height;
            double workW = SystemParameters.WorkArea.Width;
            if (workH < 620 || workW < 960)
            {
                double scale = Math.Min((workH * 0.88) / 520.0, (workW * 0.90) / 860.0);
                scale = Math.Clamp(scale, 0.70, 0.95);
                this.Width = 860 * scale;
                this.Height = 520 * scale;
                if (this.Content is FrameworkElement root)
                {
                    root.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
            }

            Loaded += SplashWindow_Loaded;
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Smooth Fade-in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 2. Initialize background services asynchronously
            var initTask = Task.Run(() =>
            {
                try
                {
                    _ = Services.SettingsManager.SyncEncryptedProxiesAsync();
                }
                catch { }

                try
                {
                    _ = Services.LicenseManagerService.VerifyLicenseAsync();
                }
                catch { }
            });

            // 3. Minimum display duration for branding (~1.8s)
            var minDelayTask = Task.Delay(1800);

            await Task.WhenAll(initTask, minDelayTask);

            // 4. Smooth Fade-out & transition to MainWindow
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (s, ev) =>
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            };
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
