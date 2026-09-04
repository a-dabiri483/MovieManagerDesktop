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
        public LicenseActivationWindow()
        {
            InitializeComponent();
            Loaded += LicenseActivationWindow_Loaded;
        }

        private void LicenseActivationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtHwid.Text = HardwareIdService.GetHardwareId();
            RefreshLicenseUi();
        }

        private void RefreshLicenseUi()
        {
            var lic = LicenseManagerService.GetCurrentLicense();
            if (lic.IsActivated && lic.IsValid)
            {
                ActiveLicenseBanner.Visibility = Visibility.Visible;
                TxtActivePlan.Text = lic.PlanTitle;
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
                    TxtActiveExpiry.Text = "فعال";
                }

                TxtActiveKeyMasked.Text = $"کلید فعال: {lic.MaskedLicenseKey}";
            }
            else
            {
                ActiveLicenseBanner.Visibility = Visibility.Collapsed;
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

        private void TxtLicenseKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnActivate_Click(sender, e);
            }
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtLicenseKey.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowFeedback("لطفاً کلید لایسنس خود را وارد نمایید.", false);
                return;
            }

            BtnActivate.IsEnabled = false;
            TxtActivateBtnText.Text = "در حال برقراری ارتباط با سرور و فعال‌سازی...";
            FeedbackBox.Visibility = Visibility.Collapsed;

            var result = await LicenseManagerService.ActivateLicenseAsync(key);

            BtnActivate.IsEnabled = true;
            TxtActivateBtnText.Text = "تایید و فعال‌سازی آنلاین لایسنس";

            if (result.Success)
            {
                ShowFeedback(result.Message, true);
                TxtLicenseKey.Text = string.Empty;
                RefreshLicenseUi();
            }
            else
            {
                ShowFeedback(result.Message, false);
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://moviemanager.ir/checkout.html",
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
                "آیا از غیرفعال‌سازی و حذف لایسنس از این سیستم اطمینان دارید؟\nپس از حذف، برنامه به نسخه رایگان برمی‌گردد.",
                "حذف لایسنس",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                LicenseManagerService.DeactivateCurrentLicense();
                RefreshLicenseUi();
                ShowFeedback("لایسنس با موفقیت از این سیستم حذف و برنامه به حالت رایگان بازگردانده شد.", true);
                ToastService.Instance.ShowSuccess("لایسنس با موفقیت از این سیستم حذف شد.");
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
