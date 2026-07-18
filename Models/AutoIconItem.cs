using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public enum AutoIconStatus
    {
        Pending,
        Searching,
        Found,
        NotFound,
        Downloading,
        Applied,
        Error
    }

    public partial class AutoIconItem : ObservableObject
    {
        [ObservableProperty]
        private string _folderPath = string.Empty;

        [ObservableProperty]
        private string _folderName = string.Empty;

        [ObservableProperty]
        private string _extractedName = string.Empty;

        [ObservableProperty]
        private string _mediaTitle = string.Empty;

        [ObservableProperty]
        private string? posterUrl;

        [ObservableProperty]
        private double? rating;

        [ObservableProperty]
        private BitmapImage? _posterPreview;

        [ObservableProperty]
        private AutoIconStatus _status = AutoIconStatus.Pending;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private bool _hasExistingIcon;

        [ObservableProperty]
        private string _iconPath = string.Empty;

        public string StatusLabel => Status switch
        {
            AutoIconStatus.Pending => "در انتظار",
            AutoIconStatus.Searching => "در حال جستجو...",
            AutoIconStatus.Found => "پیدا شد",
            AutoIconStatus.NotFound => "پیدا نشد",
            AutoIconStatus.Downloading => "در حال دانلود...",
            AutoIconStatus.Applied => "اعمال شد",
            AutoIconStatus.Error => "خطا",
            _ => "نامشخص"
        };

        public string StatusIcon => Status switch
        {
            AutoIconStatus.Pending => "⏳",
            AutoIconStatus.Searching => "🔍",
            AutoIconStatus.Found => "✅",
            AutoIconStatus.NotFound => "❌",
            AutoIconStatus.Downloading => "⬇️",
            AutoIconStatus.Applied => "✨",
            AutoIconStatus.Error => "⚠️",
            _ => "❓"
        };

        partial void OnStatusChanged(AutoIconStatus value)
        {
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusIcon));
        }
    }
}
