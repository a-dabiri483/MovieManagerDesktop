using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.ViewModels
{
    public partial class GalleryItemViewModel : ObservableObject
    {
        public VideoFile File { get; }
        
        [ObservableProperty]
        private bool _isUpdating;

        public bool IsFavorite
        {
            get => File.IsFavorite;
            set
            {
                if (File.IsFavorite != value)
                {
                    File.IsFavorite = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FavoriteIconForeground));
                }
            }
        }

        public bool IsWatchlist
        {
            get => File.IsWatchlist;
            set
            {
                if (File.IsWatchlist != value)
                {
                    File.IsWatchlist = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(WatchlistIconForeground));
                }
            }
        }

        public bool IsWatched
        {
            get => File.IsWatched;
            set
            {
                if (File.IsWatched != value)
                {
                    File.IsWatched = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(WatchedIconForeground));
                    OnPropertyChanged(nameof(WatchedBadgeVisibility));
                }
            }
        }

        public bool IsSeries => File.MediaType == "Series";
        public bool HasSeasonEpisode => IsSeries && File.Season.HasValue && File.Episode.HasValue;
        public string SeasonEpisodeText => HasSeasonEpisode ? $"فصل {File.Season} - قسمت {File.Episode}" : "";

        public string FavoriteIconForeground => IsFavorite ? "#E91E63" : "#80FFFFFF";
        public string WatchlistIconForeground => IsWatchlist ? "#FFC107" : "#80FFFFFF";
        public string WatchedIconForeground => IsWatched ? "#4CAF50" : "#80FFFFFF";
        public System.Windows.Visibility WatchedBadgeVisibility => IsWatched ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private Action? _onSelectionChanged;
        private Action<GalleryItemViewModel>? _onToggleFavorite;
        private bool _isSelected;

        public GalleryItemViewModel(VideoFile file, Action? onSelectionChanged = null, Action<GalleryItemViewModel>? onToggleFavorite = null)
        {
            File = file;
            _onSelectionChanged = onSelectionChanged;
            _onToggleFavorite = onToggleFavorite;
        }

        [RelayCommand]
        private void ToggleFavorite()
        {
            _onToggleFavorite?.Invoke(this);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    _onSelectionChanged?.Invoke();
                }
            }
        }

        public void NotifyFileChanged()
        {
            OnPropertyChanged(nameof(File));
        }
    }
}
