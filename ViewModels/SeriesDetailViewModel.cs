using System.Collections.ObjectModel;
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
    public class SeasonGroup
    {
        public TvSeason? Season { get; set; }
        public ObservableCollection<TvEpisode> Episodes { get; set; } = new();
    }

    public partial class SeriesDetailViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _mediaService;

        [ObservableProperty]
        private VideoFile _series;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _errorMessage;
        
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
        private string _nextEpisodeText = "ندارد";
        
        [ObservableProperty]
        private string _episodesInfoText = "نامشخص";

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
                bool isPersian = string.IsNullOrEmpty(language) || language.Contains("fa", StringComparison.OrdinalIgnoreCase);
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
                FirstAirDateText = Series.FirstAirDate.Value.ToString("yyyy/MM/dd");
            else if (!string.IsNullOrWhiteSpace(Series.Year))
                FirstAirDateText = Series.Year;
            
            // Last Air Date
            if (Series.LastAirDate.HasValue)
                LastAirDateText = Series.LastAirDate.Value.ToString("yyyy/MM/dd");
            
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
                NextEpisodeText = $"تاریخ: {Series.NextEpisodeDate}";
                if (Series.NextEpisodeNumber.HasValue)
                    NextEpisodeText = $"قسمت {Series.NextEpisodeNumber} - " + NextEpisodeText;
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
                        string apiKey = string.IsNullOrEmpty(settings.TmdbApiKey) ? "3272e27041f0b0ee11dbaf0315ce5b21" : settings.TmdbApiKey;
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
                            await db.SaveChangesAsync();
                        }
                        
                        // Update UI properties
                        App.Current.Dispatcher.Invoke(() => {
                            LoadSeriesTrackerInfo();
                            ToastService.Instance.ShowSuccess("سریال با موفقیت بروزرسانی شد");
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

                // Update UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    Seasons.Clear();
                    foreach (var s in dbSeasons.OrderBy(x => x.SeasonNumber))
                    {
                        var group = new SeasonGroup { Season = s };
                        var next = dbEpisodes.FirstOrDefault(e => e.SeasonNumber == s.SeasonNumber && DateTime.TryParse(e.AirDate, out var ad) && ad > DateTime.Today);
                        if (next != null)
                        {
                            Series.NextEpisodeToAirInfo = $"فصل {next.SeasonNumber} قسمت {next.EpisodeNumber}: {next.AirDate}";
                        }
                        else
                        {
                            Series.NextEpisodeToAirInfo = string.Empty;
                        }
                        foreach (var e in dbEpisodes.Where(ep => ep.SeasonNumber == s.SeasonNumber).OrderBy(x => x.EpisodeNumber))
                        {
                            group.Episodes.Add(e);
                        }
                        Seasons.Add(group);
                    }
                });
                
                UpdateSeriesProgress(db);
            }
            finally
            {
                IsLoading = false;
            }
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
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError("شناسه TMDB یافت نشد، امکان واکشی پوستر وجود ندارد"));
                return;
            }

            var service = new IdentifyMediaService();
            var posters = await service.GetMediaPostersAsync(Series.TmdbId.Value, "Series");
            
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
                        ToastService.Instance.ShowSuccess("پوستر با موفقیت تغییر کرد");
                    });
                }
            }
        }

        [RelayCommand]
        private async Task RefreshSeriesAsync()
        {
            if (_series.TmdbId == null) return;
            IsLoading = true;
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = string.IsNullOrEmpty(settings.TmdbApiKey) ? "3272e27041f0b0ee11dbaf0315ce5b21" : settings.TmdbApiKey;
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
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadSeriesTrackerInfo();
                    Seasons.Clear();
                    foreach (var s in fetchedSeasons.OrderBy(x => x.SeasonNumber))
                    {
                        var group = new SeasonGroup { Season = s };
                        foreach (var e in fetchedEpisodes.Where(ep => ep.SeasonNumber == s.SeasonNumber).OrderBy(x => x.EpisodeNumber))
                        {
                            group.Episodes.Add(e);
                        }
                        Seasons.Add(group);
                    }
                    OnPropertyChanged(nameof(Series));
                    ToastService.Instance.ShowSuccess("اطلاعات سریال با موفقیت بروزرسانی شد");
                });
            }
            catch (System.Exception ex)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError($"خطا: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshTrackerAsync()
        {
            if (_series.TmdbId == null) return;
            IsLoading = true;
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = string.IsNullOrEmpty(settings.TmdbApiKey) ? "3272e27041f0b0ee11dbaf0315ce5b21" : settings.TmdbApiKey;
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;
                
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
                    await db.SaveChangesAsync();
                }
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadSeriesTrackerInfo();
                    OnPropertyChanged(nameof(Series));
                    ToastService.Instance.ShowSuccess("اطلاعات ردیاب بروزرسانی شد");
                });
            }
            catch (System.Exception ex)
            {
                App.Current.Dispatcher.Invoke(() => ToastService.Instance.ShowError($"خطا: {ex.Message}"));
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

        [RelayCommand]
        private void ToggleEpisodeWatched(TvEpisode episode)
        {
            if (episode == null) return;
            
            episode.IsWatched = !episode.IsWatched;
            
            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                var dbEp = db.TvEpisodes.FirstOrDefault(e => e.Id == episode.Id);
                if (dbEp != null)
                {
                    dbEp.IsWatched = episode.IsWatched;
                    await db.SaveChangesAsync();
                    UpdateSeriesProgress(db);
                }
            });
        }
        
        [RelayCommand]
        private void ToggleSeasonWatched(SeasonGroup season)
        {
            if (season == null) return;
            season.Season.IsWatched = !season.Season.IsWatched;
            Task.Run(() =>
            {
                using var db = new AppDbContext();
                var dbSeason = db.TvSeasons.FirstOrDefault(s => s.Id == season.Season.Id);
                if (dbSeason != null)
                {
                    dbSeason.IsWatched = season.Season.IsWatched;
                    
                    foreach (var ep in season.Episodes)
                    {
                        ep.IsWatched = season.Season.IsWatched;
                        var dbEp = db.TvEpisodes.FirstOrDefault(e => e.Id == ep.Id);
                        if (dbEp != null) dbEp.IsWatched = season.Season.IsWatched;
                    }
                }
                
                db.SaveChanges();
                
                UpdateSeriesProgress(db);
            });
        }
        
        private void UpdateSeriesProgress(AppDbContext db)
        {
            if (_series.TmdbId == null) return;
            
            int totalEpisodes = db.TvEpisodes.Count(e => e.TmdbSeriesId == _series.TmdbId.Value);
            int watchedEpisodes = db.TvEpisodes.Count(e => e.TmdbSeriesId == _series.TmdbId.Value && e.IsWatched);
            
            if (totalEpisodes > 0)
            {
                var dbSeries = db.VideoFiles.FirstOrDefault(v => v.Id == _series.Id);
                if (dbSeries != null)
                {
                    dbSeries.WatchProgressPercent = (double)watchedEpisodes / totalEpisodes * 100;
                    dbSeries.IsTracked = true; // Auto track when interacted
                    db.SaveChanges();
                    
                    // Update current instance
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _series.WatchProgressPercent = dbSeries.WatchProgressPercent;
                        _series.IsTracked = true;
                    });
                }
            }
        }
    }
}
