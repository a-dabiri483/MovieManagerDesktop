using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class LicenseActivationWindow : Window
    {
        private bool _isChecking = false;

        public LicenseActivationWindow()
        {
            InitializeComponent();
            Loaded += LicenseActivationWindow_Loaded;
        }

        private async void LicenseActivationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string hwid = HardwareIdService.GetHardwareId();
            TxtHwid.Text = hwid;
            RefreshLicenseUi();

            // Auto-check on open if not currently activated (in case user just paid on website)
            var lic = LicenseManagerService.GetCurrentLicense();
            if (!lic.IsActivated || !lic.IsValid)
            {
                await PerformCheckLicenseAsync(silent: true);
            }
        }

        private void RefreshLicenseUi()
        {
            var lic = LicenseManagerService.GetCurrentLicense();
            if (lic.IsActivated && lic.IsValid)
            {
                ActiveLicenseCard.Visibility = Visibility.Visible;
                LicenseActionsCard.Visibility = Visibility.Collapsed;

                TxtActivePlan.Text = !string.IsNullOrWhiteSpace(lic.PlanTitle) ? lic.PlanTitle : "اشتراک فعال MovieManager";
                if (lic.IsLifetime)
                {
                    TxtActiveExpiry.Text = "مادام‌العمر (بدون انقضا)";
                }
                else if (lic.ExpiresAt.HasValue)
                {
                    int days = lic.DaysRemaining ?? 0;
                    TxtActiveExpiry.Text = $"{days} روز باقیمانده ({lic.ExpiresAt.Value:yyyy/MM/dd})";
                }
                else
                {
                    TxtActiveExpiry.Text = "فعال و معتبر ✓";
                }
            }
            else
            {
                ActiveLicenseCard.Visibility = Visibility.Collapsed;
                LicenseActionsCard.Visibility = Visibility.Visible;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnCopyHwid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtHwid.Text);
                TxtCopyBtnLabel.Text = "کپی شد! ✓";
                await Task.Delay(2000);
                TxtCopyBtnLabel.Text = "کپی";
            }
            catch { }
        }

        private async void BtnCheckLicense_Click(object sender, RoutedEventArgs e)
        {
            await PerformCheckLicenseAsync(silent: false);
        }

        private async Task PerformCheckLicenseAsync(bool silent)
        {
            if (_isChecking) return;
            _isChecking = true;

            BtnCheckLicense.IsEnabled = false;
            TxtCheckBtnText.Text = "در حال بررسی وضعیت لایسنس از سرور...";
            IconCheckBtn.Kind = PackIconKind.Sync;
            if (!silent) FeedbackBox.Visibility = Visibility.Collapsed;

            try
            {
                var result = await LicenseManagerService.ActivateByHwidAsync();

                if (result.Success)
                {
                    RefreshLicenseUi();
                    ShowFeedback(result.Message, true);
                    ToastService.Instance.ShowSuccess("لایسنس شما با موفقیت فعال و تایید شد.");
                }
                else
                {
                    if (!silent)
                    {
                        ShowFeedback(result.Message, false);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    ShowFeedback($"خطا در ارتباط: {ex.Message}", false);
                }
            }
            finally
            {
                _isChecking = false;
                BtnCheckLicense.IsEnabled = true;
                TxtCheckBtnText.Text = "بررسی و فعال‌سازی لایسنس";
                IconCheckBtn.Kind = PackIconKind.ShieldCheckOutline;
            }
        }

        private void ShowFeedback(string message, bool isSuccess)
        {
            FeedbackBox.Visibility = Visibility.Visible;
            TxtFeedbackMessage.Text = message;

            if (isSuccess)
            {
                FeedbackBox.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                FeedbackBox.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                FeedbackBox.BorderThickness = new Thickness(1);
                FeedbackIcon.Kind = PackIconKind.CheckCircle;
                FeedbackIcon.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                TxtFeedbackMessage.Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208));
            }
            else
            {
                FeedbackBox.Background = new SolidColorBrush(Color.FromRgb(69, 10, 10));
                FeedbackBox.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                FeedbackBox.BorderThickness = new Thickness(1);
                FeedbackIcon.Kind = PackIconKind.AlertCircle;
                FeedbackIcon.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                TxtFeedbackMessage.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
            }
        }

        private void BtnBuyLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string hwid = TxtHwid.Text?.Trim() ?? string.Empty;
                string url = "https://moviemanager.ir/portal.html?redirect=buy";
                if (!string.IsNullOrWhiteSpace(hwid))
                {
                    url += $"&hwid={Uri.EscapeDataString(hwid)}";
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowFeedback($"خطا در باز کردن مرورگر: {ex.Message}", false);
            }
        }

        private void BtnDeactivateLicense_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "آیا از غیرفعال‌سازی و حذف لایسنس از این سیستم اطمینان دارید؟\nپس از حذف، برنامه به نسخه محدود رایگان برمی‌گردد.",
                "حذف لایسنس",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                LicenseManagerService.DeactivateCurrentLicense();
                RefreshLicenseUi();
                ShowFeedback("لایسنس با موفقیت از این سیستم حذف و برنامه به حالت رایگان بازگردانده شد.", true);
                ToastService.Instance.ShowSuccess("لایسنس از این سیستم حذف شد.");
            }
        }

        private void BtnSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://moviemanager.ir/support.html",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowFeedback($"خطا در باز کردن مرورگر: {ex.Message}", false);
            }
        }
    }
}
