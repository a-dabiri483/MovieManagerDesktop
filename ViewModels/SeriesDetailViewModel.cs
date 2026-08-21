using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class EpisodeItemViewModel : ObservableObject
    {
        public TvEpisode Episode { get; set; } = null!;
        public VideoFile? LocalFile { get; set; }
        public string SeriesTitle { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isWatched;

        public int EpisodeNumber => Episode.EpisodeNumber;
        public int SeasonNumber => Episode.SeasonNumber;
        public string EpisodeTag => $"S{SeasonNumber:D2}E{EpisodeNumber:D2}";
        public string DisplayTitle => string.IsNullOrWhiteSpace(Episode.Name) ? SeriesTitle : Episode.Name;
        public string AirDateFormatted => string.IsNullOrWhiteSpace(Episode.AirDate) ? "" : DateTimeFormatterService.FormatDate(Episode.AirDate);
        public string StillUrl => Episode.StillUrl;
        public bool HasLocalFile => LocalFile != null && !string.IsNullOrEmpty(LocalFile.FilePath) && File.Exists(LocalFile.FilePath);
        public bool HasDubbing => LocalFile != null && LocalFile.HasDubbing;
        public bool HasSubtitle => LocalFile != null && LocalFile.HasSubtitle;
    }

    public partial class SeasonGroup : ObservableObject
    {
        public TvSeason? Season { get; set; }
        public ObservableCollection<EpisodeItemViewModel> Episodes { get; set; } = new();

        [ObservableProperty]
        private bool _isExpanded = false;

        public string ChevronIconKind => IsExpanded ? "ChevronUp" : "ChevronDown";

        partial void OnIsExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ChevronIconKind));
        }

        public int SeasonNumber => Season?.SeasonNumber ?? 0;
        public string SeasonTag => $"S{SeasonNumber}";
        public string SeasonTitle => $"فصل {SeasonNumber} - {Season?.EpisodeCount ?? Episodes.Count} قسمت";
        
        public string WatchedSummaryText => $"{Episodes.Count(e => e.IsWatched)} از {Season?.EpisodeCount ?? Episodes.Count} قسمت";
        
        public double SeasonProgressPercent => (Season?.EpisodeCount > 0) ? ((double)Episodes.Count(e => e.IsWatched) / Season.EpisodeCount * 100) : 0;
        
        public bool AllWatched => Episodes.Count > 0 && Episodes.All(e => e.IsWatched);

        public void NotifyWatchedChanged()
        {
            OnPropertyChanged(nameof(WatchedSummaryText));
            OnPropertyChanged(nameof(SeasonProgressPercent));
            OnPropertyChanged(nameof(AllWatched));
        }
    }

    public partial class SeriesDetailViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _mediaService;

        [ObservableProperty]
        private VideoFile _series;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;
        
        // Series Tracker UI Properties
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

        // Continue Watching CTA Properties
        [ObservableProperty]
        private string _continueWatchingText = "شروع تماشا";

        [ObservableProperty]
        private bool _canContinueWatching = false;

        [ObservableProperty]
        private EpisodeItemViewModel? _continueWatchingEpisode;

        // Progress Overview Properties
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

        public ObservableCollection<SeasonGroup> Seasons { get; } = new();

        [ObservableProperty]
        private bool _isFavorite;
        
        public string FavoriteIconKind => IsFavorite ? "Heart" : "HeartOutline";
        public string FavoriteIconColor => IsFavorite ? "#FF4081" : "#888888";

        partial void OnIsFavoriteChanged(bool value)
        {
            OnPropertyChanged(nameof(FavoriteIconKind));
            OnPropertyChanged(nameof(FavoriteIconColor));
        }

        public SeriesDetailViewModel(VideoFile series)
        {
            _series = series;
            _mediaService = new IdentifyMediaService();
            _isFavorite = series.IsFavorite;

            LoadSeriesTrackerInfo();
            _ = LoadDetailsAsync();
        }
        
        private void LoadSeriesTrackerInfo()
        {
            if (!string.IsNullOrEmpty(Series.SeriesStatus))
            {
                var language = SettingsManager.LoadSettings().TmdbLanguage ?? "fa-IR";
                string rawStatus = Series.SeriesStatus.Trim().ToLowerInvariant();

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
                    _ => Series.SeriesStatus
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
            if (Series.FirstAirDate.HasValue)
                FirstAirDateText = DateTimeFormatterService.FormatShortDate(Series.FirstAirDate.Value);
            else if (!string.IsNullOrWhiteSpace(Series.Year))
                FirstAirDateText = DateTimeFormatterService.FormatYear(Series.Year);
            
            // Last Air Date
            if (Series.LastAirDate.HasValue)
                LastAirDateText = DateTimeFormatterService.FormatShortDate(Series.LastAirDate.Value);
            
            // Network
            if (!string.IsNullOrEmpty(Series.NetworkName))
                NetworkText = Series.NetworkName;
            
            // Air Schedule
            if (!string.IsNullOrEmpty(Series.AirDay))
            {
                string rawDay = Series.AirDay.ToLowerInvariant();
                var dayText = Series.AirDay;
                
                if (rawDay.Contains("saturday")) dayText = "شنبه";
                else if (rawDay.Contains("sunday")) dayText = "یکشنبه";
                else if (rawDay.Contains("monday")) dayText = "دوشنبه";
                else if (rawDay.Contains("tuesday")) dayText = "سه‌شنبه";
                else if (rawDay.Contains("wednesday")) dayText = "چهارشنبه";
                else if (rawDay.Contains("thursday")) dayText = "پنجشنبه";
                else if (rawDay.Contains("friday")) dayText = "جمعه";
                
                AirScheduleText = dayText;
                if (!string.IsNullOrEmpty(Series.AirTime))
                    AirScheduleText += $" - ساعت {Series.AirTime}";
            }
            
            // Next Episode
            if (!string.IsNullOrEmpty(Series.NextEpisodeDate))
            {
                NextEpisodeText = $"{DateTimeFormatterService.FormatDate(Series.NextEpisodeDate)}";
                if (Series.NextEpisodeNumber.HasValue)
                    NextEpisodeText = $"قسمت {Series.NextEpisodeNumber} - " + NextEpisodeText;
            }
            else
            {
                NextEpisodeText = "نامشخص";
            }
            
            // Episodes Info
            var seasons = Series.TotalSeasonsCount ?? Series.NumberOfSeasons ?? 0;
            var episodes = Series.TotalEpisodesCount ?? Series.NumberOfEpisodes ?? 0;
            var languageCheck = SettingsManager.LoadSettings().TmdbLanguage ?? "fa-IR";
            if (languageCheck == "fa-IR")
                EpisodesInfoText = $"{seasons} فصل - {episodes} قسمت";
            else
                EpisodesInfoText = $"{seasons} Seasons - {episodes} Episodes";
        }

        public string FormattedGenres => GenreTranslatorService.TranslateList(Series.Genres);
        public string FormattedYear => DateTimeFormatterService.FormatYear(Series.Year);

        private async Task LoadDetailsAsync()
        {
            if (_series.TmdbId == null) 
            {
                ErrorMessage = "شناسه TMDB برای این سریال یافت نشد. لطفاً ابتدا در بخش فیلم‌ها آن را شناسایی یا رفرش کنید.";
                return;
            }

            IsLoading = true;
            ErrorMessage = null;
            try
            {
                using var db = new AppDbContext();
                
                // Try to load from DB first
                var dbSeasons = db.TvSeasons.Where(s => s.TmdbSeriesId == _series.TmdbId.Value).ToList();
                var dbEpisodes = db.TvEpisodes.Where(e => e.TmdbSeriesId == _series.TmdbId.Value).ToList();

                // Fetch series metadata if missing
                if (_series.TotalSeasonsCount == null || _series.FirstAirDate == null)
                {
                    try
                    {
                        var settings = SettingsManager.LoadSettings();
                        string apiKey = SettingsManager.GetTmdbApiKey();
                        string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;
                        
                        await _mediaService.IdentifySeriesDetailsAsync(_series, apiKey, language);
                        
                        // Save the updated series info to DB
                        var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == _series.Id);
                        if (dbSeries != null)
                        {
                            dbSeries.FirstAirDate = _series.FirstAirDate;
                            dbSeries.LastAirDate = _series.LastAirDate;
                            dbSeries.NetworkName = _series.NetworkName;
                            dbSeries.AirDay = _series.AirDay;
                            dbSeries.AirTime = _series.AirTime;
                            dbSeries.TotalSeasonsCount = _series.TotalSeasonsCount;
                            dbSeries.TotalEpisodesCount = _series.TotalEpisodesCount;
                            dbSeries.NextEpisodeDate = _series.NextEpisodeDate;
                            dbSeries.NextEpisodeNumber = _series.NextEpisodeNumber;
                            dbSeries.SeriesStatus = _series.SeriesStatus;

                            _mediaService.CleanTrackerInfoFromOverview(_series);
                            dbSeries.Overview = _series.Overview;

                            await db.SaveChangesAsync();
                        }
                        
                        // Update UI properties
                        App.Current.Dispatcher.Invoke(() => {
                            LoadSeriesTrackerInfo();
                        });
                    }
                    catch (Exception ex)
                    {
                        App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError($"خطا در بروزرسانی: {ex.Message}"));
                    }
                }

                if (dbSeasons.Count == 0 || dbEpisodes.Count == 0)
                {
                    try
                    {
                        // Fetch from TMDB
                        var (fetchedSeasons, fetchedEpisodes) = await _mediaService.FetchSeriesDetailsAsync(_series.TmdbId.Value);
                        
                        if (fetchedSeasons.Count > 0)
                        {
                            db.TvSeasons.AddRange(fetchedSeasons);
                            db.TvEpisodes.AddRange(fetchedEpisodes);
                            await db.SaveChangesAsync();

                            dbSeasons = fetchedSeasons;
                            dbEpisodes = fetchedEpisodes;
                        }
                        else
                        {
                            ErrorMessage = "هیچ اطلاعاتی برای فصل‌ها و قسمت‌های این سریال از اینترنت دریافت نشد.";
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"خطا در دریافت اطلاعات: {ex.Message}";
                    }
                }

                // Query local video files corresponding to this series
                var localEpisodeFiles = db.VideoFiles
                    .Where(v => (v.TmdbId == _series.TmdbId || v.FormattedTitle.ToLower() == _series.FormattedTitle.ToLower()) && v.Season != null && v.Episode != null)
                    .ToList();

                // Update UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    Seasons.Clear();
                    bool isFirst = true;
                    foreach (var s in dbSeasons.OrderBy(x => x.SeasonNumber))
                    {
                        var group = new SeasonGroup 
                        { 
                            Season = s,
                            IsExpanded = isFirst
                        };
                        isFirst = false;

                        foreach (var e in dbEpisodes.Where(ep => ep.SeasonNumber == s.SeasonNumber).OrderBy(x => x.EpisodeNumber))
                        {
                            var localMatch = localEpisodeFiles.FirstOrDefault(lf => lf.Season == e.SeasonNumber && lf.Episode == e.EpisodeNumber);
                            var epVm = new EpisodeItemViewModel
                            {
                                Episode = e,
                                LocalFile = localMatch,
                                SeriesTitle = _series.FormattedTitle,
                                IsWatched = e.IsWatched
                            };
                            group.Episodes.Add(epVm);
                        }
                        Seasons.Add(group);
                    }

                    UpdateProgressAndContinueWatching(db);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateProgressAndContinueWatching(AppDbContext? existingDb = null)
        {
            bool ownDb = existingDb == null;
            var db = existingDb ?? new AppDbContext();
            try
            {
                int total = 0;
                int watched = 0;
                EpisodeItemViewModel? nextUnwatched = null;
                EpisodeItemViewModel? firstEpisode = null;

                foreach (var s in Seasons.OrderBy(x => x.SeasonNumber))
                {
                    foreach (var ep in s.Episodes.OrderBy(x => x.EpisodeNumber))
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
                    ContinueWatchingText = $"ادامه تماشا (فصل {nextUnwatched.SeasonNumber} قسمت {nextUnwatched.EpisodeNumber})";
                    CanContinueWatching = true;
                }
                else if (firstEpisode != null)
                {
                    ContinueWatchingEpisode = firstEpisode;
                    ContinueWatchingText = $"تماشای مجدد (فصل {firstEpisode.SeasonNumber} قسمت {firstEpisode.EpisodeNumber})";
                    CanContinueWatching = true;
                }
                else
                {
                    ContinueWatchingEpisode = null;
                    ContinueWatchingText = "شروع تماشا";
                    CanContinueWatching = false;
                }

                // Update series record in database
                if (_series.TmdbId != null)
                {
                    var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == _series.Id);
                    if (dbSeries != null)
                    {
                        dbSeries.WatchProgressPercent = OverallProgressPercent;
                        dbSeries.IsTracked = true;
                        db.SaveChanges();
                    }
                }
            }
            finally
            {
                if (ownDb) db.Dispose();
            }
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
                ToastService.Instance.ShowWarning("قسمتی برای پخش یافت نشد.");
            }
        }

        [RelayCommand]
        private void PlayEpisode(EpisodeItemViewModel? episodeItem)
        {
            if (episodeItem == null) return;

            VideoFile? fileToPlay = episodeItem.LocalFile;

            // If not directly linked, search DB for matching episode file
            if (fileToPlay == null || string.IsNullOrEmpty(fileToPlay.FilePath) || !File.Exists(fileToPlay.FilePath))
            {
                using var db = new AppDbContext();
                fileToPlay = db.VideoFiles.FirstOrDefault(v => 
                    (v.TmdbId == _series.TmdbId || v.FormattedTitle.ToLower() == _series.FormattedTitle.ToLower()) &&
                    v.Season == episodeItem.SeasonNumber &&
                    v.Episode == episodeItem.EpisodeNumber);
            }

            // Fallback: check if series main file is an episode in a folder, search sister files in the directory
            if (fileToPlay == null || string.IsNullOrEmpty(fileToPlay.FilePath) || !File.Exists(fileToPlay.FilePath))
            {
                if (!string.IsNullOrEmpty(_series.FilePath))
                {
                    string? dir = Path.GetDirectoryName(_series.FilePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string sPad = episodeItem.SeasonNumber.ToString("D2");
                        string ePad = episodeItem.EpisodeNumber.ToString("D2");
                        string sRaw = episodeItem.SeasonNumber.ToString();
                        string eRaw = episodeItem.EpisodeNumber.ToString();

                        var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        var matchedFile = files.FirstOrDefault(f => {
                            string name = Path.GetFileName(f).ToLowerInvariant();
                            return (name.Contains($"s{sPad}e{ePad}") || name.Contains($"s{sRaw}e{eRaw}") || name.Contains($"{sRaw}x{ePad}"));
                        });

                        if (matchedFile != null)
                        {
                            fileToPlay = new VideoFile
                            {
                                FilePath = matchedFile,
                                FileName = Path.GetFileName(matchedFile),
                                FormattedTitle = _series.FormattedTitle,
                                Season = episodeItem.SeasonNumber,
                                Episode = episodeItem.EpisodeNumber,
                                TmdbId = _series.TmdbId,
                                PosterUrl = _series.PosterUrl,
                                BackdropUrl = _series.BackdropUrl
                            };
                        }
                    }
                }
            }

            if (fileToPlay != null && !string.IsNullOrEmpty(fileToPlay.FilePath) && File.Exists(fileToPlay.FilePath))
            {
                // Build playlist of all available local episodes for seamless next/previous
                var playlist = Seasons
                    .SelectMany(s => s.Episodes)
                    .Where(e => e.LocalFile != null && File.Exists(e.LocalFile.FilePath))
                    .Select(e => e.LocalFile!)
                    .DistinctBy(f => f.FilePath)
                    .ToList();

                int initialIndex = playlist.FindIndex(f => f.FilePath == fileToPlay.FilePath);
                if (initialIndex < 0)
                {
                    playlist.Insert(0, fileToPlay);
                    initialIndex = 0;
                }

                PlaybackService.PlayMedia(fileToPlay, playlist, initialIndex);
            }
            else
            {
                ToastService.Instance.ShowWarning($"فایل ویدیویی مربوط به فصل {episodeItem.SeasonNumber} قسمت {episodeItem.EpisodeNumber} در سیستم شما یافت نشد.");
            }
        }

        [RelayCommand]
        private void ToggleSeasonExpanded(SeasonGroup? seasonGroup)
        {
            if (seasonGroup == null) return;
            seasonGroup.IsExpanded = !seasonGroup.IsExpanded;
        }

        [RelayCommand]
        private void ToggleEpisodeWatched(EpisodeItemViewModel? episodeItem)
        {
            if (episodeItem == null) return;
            
            episodeItem.IsWatched = !episodeItem.IsWatched;
            episodeItem.Episode.IsWatched = episodeItem.IsWatched;

            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                var dbEp = db.TvEpisodes.FirstOrDefault(e => e.Id == episodeItem.Episode.Id);
                if (dbEp != null)
                {
                    dbEp.IsWatched = episodeItem.IsWatched;
                    await db.SaveChangesAsync();
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateProgressAndContinueWatching(db);
                });
            });
        }
        
        [RelayCommand]
        private void ToggleSeasonWatched(SeasonGroup? seasonGroup)
        {
            if (seasonGroup == null) return;
            
            bool newWatchedState = !seasonGroup.AllWatched;
            if (seasonGroup.Season != null)
            {
                seasonGroup.Season.IsWatched = newWatchedState;
            }

            foreach (var ep in seasonGroup.Episodes)
            {
                ep.IsWatched = newWatchedState;
                ep.Episode.IsWatched = newWatchedState;
            }

            seasonGroup.NotifyWatchedChanged();

            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                if (seasonGroup.Season != null)
                {
                    var dbSeason = db.TvSeasons.FirstOrDefault(s => s.Id == seasonGroup.Season.Id);
                    if (dbSeason != null) dbSeason.IsWatched = newWatchedState;
                }

                var epIds = seasonGroup.Episodes.Select(e => e.Episode.Id).ToList();
                var dbEps = db.TvEpisodes.Where(e => epIds.Contains(e.Id)).ToList();
                foreach (var ep in dbEps)
                {
                    ep.IsWatched = newWatchedState;
                }

                await db.SaveChangesAsync();

                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateProgressAndContinueWatching(db);
                });
            });
        }

        [RelayCommand]
        private void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
            using var db = new AppDbContext();
            var filesToUpdate = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == _series.FormattedTitle.ToLower()).ToList();
            foreach (var f in filesToUpdate)
            {
                f.IsFavorite = IsFavorite;
            }
            db.SaveChanges();
            _series.IsFavorite = IsFavorite;
            App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowSuccess(IsFavorite ? "به علاقه‌مندی‌ها اضافه شد" : "از علاقه‌مندی‌ها حذف شد"));
        }

        [RelayCommand]
        private async Task ChangePosterAsync()
        {
            if (Series.TmdbId == null || Series.TmdbId == 0)
            {
                ToastService.Instance.ShowWarning("شناسه TMDB برای این سریال ثبت نشده است. ابتدا سریال را شناسایی یا جستجو کنید.");
                return;
            }

            ToastService.Instance.ShowInfo("در حال جستجوی پوسترهای باکیفیت در سرور...");
            var service = new IdentifyMediaService();
            LoggerService.Info($"[صفحه جزییات] جستجوی پوسترهای جایگزین برای: {_series.FormattedTitle}...");
            
            List<string>? posters = null;
            try
            {
                posters = await service.GetMediaPostersAsync(Series.TmdbId.Value, "Series");
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[صفحه جزییات] خطا در دریافت پوسترهای سریال: {ex.Message}", ex);
                ToastService.Instance.ShowError("عدم برقراری ارتباط با سرور پوسترها. اتصال اینترنت یا قندشکن را بررسی کنید.");
                return;
            }
            
            if (posters == null || posters.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ پوستر جایگزینی برای این سریال در سرور یافت نشد.");
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
                ToastService.Instance.ShowInfo("در حال دانلود و ذخیره پوستر انتخابی...");
                var savedPath = await service.DownloadAndSaveImageAsync(vm.SelectedPosterUrl, Series.FormattedTitle);
                if (savedPath != null)
                {
                    using var db = new AppDbContext();
                    var dbFiles = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == Series.FormattedTitle.ToLower()).ToList();
                    foreach (var dbFile in dbFiles)
                    {
                        dbFile.PosterUrl = savedPath;
                    }
                    await db.SaveChangesAsync();

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var temp = Series;
                        Series = null;
                        temp.PosterUrl = savedPath;
                        Series = temp;
                        WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
                        ToastService.Instance.ShowSuccess("پوستر جدید با موفقیت اعمال و ذخیره شد.");
                    });
                }
                else
                {
                    ToastService.Instance.ShowError("خطا در دانلود پوستر جدید.");
                }
            }
        }

        [RelayCommand]
        private async Task RefreshSeriesAsync()
        {
            if (_series.TmdbId == null || _series.TmdbId == 0)
            {
                ToastService.Instance.ShowWarning("شناسه TMDB برای این سریال ثبت نشده است. ابتدا سریال را از جستجوی دستی مشخص کنید.");
                return;
            }

            IsLoading = true;
            ToastService.Instance.ShowInfo("در حال بروزرسانی اطلاعات کامل و قسمت‌های سریال از سرور...");
            try
            {
                LoggerService.Info($"[صفحه جزییات] بروزرسانی اطلاعات سریال: {_series.FormattedTitle}...");
                var settings = SettingsManager.LoadSettings();
                string apiKey = SettingsManager.GetTmdbApiKey();
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;
                
                await _mediaService.IdentifySeriesDetailsAsync(_series, apiKey, language);
                
                using var db = new AppDbContext();
                var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == _series.Id);
                if (dbSeries != null)
                {
                    dbSeries.FirstAirDate = _series.FirstAirDate;
                    dbSeries.LastAirDate = _series.LastAirDate;
                    dbSeries.NetworkName = _series.NetworkName;
                    dbSeries.AirDay = _series.AirDay;
                    dbSeries.AirTime = _series.AirTime;
                    dbSeries.TotalSeasonsCount = _series.TotalSeasonsCount;
                    dbSeries.TotalEpisodesCount = _series.TotalEpisodesCount;
                    dbSeries.NextEpisodeDate = _series.NextEpisodeDate;
                    dbSeries.NextEpisodeNumber = _series.NextEpisodeNumber;
                    dbSeries.SeriesStatus = _series.SeriesStatus;
                    dbSeries.PosterUrl = _series.PosterUrl;
                    dbSeries.BackdropUrl = _series.BackdropUrl;

                    _mediaService.CleanTrackerInfoFromOverview(_series);
                    dbSeries.Overview = _series.Overview;

                    await db.SaveChangesAsync();
                }
                
                // Refresh seasons/episodes from TMDB
                var existingSeasons = db.TvSeasons.Where(s => s.TmdbSeriesId == _series.TmdbId.Value).ToList();
                var existingEpisodes = db.TvEpisodes.Where(e => e.TmdbSeriesId == _series.TmdbId.Value).ToList();
                db.TvSeasons.RemoveRange(existingSeasons);
                db.TvEpisodes.RemoveRange(existingEpisodes);
                await db.SaveChangesAsync();
                
                var (fetchedSeasons, fetchedEpisodes) = await _mediaService.FetchSeriesDetailsAsync(_series.TmdbId.Value);
                if (fetchedSeasons.Count > 0)
                {
                    db.TvSeasons.AddRange(fetchedSeasons);
                    db.TvEpisodes.AddRange(fetchedEpisodes);
                    await db.SaveChangesAsync();
                }

                var localEpisodeFiles = db.VideoFiles
                    .Where(v => (v.TmdbId == _series.TmdbId || v.FormattedTitle.ToLower() == _series.FormattedTitle.ToLower()) && v.Season != null && v.Episode != null)
                    .ToList();
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadSeriesTrackerInfo();
                    Seasons.Clear();
                    bool isFirst = true;
                    foreach (var s in fetchedSeasons.OrderBy(x => x.SeasonNumber))
                    {
                        var group = new SeasonGroup 
                        { 
                            Season = s,
                            IsExpanded = isFirst
                        };
                        isFirst = false;

                        foreach (var e in fetchedEpisodes.Where(ep => ep.SeasonNumber == s.SeasonNumber).OrderBy(x => x.EpisodeNumber))
                        {
                            var localMatch = localEpisodeFiles.FirstOrDefault(lf => lf.Season == e.SeasonNumber && lf.Episode == e.EpisodeNumber);
                            var epVm = new EpisodeItemViewModel
                            {
                                Episode = e,
                                LocalFile = localMatch,
                                SeriesTitle = _series.FormattedTitle,
                                IsWatched = e.IsWatched
                            };
                            group.Episodes.Add(epVm);
                        }
                        Seasons.Add(group);
                    }

                    UpdateProgressAndContinueWatching(db);
                    OnPropertyChanged(nameof(Series));
                    ToastService.Instance.ShowSuccess($"اطلاعات سریال، {fetchedSeasons.Count} فصل و {fetchedEpisodes.Count} قسمت با موفقیت بروزرسانی شد.");
                });
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[صفحه جزییات] خطا در بروزرسانی سریال: {ex.Message}", ex);
                string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم برقراری ارتباط با سرور. لطفاً وضعیت اتصال یا قندشکن را بررسی کنید."
                    : $"خطا در بروزرسانی: {ex.Message}";
                ToastService.Instance.ShowError(errMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshTrackerAsync()
        {
            if (_series.TmdbId == null || _series.TmdbId == 0)
            {
                ToastService.Instance.ShowWarning("شناسه TMDB برای این سریال ثبت نشده است.");
                return;
            }

            IsLoading = true;
            ToastService.Instance.ShowInfo("در حال دریافت آخرین اطلاعات ردیاب و تاریخ پخش...");
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = SettingsManager.GetTmdbApiKey();
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;
                
                LoggerService.Info($"[صفحه جزییات] بروزرسانی اطلاعات ردیاب: {_series.FormattedTitle}...");
                
                await _mediaService.IdentifySeriesDetailsAsync(_series, apiKey, language);
                
                using var db = new AppDbContext();
                var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == _series.Id);
                if (dbSeries != null)
                {
                    dbSeries.SeriesStatus = _series.SeriesStatus;
                    dbSeries.FirstAirDate = _series.FirstAirDate;
                    dbSeries.LastAirDate = _series.LastAirDate;
                    dbSeries.NetworkName = _series.NetworkName;
                    dbSeries.AirDay = _series.AirDay;
                    dbSeries.AirTime = _series.AirTime;
                    dbSeries.NextEpisodeDate = _series.NextEpisodeDate;
                    dbSeries.NextEpisodeNumber = _series.NextEpisodeNumber;
                    dbSeries.TotalSeasonsCount = _series.TotalSeasonsCount;
                    dbSeries.TotalEpisodesCount = _series.TotalEpisodesCount;
                    
                    _mediaService.CleanTrackerInfoFromOverview(_series);
                    dbSeries.Overview = _series.Overview;
                    
                    await db.SaveChangesAsync();
                }
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadSeriesTrackerInfo();
                    OnPropertyChanged(nameof(Series));
                    ToastService.Instance.ShowSuccess($"اطلاعات ردیاب با موفقیت بروزرسانی شد: {SeriesStatusText}");
                });
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[صفحه جزییات] خطا در بروزرسانی ردیاب: {ex.Message}", ex);
                string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم برقراری ارتباط با سرور ردیاب. اتصال اینترنت را بررسی کنید."
                    : $"خطا در بروزرسانی ردیاب: {ex.Message}";
                ToastService.Instance.ShowError(errMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSeriesAsync()
        {
            var result = System.Windows.MessageBox.Show(
                $"آیا مطمئن هستید که می‌خواهید سریال «{_series.FormattedTitle}» را حذف کنید؟",
                "تأیید حذف",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            using var db = new AppDbContext();
            var filesToDelete = db.VideoFiles.Where(v => v.FormattedTitle.ToLower() == _series.FormattedTitle.ToLower()).ToList();
            db.VideoFiles.RemoveRange(filesToDelete);

            if (_series.TmdbId.HasValue)
            {
                var seasons = db.TvSeasons.Where(s => s.TmdbSeriesId == _series.TmdbId.Value).ToList();
                var episodes = db.TvEpisodes.Where(e => e.TmdbSeriesId == _series.TmdbId.Value).ToList();
                db.TvSeasons.RemoveRange(seasons);
                db.TvEpisodes.RemoveRange(episodes);
            }

            await db.SaveChangesAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                ToastService.Instance.ShowSuccess("سریال با موفقیت حذف شد");
                WeakReferenceMessenger.Default.Send(new NavigationMessage(new MoviesViewModel()));
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            });
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new MoviesViewModel()));
        }
    }
}
