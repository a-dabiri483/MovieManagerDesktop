using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Data;
using System.Collections.ObjectModel;
using System.Linq;

namespace MovieManagerDesktop.ViewModels
{
    public partial class MediaDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private VideoFile _media;
        
        [ObservableProperty]
        private bool _isWatched;

        [ObservableProperty]
        private bool _isMovie;
        
        public ObservableCollection<VideoFile> Episodes { get; } = new();
        
        [ObservableProperty]
        private bool _hasEpisodes;
        
        [ObservableProperty]
        private string _seriesStatusText = string.Empty;

        [ObservableProperty]
        private string _seriesStatusColor = "#00E5FF";

        [ObservableProperty]
        private string _firstAirDateText = "نامشخص";

        [ObservableProperty]
        private string _lastAirDateText = "نامشخص";

        [ObservableProperty]
        private string _networkText = "نامشخص";

        [ObservableProperty]
        private string _airScheduleText = "نامشخص";

        [ObservableProperty]
        private string _nextEpisodeText = "نامشخص";

        [ObservableProperty]
        private string _episodesInfoText = "نامشخص";

        [ObservableProperty]
        private bool _showSeriesTracker = false;
        
        private readonly ObservableObject _parentViewModel;

        public MediaDetailsViewModel(VideoFile media, ObservableObject parentViewModel = null)
        {
            Media = media;
            _parentViewModel = parentViewModel ?? new MoviesViewModel();
            IsWatched = media.IsWatched;
            IsMovie = media.MediaType != "Series";
            if (!IsMovie)
            {
                LoadSeriesTrackerInfo();
            }

            LoadEpisodes();
        }

        private void LoadEpisodes()
        {
            if (Media.MediaType != "Series") return;
            
            using var db = new AppDbContext();
            var episodes = db.VideoFiles
                .Where(v => v.MediaType == "Series" && v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower())
                .OrderBy(v => v.Season)
                .ThenBy(v => v.Episode)
                .ToList();
                
            Episodes.Clear();
            foreach (var ep in episodes)
            {
                Episodes.Add(ep);
            }
            HasEpisodes = Episodes.Any();
        }

        private void LoadSeriesTrackerInfo()
        {
            if (Media.MediaType != "Series") return;
            
            ShowSeriesTracker = true;

            if (!string.IsNullOrEmpty(Media.SeriesStatus))
            {
                string rawStatus = Media.SeriesStatus.Trim().ToLowerInvariant();
                SeriesStatusText = rawStatus switch
                {
                    "returning series" => "در حال پخش",
                    "ended" => "تمام شده",
                    "cancelled" => "کنسل شده",
                    "canceled" => "کنسل شده",
                    "planned" => "برنامه‌ریزی شده",
                    "currently airing" => "در حال پخش",
                    "finished airing" => "تمام شده",
                    "not yet aired" => "پخش نشده",
                    _ => Media.SeriesStatus
                };

                SeriesStatusColor = rawStatus switch
                {
                    "returning series" => "#4CAF50",
                    "ended" => "#FF9800",
                    "cancelled" => "#FF5252",
                    "canceled" => "#FF5252",
                    "planned" => "#2196F3",
                    "currently airing" => "#4CAF50",
                    "finished airing" => "#FF9800",
                    "not yet aired" => "#2196F3",
                    _ => "#00E5FF"
                };
            }

            if (Media.FirstAirDate.HasValue)
            {
                FirstAirDateText = Media.FirstAirDate.Value.ToString("yyyy/MM/dd");
            }

            if (Media.LastAirDate.HasValue)
            {
                LastAirDateText = Media.LastAirDate.Value.ToString("yyyy/MM/dd");
            }

            if (!string.IsNullOrEmpty(Media.NetworkName))
            {
                NetworkText = Media.NetworkName;
            }

            if (!string.IsNullOrEmpty(Media.AirDay))
            {
                string rawDay = Media.AirDay.ToLowerInvariant();
                var dayText = Media.AirDay;
                
                if (rawDay.Contains("saturday")) dayText = "شنبه";
                else if (rawDay.Contains("sunday")) dayText = "یکشنبه";
                else if (rawDay.Contains("monday")) dayText = "دوشنبه";
                else if (rawDay.Contains("tuesday")) dayText = "سه‌شنبه";
                else if (rawDay.Contains("wednesday")) dayText = "چهارشنبه";
                else if (rawDay.Contains("thursday")) dayText = "پنجشنبه";
                else if (rawDay.Contains("friday")) dayText = "جمعه";
                
                AirScheduleText = dayText;
                if (!string.IsNullOrEmpty(Media.AirTime))
                    AirScheduleText += $" - ساعت {Media.AirTime}";
            }

            var seasons = Media.TotalSeasonsCount ?? Media.NumberOfSeasons ?? 0;
            var episodes = Media.TotalEpisodesCount ?? Media.NumberOfEpisodes ?? 0;
            EpisodesInfoText = episodes > 0 ? $"{seasons} فصل - {episodes} قسمت" : "تعداد قسمتها نامشخص";

            if (!string.IsNullOrEmpty(Media.NextEpisodeDate))
            {
                // In VideoFile.cs NextEpisodeDate is string
                string dateStr = Media.NextEpisodeDate ?? "";
                NextEpisodeText = $"قسمت {Media.NextEpisodeNumber} - {dateStr}";
            }
            else if (Media.SeriesStatus == "Currently Airing")
            {
                NextEpisodeText = "به زودی اعلام میشود";
            }
        }

        [RelayCommand]
        private void ToggleWatched()
        {
            IsWatched = !IsWatched;
            using var db = new AppDbContext();
            var filesToUpdate = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower()).ToList();
            foreach (var f in filesToUpdate)
            {
                f.IsWatched = IsWatched;
            }
            db.SaveChanges();
            Media.IsWatched = IsWatched;
        }


        [RelayCommand]
        private void PlayMovie()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Media.FilePath) && System.IO.File.Exists(Media.FilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Media.FilePath) { UseShellExecute = true });
                    
                    if (!IsWatched) ToggleWatched(); // Auto mark as watched when played
                }
            }
            catch { }
        }

        [RelayCommand]
        private void PlayEpisode(VideoFile episode)
        {
            try
            {
                if (episode != null && !string.IsNullOrWhiteSpace(episode.FilePath) && System.IO.File.Exists(episode.FilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(episode.FilePath) { UseShellExecute = true });
                    
                    using var db = new AppDbContext();
                    var dbEp = db.VideoFiles.FirstOrDefault(v => v.Id == episode.Id);
                    if (dbEp != null) {
                        dbEp.IsWatched = true;
                        db.SaveChanges();
                        episode.IsWatched = true;
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        private void Refresh()
        {
            LoadEpisodes();
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(_parentViewModel));
        }
    }
}
