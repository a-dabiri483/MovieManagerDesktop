using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class FolderInfo : ObservableObject
    {
        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _hasIcon;

        [ObservableProperty]
        private string _iconPath = string.Empty;

        // برای نمایش در ListView
        public string DisplayPath => Path;
        public string StatusText => HasIcon ? "دارد" : "ندارد";
        public string StatusIcon => HasIcon ? "✅" : "❌";

        partial void OnHasIconChanged(bool value)
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusIcon));
        }

        partial void OnPathChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayPath));
        }
    }
}
