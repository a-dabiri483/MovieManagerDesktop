using System;
using System.Collections.Generic;
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
    public partial class SeasonTrackerItem : ObservableObject
    {
        public int SeasonNumber { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public int TotalEpisodes { get; set; }

        [ObservableProperty]
        private int _watchedEpisodes;

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private string _progressText = string.Empty;

        [ObservableProperty]
        private string _progressPercentText = string.Empty;

        public void Recalculate()
        {
            ProgressPercent = TotalEpisodes > 0 ? Math.Min(100.0, (double)WatchedEpisodes / TotalEpisodes * 100.0) : 0;
            IsCompleted = WatchedEpisodes >= TotalEpisodes && TotalEpisodes > 0;
            ProgressText = $"{WatchedEpisodes} از {TotalEpisodes} قسمت";
            ProgressPercentText = $"{Math.Round(ProgressPercent)}%";
        }

        partial void OnWatchedEpisodesChanged(int value)
        {
            Recalculate();
        }
    }

    public partial class TrackedDetailViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _mediaService;

        [ObservableProperty]
        private VideoFile _media;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // Visual properties
        public string Title => string.IsNullOrWhiteSpace(Media.FormattedTitle) ? (Media.FileName ?? "بدون عنوان") : Media.FormattedTitle;
        public string FormattedYear => DateTimeFormatterService.FormatYear(Media.Year);
        public string FormattedGenres => GenreTranslatorService.TranslateList(Media.Genres);
        public string PosterUrl => Media.PosterUrl ?? string.Empty;
        public string BackdropUrl => Media.BackdropUrl ?? string.Empty;
        public double Rating => Media.Rating ?? 0.0;
        public string Overview => string.IsNullOrWhiteSpace(Media.Overview) ? "توضیحاتی برای این اثر ثبت نشده است." : Media.Overview;
        public string MediaType => Media.MediaType ?? "Movie";
        public bool IsSeries => MediaType.Equals("Series", StringComparison.OrdinalIgnoreCase);
        public string MediaTypeDisplay => IsSeries ? "سریال تلویزیونی" : "فیلم سینمایی";

        // Series Tracker Properties
        [ObservableProperty]
        private string _seriesStatusText = "نامشخص";

        [ObservableProperty]
        private string _seriesStatusColor = "#00D2D3";

        [ObservableProperty]
        private string _firstAirDateText = "نامشخص";

        [ObservableProperty]
        private string _lastAirDateText = "نامشخص";

        [ObservableProperty]
        private string _networkText = "نامشخص";

        [ObservableProperty]
        private string _airScheduleText = "نامشخص";

        [ObservableProperty]
        private string _nextEpisodeText = string.Empty;

        [ObservableProperty]
        private bool _hasNextEpisode;

        [ObservableProperty]
        private string _seasonsCountText = string.Empty;

        [ObservableProperty]
        private string _episodesCountText = string.Empty;

        [ObservableProperty]
        private double _overallProgressPercent;

        [ObservableProperty]
        private string _overallProgressText = "۰ از ۰ قسمت دیده شده";

        public ObservableCollection<SeasonTrackerItem> SeasonItems { get; } = new();

        public TrackedDetailViewModel(VideoFile media)
        {
            _media = media;
            _mediaService = new IdentifyMediaService();

            InitTrackerInfo();
            _ = LoadDetailsAsync();
        }

        private void InitTrackerInfo()
        {
            if (IsSeries)
            {
                string rawStatus = (Media.SeriesStatus ?? "").Trim().ToLowerInvariant();
                if (rawStatus.Contains("returning") || rawStatus.Contains("airing"))
                {
                    SeriesStatusText = "در حال پخش";
                    SeriesStatusColor = "#2ED573"; // سبز
                }
                else if (rawStatus.Contains("ended") || rawStatus.Contains("finished"))
                {
                    SeriesStatusText = "تمام شده";
                    SeriesStatusColor = "#A4B0BE"; // خاکستری
                }
                else if (rawStatus.Contains("cancel"))
                {
                    SeriesStatusText = "کنسل شده";
                    SeriesStatusColor = "#FF4757"; // قرمز
                }
                else if (rawStatus.Contains("planned") || rawStatus.Contains("production"))
                {
                    SeriesStatusText = "در دست ساخت";
                    SeriesStatusColor = "#FFA502"; // نارنجی
                }
                else
                {
                    SeriesStatusText = !string.IsNullOrEmpty(Media.SeriesStatus) ? Media.SeriesStatus : "نامشخص";
                    SeriesStatusColor = "#00D2D3";
                }

                if (Media.FirstAirDate.HasValue)
                    FirstAirDateText = DateTimeFormatterService.FormatShortDate(Media.FirstAirDate.Value);
                else if (!string.IsNullOrEmpty(Media.Year))
                    FirstAirDateText = FormattedYear;

                if (Media.LastAirDate.HasValue)
                    LastAirDateText = DateTimeFormatterService.FormatShortDate(Media.LastAirDate.Value);

                if (!string.IsNullOrEmpty(Media.NetworkName))
                    NetworkText = Media.NetworkName;

                if (!string.IsNullOrEmpty(Media.AirDay))
                {
                    AirScheduleText = !string.IsNullOrEmpty(Media.AirTime)
                        ? $"{Media.AirDay} ساعت {Media.AirTime}"
                        : Media.AirDay;
                }

                if (!string.IsNullOrEmpty(Media.NextEpisodeDate))
                {
                    HasNextEpisode = true;
                    string formattedDate = DateTimeFormatterService.FormatDate(Media.NextEpisodeDate);
                    if (Media.NextEpisodeNumber.HasValue)
                    {
                        string seasonPart = Media.NextEpisodeSeason.HasValue ? $"فصل {Media.NextEpisodeSeason} - " : "";
                        NextEpisodeText = $"{seasonPart}قسمت {Media.NextEpisodeNumber} ({formattedDate})";
                    }
                    else
                    {
                        NextEpisodeText = formattedDate;
                    }
                }
                else
                {
                    HasNextEpisode = false;
                    NextEpisodeText = "قسمت جدیدی در برنامه پخش ثبت نشده است.";
                }

                int s = Media.TotalSeasonsCount ?? Media.NumberOfSeasons ?? 0;
                int e = Media.TotalEpisodesCount ?? Media.NumberOfEpisodes ?? 0;
                SeasonsCountText = s > 0 ? $"{s} فصل" : "نامشخص";
                EpisodesCountText = e > 0 ? $"{e} قسمت" : "نامشخص";
            }
            else
            {
                SeriesStatusText = "فیلم سینمایی";
                SeriesStatusColor = "#3A86FF";
                FirstAirDateText = FormattedYear;
            }
        }

        [RelayCommand]
        public async Task LoadDetailsAsync()
        {
            if (!IsSeries || !Media.TmdbId.HasValue) return;

            IsLoading = true;
            try
            {
                int tmdbId = Media.TmdbId.Value;

                var (dbSeasons, dbEpisodes) = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var sList = db.TvSeasons.Where(s => s.TmdbSeriesId == tmdbId).OrderBy(s => s.SeasonNumber).ToList();
                    var eList = db.TvEpisodes.Where(e => e.TmdbSeriesId == tmdbId).OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).ToList();
                    return (sList, eList);
                });

                if (dbSeasons.Count == 0 && dbEpisodes.Count == 0)
                {
                    // Fetch from TMDb
                    var (fetchedSeasons, fetchedEpisodes) = await _mediaService.FetchSeriesDetailsAsync(tmdbId);
                    if (fetchedSeasons.Count > 0)
                    {
                        await Task.Run(async () =>
                        {
                            using var db = new AppDbContext();
                            db.TvSeasons.AddRange(fetchedSeasons);
                            db.TvEpisodes.AddRange(fetchedEpisodes);
                            await db.SaveChangesAsync();
                        });
                        dbSeasons = fetchedSeasons;
                        dbEpisodes = fetchedEpisodes;
                    }
                }

                SeasonItems.Clear();
                int totalEps = 0;
                int totalWatched = 0;

                foreach (var s in dbSeasons.Where(x => x.SeasonNumber > 0).OrderBy(x => x.SeasonNumber))
                {
                    var seasonEps = dbEpisodes.Where(e => e.SeasonNumber == s.SeasonNumber).ToList();
                    int count = s.EpisodeCount > 0 ? s.EpisodeCount : seasonEps.Count;
                    if (count == 0) count = seasonEps.Count;

                    int watched = seasonEps.Count(e => e.IsWatched);

                    totalEps += count;
                    totalWatched += watched;

                    SeasonItems.Add(new SeasonTrackerItem
                    {
                        SeasonNumber = s.SeasonNumber,
                        SeasonName = !string.IsNullOrWhiteSpace(s.Name) ? s.Name : $"فصل {s.SeasonNumber}",
                        TotalEpisodes = count,
                        WatchedEpisodes = watched
                    });
                }

                if (SeasonItems.Count == 0 && (Media.NumberOfSeasons ?? 0) > 0)
                {
                    // Fallback create season items from NumberOfSeasons
                    int sCount = Media.NumberOfSeasons!.Value;
                    int epsPerSeason = (Media.NumberOfEpisodes ?? (sCount * 10)) / sCount;
                    if (epsPerSeason == 0) epsPerSeason = 10;

                    for (int i = 1; i <= sCount; i++)
                    {
                        SeasonItems.Add(new SeasonTrackerItem
                        {
                            SeasonNumber = i,
                            SeasonName = $"فصل {i}",
                            TotalEpisodes = epsPerSeason,
                            WatchedEpisodes = 0
                        });
                    }
                }

                UpdateOverallProgress();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error loading tracked series details", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateOverallProgress()
        {
            int totalEps = SeasonItems.Sum(s => s.TotalEpisodes);
            int totalWatched = SeasonItems.Sum(s => s.WatchedEpisodes);
            OverallProgressPercent = totalEps > 0 ? Math.Min(100.0, (double)totalWatched / totalEps * 100.0) : 0;
            OverallProgressText = $"{totalWatched} از {totalEps} قسمت دیده شده ({Math.Round(OverallProgressPercent)}%)";
        }

        [RelayCommand]
        public void IncrementWatched(SeasonTrackerItem season)
        {
            if (season == null || season.WatchedEpisodes >= season.TotalEpisodes) return;

            season.WatchedEpisodes++;
            UpdateOverallProgress();

            // Persist to DB in background
            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                if (Media.TmdbId.HasValue)
                {
                    var eps = db.TvEpisodes
                        .Where(e => e.TmdbSeriesId == Media.TmdbId.Value && e.SeasonNumber == season.SeasonNumber)
                        .OrderBy(e => e.EpisodeNumber)
                        .ToList();

                    if (eps.Count >= season.WatchedEpisodes)
                    {
                        eps[season.WatchedEpisodes - 1].IsWatched = true;
                    }
                    else if (eps.Count == 0)
                    {
                        // Ensure episode record exists
                        var newEp = new TvEpisode
                        {
                            TmdbSeriesId = Media.TmdbId.Value,
                            SeasonNumber = season.SeasonNumber,
                            EpisodeNumber = season.WatchedEpisodes,
                            IsWatched = true,
                            Name = $"قسمت {season.WatchedEpisodes}"
                        };
                        db.TvEpisodes.Add(newEp);
                    }
                }

                var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbMedia != null)
                {
                    dbMedia.WatchProgressPercent = OverallProgressPercent;
                    dbMedia.IsWatched = OverallProgressPercent >= 100;
                }
                await db.SaveChangesAsync();
            });
        }

        [RelayCommand]
        public void DecrementWatched(SeasonTrackerItem season)
        {
            if (season == null || season.WatchedEpisodes <= 0) return;

            int currentWatched = season.WatchedEpisodes;
            season.WatchedEpisodes--;
            UpdateOverallProgress();

            // Persist to DB in background
            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                if (Media.TmdbId.HasValue)
                {
                    var eps = db.TvEpisodes
                        .Where(e => e.TmdbSeriesId == Media.TmdbId.Value && e.SeasonNumber == season.SeasonNumber)
                        .OrderBy(e => e.EpisodeNumber)
                        .ToList();

                    if (eps.Count >= currentWatched)
                    {
                        eps[currentWatched - 1].IsWatched = false;
                    }
                }

                var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbMedia != null)
                {
                    dbMedia.WatchProgressPercent = OverallProgressPercent;
                    dbMedia.IsWatched = OverallProgressPercent >= 100;
                }
                await db.SaveChangesAsync();
            });
        }

        [RelayCommand]
        public void MarkAllWatched(SeasonTrackerItem season)
        {
            if (season == null) return;

            bool isAll = season.IsCompleted;
            season.WatchedEpisodes = isAll ? 0 : season.TotalEpisodes;
            UpdateOverallProgress();

            Task.Run(async () =>
            {
                using var db = new AppDbContext();
                if (Media.TmdbId.HasValue)
                {
                    var eps = db.TvEpisodes
                        .Where(e => e.TmdbSeriesId == Media.TmdbId.Value && e.SeasonNumber == season.SeasonNumber)
                        .ToList();

                    foreach (var ep in eps)
                    {
                        ep.IsWatched = !isAll;
                    }

                    var s = db.TvSeasons.FirstOrDefault(x => x.TmdbSeriesId == Media.TmdbId.Value && x.SeasonNumber == season.SeasonNumber);
                    if (s != null)
                    {
                        s.IsWatched = !isAll;
                    }
                }

                var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbMedia != null)
                {
                    dbMedia.WatchProgressPercent = OverallProgressPercent;
                    dbMedia.IsWatched = OverallProgressPercent >= 100;
                }
                await db.SaveChangesAsync();
            });
        }

        [RelayCommand]
        public async Task RefreshFromTmdbAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                ToastService.Instance.ShowInfo("در حال بروزرسانی اطلاعات از TMDb...");
                await _mediaService.UpdateSeriesStatusAsync(Media);

                if (Media.TmdbId.HasValue && IsSeries)
                {
                    var (fetchedSeasons, fetchedEpisodes) = await _mediaService.FetchSeriesDetailsAsync(Media.TmdbId.Value);
                    if (fetchedSeasons.Count > 0)
                    {
                        await Task.Run(async () =>
                        {
                            using var db = new AppDbContext();
                            var oldS = db.TvSeasons.Where(s => s.TmdbSeriesId == Media.TmdbId.Value).ToList();
                            var oldE = db.TvEpisodes.Where(e => e.TmdbSeriesId == Media.TmdbId.Value).ToList();
                            
                            // Preserve watched states
                            var watchedSet = oldE.Where(e => e.IsWatched).Select(e => $"{e.SeasonNumber}_{e.EpisodeNumber}").ToHashSet();

                            db.TvSeasons.RemoveRange(oldS);
                            db.TvEpisodes.RemoveRange(oldE);

                            foreach (var ep in fetchedEpisodes)
                            {
                                if (watchedSet.Contains($"{ep.SeasonNumber}_{ep.EpisodeNumber}"))
                                    ep.IsWatched = true;
                            }

                            db.TvSeasons.AddRange(fetchedSeasons);
                            db.TvEpisodes.AddRange(fetchedEpisodes);

                            var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                            if (dbMedia != null)
                            {
                                dbMedia.SeriesStatus = Media.SeriesStatus;
                                dbMedia.NextEpisodeDate = Media.NextEpisodeDate;
                                dbMedia.NextEpisodeSeason = Media.NextEpisodeSeason;
                                dbMedia.NextEpisodeNumber = Media.NextEpisodeNumber;
                                dbMedia.NumberOfSeasons = Media.NumberOfSeasons;
                                dbMedia.NumberOfEpisodes = Media.NumberOfEpisodes;
                            }
                            await db.SaveChangesAsync();
                        });
                    }
                }

                InitTrackerInfo();
                await LoadDetailsAsync();

                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Overview));
                OnPropertyChanged(nameof(FormattedYear));
                ToastService.Instance.ShowSuccess("اطلاعات ردیاب با موفقیت بروزرسانی شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error refreshing tracked details from TMDb", ex);
                ToastService.Instance.ShowError("خطا در بروزرسانی از TMDb.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveFromTrackerAsync()
        {
            var result = System.Windows.MessageBox.Show(
                $"آیا مطمئن هستید که می‌خواهید «{Title}» را از لیست ردیاب حذف کنید؟",
                "تأیید حذف از ردیاب",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();
                    var dbItem = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                    if (dbItem != null)
                    {
                        if (dbItem.FilePath == "[Manual Tracker]" || string.IsNullOrEmpty(dbItem.FilePath))
                        {
                            db.VideoFiles.Remove(dbItem);
                        }
                        else
                        {
                            dbItem.IsTracked = false;
                            dbItem.IsWatchlist = false;
                        }
                        await db.SaveChangesAsync();
                    }
                });

                ToastService.Instance.ShowSuccess($"«{Title}» از ردیاب حذف شد.");
                GoBack();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error removing item from tracker", ex);
                ToastService.Instance.ShowError("خطا در حذف از ردیاب.");
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new TrackerViewModel()));
        }
    }
}
