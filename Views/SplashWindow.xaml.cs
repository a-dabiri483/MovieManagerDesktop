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
            // 1. Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 2. Initialize application services and database in background
            var initTask = Task.Run(() =>
            {
                try
                {
                    using var db = new Data.AppDbContext();
                    db.Database.EnsureCreated();
                }
                catch { }

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

            // 3. Minimum display duration for a pleasant branding experience (~2.2s)
            var minDelayTask = Task.Delay(2200);

            await Task.WhenAll(initTask, minDelayTask);

            // 4. Fade-out animation
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));
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
