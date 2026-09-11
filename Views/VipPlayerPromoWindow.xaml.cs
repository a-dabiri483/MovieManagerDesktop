using System.Windows;
using System.Windows.Input;
using MovieManagerDesktop.Helpers;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    /// <summary>
    /// پنجره معرفی و پروموی پلیر اختصاصی MovieManager برای کاربران نسخه رایگان
    /// </summary>
    public partial class VipPlayerPromoWindow : Window
    {
        public bool UserChoseExternalPlayer { get; private set; } = false;
        public bool LicenseActivated { get; private set; } = false;

        public VipPlayerPromoWindow()
        {
            InitializeComponent();
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

        private void BtnBuyLicense_Click(object sender, RoutedEventArgs e)
        {
            var licenseWin = new LicenseActivationWindow
            {
                Owner = this
            };

            WindowHelper.SafeShowDialog(licenseWin);

            if (LicenseManagerService.IsLicenseValid())
            {
                LicenseActivated = true;
                Close();
            }
        }

        private void BtnPlayExternal_Click(object sender, RoutedEventArgs e)
        {
            UserChoseExternalPlayer = true;

            // در صورتی که تیک «عدم نمایش مجدد» فعال باشد، تنظیمات ذخیره شود تا دفعات بعد دیگر سوال نکند
            if (ChkRememberExternalPlayer?.IsChecked == true)
            {
                try
                {
                    var settings = SettingsManager.LoadSettings();
                    settings.UseInternalPlayer = false;
                    SettingsManager.SaveSettings(settings);
                    ToastService.Instance.ShowInfo("پلیر سیستم به عنوان پیش‌فرض ذخیره شد؛ از این پس فایل‌ها مستقیماً با آن پخش می‌شوند.");
                }
                catch { }
            }

            Close();
        }

        private void BtnOpenPlayerSettings_Click(object sender, RoutedEventArgs e)
        {
            Close();
            try
            {
                if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.NavigateToSettingsCommand.Execute(null);
                }
            }
            catch { }
        }
    }
}

