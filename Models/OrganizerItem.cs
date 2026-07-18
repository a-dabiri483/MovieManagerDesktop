using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class OrganizerItem : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private string _seriesName = string.Empty;

        [ObservableProperty]
        private int? _seasonNumber;

        [ObservableProperty]
        private bool _isSelected = true;

        public bool IsSeries => !string.IsNullOrEmpty(SeriesName);

        public string TypeIcon => IsSeries ? "📺" : "🎬";

        public string TypeLabel => IsSeries ? "سریال" : "فیلم";

        public string TargetFolder
        {
            get
            {
                if (!IsSeries) return "(بدون تغییر)";
                return SeriesName;
            }
        }

        [ObservableProperty]
        private OrganizerStatus _status = OrganizerStatus.Pending;

        [ObservableProperty]
        private string? _errorMessage;

        public string StatusIcon => Status switch
        {
            OrganizerStatus.Pending => "⏳",
            OrganizerStatus.Moved => "✅",
            OrganizerStatus.Skipped => "⏭️",
            OrganizerStatus.Error => "❌",
            _ => "❓"
        };

        public string StatusLabel => Status switch
        {
            OrganizerStatus.Pending => "انتظار",
            OrganizerStatus.Moved => "منتقل شد",
            OrganizerStatus.Skipped => "رد شد",
            OrganizerStatus.Error => "خطا",
            _ => "نامشخص"
        };

        partial void OnSeriesNameChanged(string value)
        {
            OnPropertyChanged(nameof(IsSeries));
            OnPropertyChanged(nameof(TypeIcon));
            OnPropertyChanged(nameof(TypeLabel));
            OnPropertyChanged(nameof(TargetFolder));
        }

        partial void OnSeasonNumberChanged(int? value)
        {
            OnPropertyChanged(nameof(TargetFolder));
        }

        partial void OnStatusChanged(OrganizerStatus value)
        {
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusLabel));
        }
    }

    public enum OrganizerStatus
    {
        Pending,
        Moved,
        Skipped,
        Error
    }
}
