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
