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
        public string Name { get; set; } = string.Empty;
        public int SeasonNumber { get; set; }
        public ObservableCollection<VideoFile> Episodes { get; } = new();
        
        [ObservableProperty]
        private bool _isWatched;

        [ObservableProperty]
        private bool _isSelected;

        public string SeasonTag => $"S{SeasonNumber}";
        public string SeasonTitle => $"فصل {SeasonNumber}";
        public string EpisodesCountText => $"{Episodes.Count} قسمت";
        public string WatchedSummaryText => $"{Episodes.Count(e => e.IsWatched)} از {Episodes.Count} قسمت";
        public double SeasonProgressPercent => Episodes.Count > 0 ? ((double)Episodes.Count(e => e.IsWatched) / Episodes.Count * 100) : 0;
        public bool AllWatched => Episodes.Count > 0 && Episodes.All(e => e.IsWatched);
        public bool HasDubbing => Episodes.Any(e => MediaDetailsViewModel.CheckDubbing(e));
        public bool HasSubtitle => Episodes.Any(e => MediaDetailsViewModel.CheckSubtitle(e));

        public void NotifyWatchedChanged()
        {
            OnPropertyChanged(nameof(WatchedSummaryText));
            OnPropertyChanged(nameof(SeasonProgressPercent));
            OnPropertyChanged(nameof(AllWatched));
            OnPropertyChanged(nameof(IsWatched));
            OnPropertyChanged(nameof(HasDubbing));
            OnPropertyChanged(nameof(HasSubtitle));
        }
    }

    public partial class MediaDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private VideoFile _media;
        
        public ObservableCollection<VideoSeasonGroup> Seasons { get; } = new();

        [ObservableProperty]
        private VideoSeasonGroup? _selectedSeason;

        partial void OnSelectedSeasonChanged(VideoSeasonGroup? oldValue, VideoSeasonGroup? newValue)
        {
            if (oldValue != null) oldValue.IsSelected = false;
            if (newValue != null) newValue.IsSelected = true;
        }

        [ObservableProperty]
        private bool _isWatched;

        [ObservableProperty]
        private bool _isMovie;

        public bool IsSeries => !IsMovie;
        
        public string EffectiveBackdropUrl => !string.IsNullOrWhiteSpace(Media?.BackdropUrl) ? Media.BackdropUrl : (Media?.PosterUrl ?? string.Empty);
        
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

        // Continue Watching CTA
        [ObservableProperty]
        private string _continueWatchingText = "شروع تماشا";

        [ObservableProperty]
        private bool _canContinueWatching = false;

        [ObservableProperty]
        private VideoFile? _continueWatchingEpisode;

        // Progress Overview
        [ObservableProperty]
        private int _watchedEpisodesCount = 0;

        [ObservableProperty]
        private int _totalEpisodesCount = 0;

        [ObservableProperty]
        private double _overallProgressPercent = 0.0;

        [ObservableProperty]
        private string _watchedProgressSummaryText = string.Empty;

        [ObservableProperty]
        private string _progressPercentText = "0%";
        
        [ObservableProperty]
        private bool _isFavorite;
        
        public string FavoriteIconKind => IsFavorite ? "Heart" : "HeartOutline";
        public string FavoriteIconColor => IsFavorite ? "#FF4081" : "#888888";

        public ObservableCollection<string> GenreList { get; } = new();

        [ObservableProperty]
        private bool _hasDubbing;

        [ObservableProperty]
        private bool _hasSubtitle;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        public bool IsTabEpisodesSelected => SelectedTabIndex == 0;
        public bool IsTabCastSelected => SelectedTabIndex == 1;
        public bool IsTabTrackerSelected => SelectedTabIndex == 2;

        partial void OnSelectedTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsTabEpisodesSelected));
            OnPropertyChanged(nameof(IsTabCastSelected));
            OnPropertyChanged(nameof(IsTabTrackerSelected));
        }

        [RelayCommand]
        private void SelectTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int index))
            {
                SelectedTabIndex = index;
            }
        }

        private readonly ObservableObject _parentViewModel;

        public MediaDetailsViewModel(VideoFile media, ObservableObject parentViewModel = null)
        {
            Media = media;
            _parentViewModel = parentViewModel ?? new MoviesViewModel();
            IsWatched = media.IsWatched;
            IsFavorite = media.IsFavorite;
            IsMovie = media.MediaType != "Series";
            HasDubbing = CheckDubbing(media);
            HasSubtitle = CheckSubtitle(media);

            PopulateGenreList();

            if (!IsMovie)
            {
                LoadSeriesTrackerInfo();
            }

            LoadEpisodes();

            // Real-time synchronization when episodes are played or watched
            WeakReferenceMessenger.Default.Register<MediaUpdatedMessage>(this, (r, m) =>
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    RefreshEpisodesWatchedState();
                });
            });
        }

        public void RefreshEpisodesWatchedState()
        {
            if (Media.MediaType != "Series") return;
            try
            {
                using var db = new AppDbContext();
                var episodesInDb = db.VideoFiles
                    .Where(v => v.MediaType == "Series" && v.FormattedTitle.ToLower() == Media.FormattedTitle.ToLower())
                    .ToList();

                foreach (var season in Seasons)
                {
                    foreach (var ep in season.Episodes)
                    {
                        var dbEp = episodesInDb.FirstOrDefault(d => d.Id == ep.Id || d.FilePath == ep.FilePath);
                        if (dbEp != null)
                        {
                            ep.IsWatched = dbEp.IsWatched;
                            ep.WatchProgressPercent = dbEp.WatchProgressPercent;
                            ep.WatchProgressSeconds = dbEp.WatchProgressSeconds;
                        }
                    }
                    season.NotifyWatchedChanged();
                }

                UpdateProgressAndContinueWatching(db);
            }
            catch { }
        }

        public static bool CheckDubbing(VideoFile file, IEnumerable<VideoFile>? allEpisodes = null)
        {
            if (file == null) return false;
            if (file.HasDubbing) return true;
            var list = new List<VideoFile> { file };
            if (allEpisodes != null) list.AddRange(allEpisodes);

            foreach (var item in list)
            {
                if (item.HasDubbing) return true;
                string text = $"{item.FileName} {item.FilePath}".ToLowerInvariant();
                if (text.Contains("dub") || text.Contains("دوبله") || text.Contains("farsi") || 
                    text.Contains("persian") || text.Contains("dual") || text.Contains("multi"))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckSubtitle(VideoFile file, IEnumerable<VideoFile>? allEpisodes = null)
        {
            if (file.HasSubtitle) return true;
            var list = new List<VideoFile> { file };
            if (allEpisodes != null) list.AddRange(allEpisodes);

            foreach (var item in list)
            {
                if (item.HasSubtitle) return true;
                string text = $"{item.FileName} {item.FilePath}".ToLowerInvariant();
                if (text.Contains("sub") || text.Contains("زیرنویس") || text.Contains("softsub") || text.Contains("hardsub"))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(item.FilePath))
                {
                    try
                    {
                        string? dir = System.IO.Path.GetDirectoryName(item.FilePath);
                        if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                        {
                            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(item.FilePath);
                            if (System.IO.File.Exists(System.IO.Path.Combine(dir, nameWithoutExt + ".srt")) ||
                                System.IO.File.Exists(System.IO.Path.Combine(dir, nameWithoutExt + ".vtt")) ||
                                System.IO.File.Exists(System.IO.Path.Combine(dir, nameWithoutExt + ".ass")) ||
                                System.IO.Directory.Exists(System.IO.Path.Combine(dir, "Subs")) ||
                                System.IO.Directory.Exists(System.IO.Path.Combine(dir, "subtitles")) ||
                                System.IO.Directory.EnumerateFiles(dir, "*.srt").Any())
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }
            }
            return false;
        }

        private void PopulateGenreList()
        {
            GenreList.Clear();
            if (!string.IsNullOrWhiteSpace(Media?.Genres))
            {
                var genres = Media.Genres.Split(new[] { ',', '،', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var g in genres)
                {
                    var trimmed = g.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        var fa = GenreTranslatorService.Translate(trimmed);
                        if (!GenreList.Contains(fa))
                        {
                            GenreList.Add(fa);
                        }
                    }
                }
            }
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
                
            HasDubbing = CheckDubbing(Media, episodes);
            HasSubtitle = CheckSubtitle(Media, episodes);

            Seasons.Clear();
            var grouped = episodes.GroupBy(e => e.Season ?? 1).OrderBy(g => g.Key);
            
            VideoSeasonGroup? firstGroup = null;
            VideoSeasonGroup? groupWithUnwatched = null;

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
                    if (!ep.IsWatched && groupWithUnwatched == null)
                    {
                        groupWithUnwatched = seasonGroup;
                    }
                }
                
                if (firstGroup == null) firstGroup = seasonGroup;
                Seasons.Add(seasonGroup);
            }
            HasEpisodes = Seasons.Any();
            SelectedSeason = groupWithUnwatched ?? firstGroup;
            if (SelectedSeason != null) SelectedSeason.IsSelected = true;
            
            UpdateProgressAndContinueWatching(db);
        }

        private void UpdateProgressAndContinueWatching(AppDbContext? existingDb = null)
        {
            bool ownDb = existingDb == null;
            var db = existingDb ?? new AppDbContext();
            try
            {
                int total = 0;
                int watched = 0;
                VideoFile? nextUnwatched = null;
                VideoFile? firstEpisode = null;

                foreach (var s in Seasons.OrderBy(x => x.SeasonNumber))
                {
                    foreach (var ep in s.Episodes.OrderBy(x => x.Episode))
                    {
                        if (firstEpisode == null) firstEpisode = ep;
                        total++;
                        if (ep.IsWatched)
                        {
                            watched++;
                        }
                        else if (nextUnwatched == null)
                        {
                            nextUnwatched = ep;
                        }
                    }
                    s.NotifyWatchedChanged();
                }

                WatchedEpisodesCount = watched;
                TotalEpisodesCount = total;
                OverallProgressPercent = total > 0 ? ((double)watched / total * 100) : 0;
                ProgressPercentText = $"{Math.Round(OverallProgressPercent)}٪";
                WatchedProgressSummaryText = $"شما {watched} از {total} قسمت را دیده‌اید";

                if (nextUnwatched != null)
                {
                    ContinueWatchingEpisode = nextUnwatched;
                    ContinueWatchingText = $"ادامه تماشا (فصل {nextUnwatched.Season ?? 1} قسمت {nextUnwatched.Episode ?? 1})";
                    CanContinueWatching = true;
                }
                else if (firstEpisode != null)
                {
                    ContinueWatchingEpisode = firstEpisode;
                    ContinueWatchingText = $"تماشای مجدد (فصل {firstEpisode.Season ?? 1} قسمت {firstEpisode.Episode ?? 1})";
                    CanContinueWatching = true;
                }
                else
                {
                    ContinueWatchingEpisode = null;
                    ContinueWatchingText = "شروع تماشا";
                    CanContinueWatching = false;
                }

                bool allWatched = total > 0 && watched == total;
                IsWatched = allWatched;

                if (IsMovie)
                {
                    Media.IsWatched = allWatched;
                    var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                    if (dbMedia != null)
                    {
                        dbMedia.WatchProgressPercent = OverallProgressPercent;
                        dbMedia.IsWatched = allWatched;
                        db.SaveChanges();
                    }
                }
            }
            finally
            {
                if (ownDb) db.Dispose();
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
                FirstAirDateText = DateTimeFormatterService.FormatShortDate(Media.FirstAirDate.Value);
            }
            else if (!string.IsNullOrWhiteSpace(Media.Year))
            {
                FirstAirDateText = DateTimeFormatterService.FormatYear(Media.Year);
            }
            else
            {
                FirstAirDateText = "نامشخص";
            }

            if (Media.LastAirDate.HasValue)
            {
                LastAirDateText = DateTimeFormatterService.FormatShortDate(Media.LastAirDate.Value);
            }
            else
            {
                LastAirDateText = "نامشخص";
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
                string dateStr = Media.NextEpisodeDate ?? "";
                NextEpisodeText = $"قسمت {Media.NextEpisodeNumber} - {DateTimeFormatterService.FormatDate(dateStr)}";
            }
            else if (Media.SeriesStatus == "Currently Airing")
            {
                NextEpisodeText = "به زودی اعلام میشود";
            }
        }

        public string FormattedGenres => GenreTranslatorService.TranslateList(Media.Genres);
        public string FormattedYear => DateTimeFormatterService.FormatYear(Media.Year);



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
        private void PlayMovie()
        {
            if (Media != null)
            {
                PlaybackService.PlayMedia(Media);
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Media.FilePath))
                {
                    string targetPath = Media.FilePath;
                    
                    if (System.IO.File.Exists(targetPath))
                    {
                        // It's a file, get its directory
                        targetPath = System.IO.Path.GetDirectoryName(targetPath);
                    }
                    else if (!System.IO.Directory.Exists(targetPath))
                    {
                        // Neither a valid file nor directory
                        MovieManagerDesktop.Services.ToastService.Instance.ShowError("مسیر مورد نظر یافت نشد.");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(targetPath) && System.IO.Directory.Exists(targetPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = targetPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Error opening folder", ex);
            }
        }

        [RelayCommand]
        private void SelectSeason(VideoSeasonGroup? season)
        {
            if (season == null) return;
            foreach (var s in Seasons)
            {
                s.IsSelected = (s == season);
            }
            SelectedSeason = season;
        }

        [RelayCommand]
        private void ToggleSelectedSeasonAllWatched()
        {
            if (SelectedSeason == null) return;
            ToggleSeasonWatched(SelectedSeason);
        }

        [RelayCommand]
        private void ContinueWatching()
        {
            if (ContinueWatchingEpisode != null)
            {
                PlayEpisode(ContinueWatchingEpisode);
            }
            else
            {
                PlayLastEpisode();
            }
        }

        [RelayCommand]
        private void ToggleEpisodeWatched(VideoFile episode)
        {
            if (episode == null) return;
            
            episode.IsWatched = !episode.IsWatched;
            episode.WatchProgressPercent = episode.IsWatched ? 100 : 0;
            episode.WatchProgressSeconds = 0;

            Task.Run(() =>
            {
                using var db = new AppDbContext();
                var dbEp = db.VideoFiles.FirstOrDefault(e => e.Id == episode.Id);
                if (dbEp != null)
                {
                    dbEp.IsWatched = episode.IsWatched;
                    dbEp.WatchProgressPercent = episode.WatchProgressPercent;
                    dbEp.WatchProgressSeconds = 0;
                    db.SaveChanges();
                }

                App.Current.Dispatcher.Invoke(() => 
                {
                    UpdateProgressAndContinueWatching(db);
                });
            });
        }

        [RelayCommand]
        private void ToggleSeasonWatched(VideoSeasonGroup seasonGroup)
        {
            if (seasonGroup == null) return;
            
            bool newWatchedState = !seasonGroup.AllWatched;
            seasonGroup.IsWatched = newWatchedState;

            foreach (var ep in seasonGroup.Episodes)
            {
                ep.IsWatched = newWatchedState;
                ep.WatchProgressPercent = newWatchedState ? 100 : 0;
                ep.WatchProgressSeconds = 0;
            }

            seasonGroup.NotifyWatchedChanged();

            Task.Run(() =>
            {
                using var db = new AppDbContext();
                foreach (var ep in seasonGroup.Episodes)
                {
                    var dbEp = db.VideoFiles.FirstOrDefault(v => v.Id == ep.Id);
                    if (dbEp != null)
                    {
                        dbEp.IsWatched = newWatchedState;
                        dbEp.WatchProgressPercent = newWatchedState ? 100 : 0;
                        dbEp.WatchProgressSeconds = 0;
                    }
                }
                db.SaveChanges();
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateProgressAndContinueWatching(db);
                });
            });
        }

        [RelayCommand]
        private void PlayLastEpisode()
        {
            if (Seasons.Count == 0) return;
            var allEps = Seasons.SelectMany(s => s.Episodes).ToList();
            var targetEpisode = allEps.FirstOrDefault(e => !e.IsWatched && e.WatchProgressPercent < 100) ?? allEps.LastOrDefault();
            if (targetEpisode != null)
            {
                PlayEpisode(targetEpisode);
            }
        }

        [RelayCommand]
        private void PlayEpisode(VideoFile episode)
        {
            if (episode != null)
            {
                var playlist = Episodes?.ToList() ?? new List<VideoFile> { episode };
                int idx = playlist.IndexOf(episode);
                PlaybackService.PlayMedia(episode, playlist, Math.Max(0, idx));
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
                LoggerService.Info($"[صفحه جزییات] شروع بروزرسانی اطلاعات برای: {Media.FormattedTitle}");
                var service = new IdentifyMediaService();
                var updatedFile = await service.IdentifyMediaAsync(Media);
                
                using (var db = new AppDbContext())
                {
                    var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle == Media.FormattedTitle || v.Id == Media.Id).ToList();
                    foreach (var dbFile in dbFiles)
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
                        dbFile.FirstAirDate = updatedFile.FirstAirDate;
                        dbFile.LastAirDate = updatedFile.LastAirDate;
                        dbFile.NetworkName = updatedFile.NetworkName;
                        dbFile.AirDay = updatedFile.AirDay;
                        dbFile.AirTime = updatedFile.AirTime;
                        dbFile.TotalSeasonsCount = updatedFile.TotalSeasonsCount ?? updatedFile.NumberOfSeasons;
                        dbFile.TotalEpisodesCount = updatedFile.TotalEpisodesCount ?? updatedFile.NumberOfEpisodes;
                        dbFile.NumberOfSeasons = updatedFile.NumberOfSeasons ?? updatedFile.TotalSeasonsCount;
                        dbFile.NumberOfEpisodes = updatedFile.NumberOfEpisodes ?? updatedFile.TotalEpisodesCount;
                        dbFile.NextEpisodeDate = updatedFile.NextEpisodeDate;
                        dbFile.NextEpisodeSeason = updatedFile.NextEpisodeSeason;
                        dbFile.NextEpisodeNumber = updatedFile.NextEpisodeNumber;
                        dbFile.SeriesStatus = updatedFile.SeriesStatus;
                        dbFile.CollectionName = updatedFile.CollectionName;
                    }
                    await db.SaveChangesAsync();

                    if (!IsMovie && updatedFile.TmdbId.HasValue)
                    {
                        var (sList, eList) = await service.FetchSeriesDetailsAsync(updatedFile.TmdbId.Value);
                        if (sList.Count > 0)
                        {
                            var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == updatedFile.TmdbId.Value).ToList();
                            var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == updatedFile.TmdbId.Value).ToList();
                            db.TvSeasons.RemoveRange(oldS);
                            db.TvEpisodes.RemoveRange(oldE);
                            db.TvSeasons.AddRange(sList);
                            db.TvEpisodes.AddRange(eList);
                            await db.SaveChangesAsync();
                        }
                    }
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
                    Media.FirstAirDate = updatedFile.FirstAirDate;
                    Media.LastAirDate = updatedFile.LastAirDate;
                    Media.NetworkName = updatedFile.NetworkName;
                    Media.AirDay = updatedFile.AirDay;
                    Media.AirTime = updatedFile.AirTime;
                    Media.TotalSeasonsCount = updatedFile.TotalSeasonsCount;
                    Media.TotalEpisodesCount = updatedFile.TotalEpisodesCount;
                    Media.NextEpisodeDate = updatedFile.NextEpisodeDate;
                    Media.NextEpisodeNumber = updatedFile.NextEpisodeNumber;
                    Media.SeriesStatus = updatedFile.SeriesStatus;
                    Media.CollectionName = updatedFile.CollectionName;
                    
                    OnPropertyChanged(nameof(Media));
                    OnPropertyChanged(nameof(FirstAirDateText));
                    OnPropertyChanged(nameof(LastAirDateText));
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
            
            WindowHelper.SafeShowDialog(searchDialog);
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
        private async System.Threading.Tasks.Task UpdateSeriesTrackerAsync()
        {
            if (Media.TmdbId == null) return;
            
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = SettingsManager.GetTmdbApiKey();
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;
                
                LoggerService.Info($"[صفحه جزییات] بروزرسانی اطلاعات ردیاب: {Media.FormattedTitle}...");
                var service = new IdentifyMediaService();
                await service.IdentifySeriesDetailsAsync(Media, apiKey, language);
                
                using var db = new AppDbContext();
                var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbSeries != null)
                {
                    dbSeries.SeriesStatus = Media.SeriesStatus;
                    dbSeries.FirstAirDate = Media.FirstAirDate;
                    dbSeries.LastAirDate = Media.LastAirDate;
                    dbSeries.NetworkName = Media.NetworkName;
                    dbSeries.AirDay = Media.AirDay;
                    dbSeries.AirTime = Media.AirTime;
                    dbSeries.NextEpisodeDate = Media.NextEpisodeDate;
                    dbSeries.NextEpisodeNumber = Media.NextEpisodeNumber;
                    dbSeries.TotalSeasonsCount = Media.TotalSeasonsCount;
                    dbSeries.TotalEpisodesCount = Media.TotalEpisodesCount;
                    
                    service.CleanTrackerInfoFromOverview(Media);
                    dbSeries.Overview = Media.Overview;
                    
                    await db.SaveChangesAsync();
                }
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadSeriesTrackerInfo();
                    OnPropertyChanged(nameof(Media));
                    ToastService.Instance.ShowSuccess("اطلاعات ردیاب بروزرسانی شد");
                });
            }
            catch (System.Exception ex)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError($"خطا: {ex.Message}"));
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(_parentViewModel));
        }

        [RelayCommand]
        private async Task TranslatePlotAsync()
        {
            if (string.IsNullOrWhiteSpace(Media.Overview)) return;
            
            try
            {
                MovieManagerDesktop.Services.ToastService.Instance.ShowSuccess("در حال ترجمه متن...");
                string translatedText = await MovieManagerDesktop.Services.TranslationService.TranslateTextAsync(Media.Overview);
                
                if (!string.IsNullOrWhiteSpace(translatedText) && translatedText != Media.Overview)
                {
                    using var db = new AppDbContext();
                    var dbFile = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                    if (dbFile != null)
                    {
                        dbFile.Overview = translatedText;
                        await db.SaveChangesAsync();
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Media.Overview = translatedText;
                        OnPropertyChanged(nameof(Media));
                        MovieManagerDesktop.Services.ToastService.Instance.ShowSuccess("ترجمه با موفقیت انجام شد.");
                    });
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error("Translation error", ex);
                App.Current.Dispatcher.Invoke(() => MovieManagerDesktop.Services.ToastService.Instance.ShowError("خطا در ترجمه متن."));
            }
        }
    }
}
