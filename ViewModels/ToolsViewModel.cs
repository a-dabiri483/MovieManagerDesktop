using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ToolsViewModel : ObservableObject
    {
        [RelayCommand]
        private void OpenFolderIcon()
        {
            if (!LicenseManagerService.EnsureProFeature("ساخت آیکون‌های پوشه")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new FolderIconToolViewModel()));
        }

        [RelayCommand]
        private void OpenNameCleaner()
        {
            // ابزار پاک‌سازی نام در نسخه رایگان باز است
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new NameCleanerToolViewModel()));
        }

        [RelayCommand]
        private void OpenSeriesOrganizer()
        {
            if (!LicenseManagerService.EnsureProFeature("سازمان‌دهی پیشرفته سریال‌ها")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesOrganizerToolViewModel()));
        }

        [RelayCommand]
        private void OpenSeriesFileRenamer()
        {
            if (!LicenseManagerService.EnsureProFeature("تغییر نام گروهی سریال‌ها")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesFileRenamerViewModel()));
        }

        [RelayCommand]
        private void OpenAutoRelocator()
        {
            if (!LicenseManagerService.EnsureProFeature("انتقال و سازمان‌دهی خودکار آرشیو")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new AutoRelocatorViewModel()));
        }

        [RelayCommand]
        private void OpenLibraryCompare()
        {
            if (!LicenseManagerService.EnsureProFeature("مقایسه دو کتابخانه و هارد")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new LibraryCompareToolViewModel()));
        }

        [RelayCommand]
        private void OpenCatalogExport()
        {
            if (!LicenseManagerService.EnsureProFeature("خروجی کاتالوگ (اکسل، PDF، HTML)")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CatalogExportToolViewModel()));
        }

        [RelayCommand]
        private void OpenMissingEpisodes()
        {
            if (!LicenseManagerService.EnsureProFeature("بررسی قسمت‌های ناقص سریال")) return;
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new MissingEpisodesToolViewModel()));
        }
    }
}
