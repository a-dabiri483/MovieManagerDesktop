using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Services
{
    /// <summary>
    /// Global reactive state for VIP / License subscription status across XAML views.
    /// Automatically notifies bindings when subscription is activated, expired or refreshed.
    /// </summary>
    public partial class LicenseState : ObservableObject
    {
        private static readonly LicenseState _instance = new();
        public static LicenseState Instance => _instance;

        [ObservableProperty]
        private bool _isSubscribed;

        [ObservableProperty]
        private bool _isVipBadgeVisible;

        public LicenseState()
        {
            Refresh();
            LicenseManagerService.LicenseStatusChanged += (s, e) =>
            {
                Application.Current?.Dispatcher?.Invoke(Refresh);
            };
        }

        public void Refresh()
        {
            IsSubscribed = LicenseManagerService.IsLicenseValid();
            IsVipBadgeVisible = !IsSubscribed;
        }
    }
}
