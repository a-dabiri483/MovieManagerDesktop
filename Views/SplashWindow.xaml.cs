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

            // Responsive sizing for smaller screens
            double workH = SystemParameters.WorkArea.Height;
            double workW = SystemParameters.WorkArea.Width;
            if (workH < 450 || workW < 600)
            {
                double scale = Math.Min((workH * 0.85) / 360.0, (workW * 0.85) / 540.0);
                scale = Math.Clamp(scale, 0.65, 0.95);
                this.Width = 540 * scale;
                this.Height = 360 * scale;
                if (this.Content is FrameworkElement root)
                {
                    root.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
            }

            Loaded += SplashWindow_Loaded;
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade-in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350));
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Initialize background services
            var initTask = Task.Run(() =>
            {
                try { _ = Services.SettingsManager.SyncEncryptedProxiesAsync(); } catch { }
                try { _ = Services.LicenseManagerService.VerifyLicenseAsync(); } catch { }
            });

            // Show splash for at least 2.5s
            var minDelay = Task.Delay(2500);

            // Pre-create MainWindow while splash is still visible so the user
            // never sees a blank/frozen frame after the splash closes.
            MainWindow? preloadedMainWindow = null;
            await Task.Delay(800); // let splash render fully first
            try
            {
                preloadedMainWindow = new MainWindow();
                // Give the framework a moment to parse XAML and
                // let HomeViewModel fire its async data load
                await Task.Delay(200);
            }
            catch { }

            // Wait for services + minimum display time
            await Task.WhenAll(initTask, minDelay);

            // Fade-out and transition
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (s, ev) =>
            {
                var mainWindow = preloadedMainWindow ?? new MainWindow();
                if (Application.Current != null)
                {
                    Application.Current.MainWindow = mainWindow;
                }
                mainWindow.Show();
                Close();
            };
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
