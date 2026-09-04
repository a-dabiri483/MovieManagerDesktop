using System.Windows;
using System.Windows.Input;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly UpdateCheckResult _updateInfo;

        public UpdateAvailableWindow(UpdateCheckResult updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            TxtLatestVersion.Text = $"v{_updateInfo.LatestVersion}";
            TxtCurrentVersion.Text = $"v{_updateInfo.CurrentVersion}";
            TxtFileSize.Text = !string.IsNullOrWhiteSpace(_updateInfo.FileSize) ? _updateInfo.FileSize : "نامشخص";
            TxtReleaseDate.Text = !string.IsNullOrWhiteSpace(_updateInfo.ReleaseDate) ? _updateInfo.ReleaseDate : "-";
            TxtChangelog.Text = !string.IsNullOrWhiteSpace(_updateInfo.Changelog) ? _updateInfo.Changelog : "• بهبودهای عمومی و رفع باگ‌های جزئی سامانه.";

            if (_updateInfo.IsMandatory)
            {
                BorderMandatory.Visibility = Visibility.Visible;
                BtnClose.Visibility = Visibility.Collapsed;
                BtnLater.Visibility = Visibility.Collapsed;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (!_updateInfo.IsMandatory)
            {
                this.Close();
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
            {
                UpdateManagerService.OpenDownloadUrl(_updateInfo.DownloadUrl);
            }

            if (!_updateInfo.IsMandatory)
            {
                this.Close();
            }
        }
    }
}
