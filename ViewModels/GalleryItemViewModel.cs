using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        public bool IsHidden
        {
            get => File.IsHidden;
            set
            {
                if (File.IsHidden != value)
                {
                    File.IsHidden = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSeries => File.MediaType == "Series";
        public bool HasSeasonEpisode => IsSeries && File.Season.HasValue && File.Episode.HasValue;
        public string SeasonEpisodeText => HasSeasonEpisode ? $"فصل {File.Season} - قسمت {File.Episode}" : "";

        // Type Badge
        public string TypeBadgeText => IsSeries ? "سریال" : "فیلم";
        public string TypeBadgeBg => IsSeries ? "#358854D0" : "#35EB3B5A";
        public string TypeBadgeBorder => IsSeries ? "#708854D0" : "#70EB3B5A";
        public string TypeBadgeFg => IsSeries ? "#E0C6FF" : "#FFCCD5";

        // Authentic Year formatting based on user calendar preference (Jalali / Gregorian with English digits)
        public string FormattedYear => DateTimeFormatterService.FormatYear(File.Year);

        public string DisplayYear => !string.IsNullOrWhiteSpace(FormattedYear) ? FormattedYear : "نامشخص";
        public bool HasYear => true; // Always present for all items

        // Rating formatting
        public bool HasRating => File.Rating.HasValue && File.Rating.Value > 0;
        public string RatingFormatted
        {
            get
            {
                if (!HasRating) return "";
                double r = File.Rating!.Value;
                if (r > 10.0) r = Math.Round(r / 10.0, 1);
                return r.ToString("0.0");
            }
        }

        // Real Dubbing / Subtitle / Original Language Detection (Always 1 badge guaranteed)
        public bool HasDubbing
        {
            get
            {
                if (File.HasDubbing) return true;
                string text = $"{File.FileName} {File.FilePath}".ToLowerInvariant();
                string[] dubKeywords = { "dubbed", "farsi.dubbed", "farsi_dubbed", "farsidubbed", "دوبله", "fa.dubbed", "fa_dubbed", "persian.dubbed", "duble", "2dooble", "doooble", "dooble", "دو زبانه", "دوزبانه" };
                return dubKeywords.Any(k => text.Contains(k));
            }
        }

        public bool HasSubtitle
        {
            get
            {
                if (File.HasSubtitle) return true;
                string text = $"{File.FileName} {File.FilePath}".ToLowerInvariant();
                string[] subKeywords = { "subbed", "subtitle", "softsub", "hardsub", "زیرنویس" };
                return subKeywords.Any(k => text.Contains(k));
            }
        }

        public string AudioBadgeText => HasDubbing ? "دوبله" : (HasSubtitle ? "زیرنویس" : "زبان اصلی");
        public string AudioBadgeBg => HasDubbing ? "#2500B4D8" : (HasSubtitle ? "#25E9C46A" : "#18FFFFFF");
        public string AudioBadgeBorder => HasDubbing ? "#4500B4D8" : (HasSubtitle ? "#45E9C46A" : "#28FFFFFF");
        public string AudioBadgeFg => HasDubbing ? "#00D2D3" : (HasSubtitle ? "#FED330" : "#D1D8E0");

        // Real Age / Content Rating
        public string? AgeRating
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(File.ContentRating)) return File.ContentRating;
                string text = $"{File.FileName} {File.FilePath} {File.Genres}".ToLowerInvariant();
                if (text.Contains("+18") || text.Contains("18+") || text.Contains("xxx") || text.Contains("adult") || text.Contains("erotic") || text.Contains("hentai"))
                    return "🔞 +18";
                return null;
            }
        }

        public bool HasAgeRating => !string.IsNullOrWhiteSpace(AgeRating);

        // Real Last Played Text
        public string? LastPlayedText
        {
            get
            {
                if (!IsSeries) return null;
                if (File.LastPlayedEpisode.HasValue && File.LastPlayedEpisode > 0)
                    return $"آخرین پخش: قسمت {File.LastPlayedEpisode}";
                return null;
            }
        }

        public string FavoriteIconForeground => IsFavorite ? "#E91E63" : "#80FFFFFF";
        public string WatchlistIconForeground => IsWatchlist ? "#FFC107" : "#80FFFFFF";
        public string WatchedIconForeground => IsWatched ? "#4CAF50" : "#80FFFFFF";
        public System.Windows.Visibility WatchedBadgeVisibility => IsWatched ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private Action? _onSelectionChanged;
        private Action<GalleryItemViewModel>? _onToggleFavorite;
        private Action<GalleryItemViewModel>? _onManageTags;
        private bool _isSelected;

        public GalleryItemViewModel(VideoFile file, Action? onSelectionChanged = null, Action<GalleryItemViewModel>? onToggleFavorite = null, Action<GalleryItemViewModel>? onManageTags = null)
        {
            File = file;
            _onSelectionChanged = onSelectionChanged;
            _onToggleFavorite = onToggleFavorite;
            _onManageTags = onManageTags;
        }

        [RelayCommand]
        public async Task ToggleFavoriteAsync()
        {
            IsFavorite = !IsFavorite;
            await Task.Run(() =>
            {
                using var db = new AppDbContext();
                var list = db.VideoFiles.Where(v => v.FormattedTitle == File.FormattedTitle || v.Id == File.Id).ToList();
                foreach (var item in list)
                {
                    item.IsFavorite = IsFavorite;
                }
                db.SaveChanges();
            });
            _onToggleFavorite?.Invoke(this);
        }

        [RelayCommand]
        public async Task ToggleWatchedAsync()
        {
            bool targetState = !IsWatched;
            IsWatched = targetState;
            await Task.Run(() =>
            {
                using var db = new AppDbContext();
                string titleLower = (File.FormattedTitle ?? "").ToLower();
                var list = db.VideoFiles
                    .Where(v => (v.MediaType == "Series" && (v.FormattedTitle ?? "").ToLower() == titleLower) || v.Id == File.Id)
                    .ToList();
                foreach (var item in list)
                {
                    item.IsWatched = targetState;
                    item.WatchProgressPercent = targetState ? 100 : 0;
                    item.WatchProgressSeconds = 0;
                }
                db.SaveChanges();
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            });
        }

        [RelayCommand]
        public async Task ToggleHiddenAsync()
        {
            bool targetState = !IsHidden;
            IsHidden = targetState;
            await Task.Run(() =>
            {
                using var db = new AppDbContext();
                string titleLower = (File.FormattedTitle ?? "").ToLower();
                var list = db.VideoFiles
                    .Where(v => (v.MediaType == "Series" && (v.FormattedTitle ?? "").ToLower() == titleLower) || v.Id == File.Id)
                    .ToList();
                foreach (var item in list)
                {
                    item.IsHidden = targetState;
                }
                db.SaveChanges();
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            });
        }

        [RelayCommand]
        public void OpenManageTags()
        {
            _onManageTags?.Invoke(this);
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
            OnPropertyChanged(nameof(IsWatched));
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(IsHidden));
            OnPropertyChanged(nameof(FormattedYear));
            OnPropertyChanged(nameof(HasYear));
            OnPropertyChanged(nameof(RatingFormatted));
            OnPropertyChanged(nameof(HasRating));
            OnPropertyChanged(nameof(HasDubbing));
            OnPropertyChanged(nameof(AgeRating));
            OnPropertyChanged(nameof(HasAgeRating));
            OnPropertyChanged(nameof(LastPlayedText));
        }
    }
}
