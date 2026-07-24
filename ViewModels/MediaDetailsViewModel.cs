using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Data;
using System.Collections.ObjectModel;
using System.Linq;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class VideoSeasonGroup : ObservableObject
    {
        public string Name { get; set; }
        public int SeasonNumber { get; set; }
        public ObservableCollection<VideoFile> Episodes { get; } = new();
        
        [ObservableProperty]
        private bool _isWatched;
    }

    public partial class MediaDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private VideoFile _media;
        
        public ObservableCollection<VideoSeasonGroup> Seasons { get; } = new();

        [ObservableProperty]
        private bool _isWatched;

        [ObservableProperty]
        private bool _isMovie;

        public bool IsSeries => !IsMovie;
        
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
        
        [ObservableProperty]
        private bool _isFavorite;
        
        public string FavoriteIconKind => IsFavorite ? "Heart" : "HeartOutline";
        public string FavoriteIconColor => IsFavorite ? "#FF4081" : "#888888";

        private readonly ObservableObject _parentViewModel;

        public MediaDetailsViewModel(VideoFile media, ObservableObject parentViewModel = null)
        {
            Media = media;
            _parentViewModel = parentViewModel ?? new MoviesViewModel();
            IsWatched = media.IsWatched;
            IsFavorite = media.IsFavorite;
            IsMovie = media.MediaType != "Series";
            if (!IsMovie)
            {
                LoadSeriesTrackerInfo();
            }

            LoadEpisodes();
        }
        
        partial void OnIsFavoriteChanged(bool value)
        {
            OnPropertyChanged(nameof(FavoriteIconKind));
            OnPropertyChanged(nameof(FavoriteIconColor));
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
                
            Seasons.Clear();
            var grouped = episodes.GroupBy(e => e.Season ?? 1).OrderBy(g => g.Key);
            
            bool allWatched = true;
            foreach (var g in grouped)
            {
                var seasonGroup = new VideoSeasonGroup
                {
                    SeasonNumber = g.Key,
                    Name = $"فصل {g.Key}",
                    IsWatched = g.All(e => e.IsWatched)
                };
                foreach (var ep in g)
                {
                    seasonGroup.Episodes.Add(ep);
                    if (!ep.IsWatched) allWatched = false;
                }
                Seasons.Add(seasonGroup);
            }
            HasEpisodes = Seasons.Any();
            
            if (HasEpisodes)
            {
                IsWatched = allWatched;
                Media.IsWatched = allWatched;
            }
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
        private void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
            using var db = new AppDbContext();
            var filesToUpdate = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower()).ToList();
            foreach (var f in filesToUpdate)
            {
                f.IsFavorite = IsFavorite;
            }
            db.SaveChanges();
            Media.IsFavorite = IsFavorite;
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
        private async Task PlayMovie()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Media.FilePath) && System.IO.File.Exists(Media.FilePath))
                {
                    // Default OS Player
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Media.FilePath) { UseShellExecute = true });
                    if (!IsWatched) ToggleWatched(); // Auto mark as watched when played
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error playing movie", ex);
            }
        }

        [RelayCommand]
        private void ToggleEpisodeWatched(VideoFile episode)
        {
            if (episode == null) return;
            
            Task.Run(() =>
            {
                using var db = new AppDbContext();
                var dbEp = db.VideoFiles.FirstOrDefault(e => e.Id == episode.Id);
                if (dbEp != null)
                {
                    dbEp.IsWatched = episode.IsWatched;
                    dbEp.WatchProgressPercent = episode.IsWatched ? 100 : 0;
                    dbEp.WatchProgressSeconds = 0;
                    db.SaveChanges();
                    
                    App.Current.Dispatcher.Invoke(() => 
                    {
                        episode.WatchProgressPercent = dbEp.WatchProgressPercent;
                        episode.WatchProgressSeconds = dbEp.WatchProgressSeconds;
                        
                        if (HasEpisodes)
                        {
                            var allEps = Seasons.SelectMany(s => s.Episodes).ToList();
                            bool allWatched = allEps.All(e => e.IsWatched);
                            if (IsWatched != allWatched)
                            {
                                IsWatched = allWatched;
                                Media.IsWatched = allWatched;
                            }
                            var seasonGroup = Seasons.FirstOrDefault(s => s.SeasonNumber == episode.Season);
                            if (seasonGroup != null)
                            {
                                seasonGroup.IsWatched = seasonGroup.Episodes.All(e => e.IsWatched);
                            }
                        }
                    });
                }
            });
        }

        [RelayCommand]
        private void ToggleSeasonWatched(VideoSeasonGroup seasonGroup)
        {
            if (seasonGroup == null) return;
            Task.Run(() =>
            {
                using var db = new AppDbContext();
                foreach (var ep in seasonGroup.Episodes)
                {
                    var dbEp = db.VideoFiles.FirstOrDefault(v => v.Id == ep.Id);
                    if (dbEp != null)
                    {
                        dbEp.IsWatched = seasonGroup.IsWatched;
                        dbEp.WatchProgressPercent = seasonGroup.IsWatched ? 100 : 0;
                        dbEp.WatchProgressSeconds = 0;
                    }
                }
                db.SaveChanges();
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var ep in seasonGroup.Episodes)
                    {
                        ep.IsWatched = seasonGroup.IsWatched;
                        ep.WatchProgressPercent = seasonGroup.IsWatched ? 100 : 0;
                        ep.WatchProgressSeconds = 0;
                    }
                    
                    var allEps = Seasons.SelectMany(s => s.Episodes).ToList();
                    bool allWatched = allEps.All(e => e.IsWatched);
                    if (IsWatched != allWatched)
                    {
                        IsWatched = allWatched;
                        Media.IsWatched = allWatched;
                    }
                });
            });
        }

        [RelayCommand]
        private async Task PlayLastEpisode()
        {
            if (Seasons.Count == 0) return;
            var allEps = Seasons.SelectMany(s => s.Episodes).ToList();
            var targetEpisode = allEps.FirstOrDefault(e => !e.IsWatched && e.WatchProgressPercent < 100) ?? allEps.LastOrDefault();
            if (targetEpisode != null)
            {
                await PlayEpisode(targetEpisode);
            }
        }

        [RelayCommand]
        private async Task PlayEpisode(VideoFile episode)
        {
            try
            {
                if (episode != null && !string.IsNullOrWhiteSpace(episode.FilePath) && System.IO.File.Exists(episode.FilePath))
                {
                    // Default OS Player
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(episode.FilePath) { UseShellExecute = true });
                    using var db = new AppDbContext();
                    var dbEp = db.VideoFiles.FirstOrDefault(v => v.Id == episode.Id);
                    if (dbEp != null) {
                        dbEp.IsWatched = true;
                        db.SaveChanges();
                        
                        App.Current.Dispatcher.Invoke(() => {
                            episode.IsWatched = true;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error playing episode", ex);
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            LoadEpisodes();
        }

        [RelayCommand]
        private async Task ChangePosterAsync()
        {
            if (Media.TmdbId == null || Media.TmdbId == 0)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError("شناسه TMDB یافت نشد، امکان واکشی پوستر وجود ندارد"));
                return;
            }

            var service = new IdentifyMediaService();
            var posters = await service.GetMediaPostersAsync(Media.TmdbId.Value, Media.MediaType ?? "Movie");
            
            if (posters == null || posters.Count == 0)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError("پوستر جایگزینی یافت نشد"));
                return;
            }

            var vm = new PosterSelectionViewModel(posters);
            bool posterChanged = false;
            
            App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new MovieManagerDesktop.Views.PosterSelectionDialog(vm);
                dialog.ShowDialog();
                
                if (!string.IsNullOrEmpty(vm.SelectedPosterUrl))
                {
                    posterChanged = true;
                }
            });

            if (posterChanged)
            {
                var savedPath = await service.DownloadAndSaveImageAsync(vm.SelectedPosterUrl, Media.FormattedTitle);
                if (savedPath != null)
                {
                    using var db = new AppDbContext();
                    var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower()).ToList();
                    foreach (var dbFile in dbFiles)
                    {
                        dbFile.PosterUrl = savedPath;
                    }
                    await db.SaveChangesAsync();

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var temp = Media;
                        Media = null;
                        temp.PosterUrl = savedPath;
                        Media = temp;
                        WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                        ToastService.Instance.ShowSuccess("پوستر با موفقیت تغییر کرد");
                    });
                }
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                var service = new IdentifyMediaService();
                var updatedFile = await service.IdentifyMediaAsync(Media);
                
                using var db = new AppDbContext();
                var dbFile = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbFile != null)
                {
                    dbFile.PosterUrl = updatedFile.PosterUrl;
                    dbFile.BackdropUrl = updatedFile.BackdropUrl;
                    dbFile.Year = updatedFile.Year;
                    dbFile.Rating = updatedFile.Rating;
                    dbFile.Overview = updatedFile.Overview;
                    dbFile.Genres = updatedFile.Genres;
                    dbFile.Actors = updatedFile.Actors;
                    dbFile.Director = updatedFile.Director;
                    dbFile.Resolution = updatedFile.Resolution;
                    await db.SaveChangesAsync();
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    Media.PosterUrl = updatedFile.PosterUrl;
                    Media.BackdropUrl = updatedFile.BackdropUrl;
                    Media.Year = updatedFile.Year;
                    Media.Rating = updatedFile.Rating;
                    Media.Overview = updatedFile.Overview;
                    Media.Genres = updatedFile.Genres;
                    Media.Actors = updatedFile.Actors;
                    Media.Director = updatedFile.Director;
                    Media.Resolution = updatedFile.Resolution;
                    
                    OnPropertyChanged(nameof(Media));
                    if (!IsMovie) LoadSeriesTrackerInfo();
                    ToastService.Instance.ShowSuccess("اطلاعات با موفقیت بروزرسانی شد");
                });
            }
            catch (System.Exception ex)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError($"خطا: {ex.Message}"));
            }
        }

        [RelayCommand]
        private void ManualIdentify()
        {
            var searchDialogViewModel = new ApiSearchDialogViewModel(!string.IsNullOrEmpty(Media.FormattedTitle) ? Media.FormattedTitle : Media.FileName);
            var searchDialog = new MovieManagerDesktop.Views.Dialogs.ApiSearchDialog { DataContext = searchDialogViewModel };
            
            searchDialogViewModel.CloseAction = () => searchDialog.Close();
            searchDialogViewModel.SelectAction = async (result) => 
            {
                if (result.Id != 0)
                {
                    using var db = new AppDbContext();
                    var oldTitle = Media.FormattedTitle.ToLower();
                    
                    var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == oldTitle && v.MediaType == Media.MediaType).ToList();
                    foreach (var dbFile in dbFiles)
                    {
                        dbFile.TmdbId = result.Id;
                        dbFile.FormattedTitle = result.Title;
                        
                        // Clear old data so RefreshData fetches fresh data
                        dbFile.Overview = null;
                        dbFile.Rating = null;
                        dbFile.PosterUrl = null;
                        dbFile.BackdropUrl = null;
                        dbFile.Genres = null;
                        dbFile.Actors = null;
                        dbFile.Director = null;
                    }
                    await db.SaveChangesAsync();
                    
                    App.Current.Dispatcher.Invoke(() => {
                        Media.TmdbId = result.Id;
                        Media.FormattedTitle = result.Title;
                        OnPropertyChanged(nameof(Media));
                    });
                    
                    await RefreshDataAsync();
                }
            };
            
            searchDialog.Owner = System.Windows.Application.Current.MainWindow;
            searchDialog.ShowDialog();
        }

        [RelayCommand]
        private void DeleteMovie()
        {
            var result = System.Windows.MessageBox.Show(
                $"آیا مطمئن هستید که می‌خواهید «{Media.FormattedTitle}» را حذف کنید؟",
                "تأیید حذف",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            using var db = new AppDbContext();
            var filesToDelete = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower()).ToList();
            db.VideoFiles.RemoveRange(filesToDelete);
            db.SaveChanges();

            App.Current.Dispatcher.Invoke(() =>
            {
                ToastService.Instance.ShowSuccess("فایل با موفقیت حذف شد");
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MoviesViewModel()));
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            });
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(_parentViewModel));
        }
    }
}
