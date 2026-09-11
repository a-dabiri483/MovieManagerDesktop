using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly UpdateCheckResult _updateInfo;
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading = false;

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
                CancelActiveDownload();
                this.Close();
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            CancelActiveDownload();
            this.Close();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
            {
                MessageBox.Show("لینک دانلود برای این بروزرسانی معتبر نیست.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Switch to download progress state
            _isDownloading = true;
            GridNormalFooter.Visibility = Visibility.Collapsed;
            GridDownloadProgress.Visibility = Visibility.Visible;
            BtnBrowserFallback.Visibility = Visibility.Collapsed;
            IconErrorStatus.Visibility = Visibility.Collapsed;
            SpinnerProgress.Visibility = Visibility.Visible;
            PnlDownloadStats.Visibility = Visibility.Visible;
            PrgDownload.IsIndeterminate = true;
            PrgDownload.Value = 0;
            TxtPercent.Text = "۰%";
            TxtPercent.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            TxtDownloadSize.Text = "0 MB";
            TxtDownloadSpeed.Text = "0 KB/s";
            TxtDownloadStatus.Text = "در حال برقراری ارتباط با سرور...";
            TxtDownloadStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));

            _downloadCts = new CancellationTokenSource();
            string? installerPath = null;

            var progress = new Progress<UpdateDownloadProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    PrgDownload.IsIndeterminate = false;
                    PrgDownload.Value = p.Percentage;
                    TxtPercent.Text = $"{p.Percentage:0}%";
                    TxtDownloadSize.Text = p.TotalBytes > 0
                        ? $"{p.DownloadedMb:0.0} MB / {p.TotalMb:0.0} MB"
                        : $"{p.DownloadedMb:0.0} MB";
                    TxtDownloadSpeed.Text = p.SpeedText;
                    if (!string.IsNullOrWhiteSpace(p.StatusMessage))
                    {
                        TxtDownloadStatus.Text = p.StatusMessage;
                    }
                });
            });

            try
            {
                installerPath = await UpdateManagerService.DownloadUpdateFileAsync(
                    _updateInfo.DownloadUrl,
                    _updateInfo.LatestVersion,
                    _updateInfo.Sha256,
                    _updateInfo.Signature,
                    progress,
                    _downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                ResetFooterToNormal();
                return;
            }
            catch (Exception ex)
            {
                LoggerService.Error("[UpdateWindow] In-app download failed", ex);
                _isDownloading = false;
                Dispatcher.Invoke(() =>
                {
                    SpinnerProgress.Visibility = Visibility.Collapsed;
                    IconErrorStatus.Visibility = Visibility.Visible;
                    PnlDownloadStats.Visibility = Visibility.Collapsed;
                    PrgDownload.IsIndeterminate = false;
                    TxtPercent.Text = "خطا";
                    TxtPercent.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                    TxtDownloadStatus.Text = ex is InvalidDataException ? ex.Message : "خطا در دریافت فایل بروزرسانی!";
                    TxtDownloadStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                    BtnBrowserFallback.Visibility = Visibility.Visible;
                });
                return;
            }

            if (!string.IsNullOrWhiteSpace(installerPath) && File.Exists(installerPath))
            {
                Dispatcher.Invoke(() =>
                {
                    SpinnerProgress.Visibility = Visibility.Collapsed;
                    IconErrorStatus.Visibility = Visibility.Collapsed;
                    PnlDownloadStats.Visibility = Visibility.Collapsed;
                    PrgDownload.Value = 100;
                    TxtPercent.Text = "۱۰۰%";
                    TxtPercent.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                    TxtDownloadStatus.Text = "دانلود کامل شد. در حال اجرای ستاپ جدید...";
                    TxtDownloadStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                });

                await Task.Delay(1000);
                UpdateManagerService.LaunchInstallerAndExit(installerPath);
            }
        }

        private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                CancelActiveDownload();
                ResetFooterToNormal();
            }
            else
            {
                if (!_updateInfo.IsMandatory)
                {
                    this.Close();
                }
                else
                {
                    ResetFooterToNormal();
                }
            }
        }

        private void BtnBrowserFallback_Click(object sender, RoutedEventArgs e)
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

        private void CancelActiveDownload()
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                try
                {
                    _downloadCts.Cancel();
                    _downloadCts.Dispose();
                }
                catch { }
                finally
                {
                    _downloadCts = null;
                }
            }
            _isDownloading = false;
        }

        private void ResetFooterToNormal()
        {
            _isDownloading = false;
            GridDownloadProgress.Visibility = Visibility.Collapsed;
            GridNormalFooter.Visibility = Visibility.Visible;
        }

        protected override void OnClosed(EventArgs e)
        {
            CancelActiveDownload();
            base.OnClosed(e);
        }
    }
}
