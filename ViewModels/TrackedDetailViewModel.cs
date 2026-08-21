using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
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
            ProgressText = $"{WatchedEpisodes} / {TotalEpisodes}";
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

        public bool IsPersianLanguage
        {
            get
            {
                var lang = SettingsManager.LoadSettings().TmdbLanguage ?? "fa-IR";
                return string.IsNullOrEmpty(lang) || lang.Contains("fa", StringComparison.OrdinalIgnoreCase);
            }
        }

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
        private string _totalEpisodesSummaryText = string.Empty;

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
            bool isFa = IsPersianLanguage;

            if (IsSeries)
            {
                string rawStatus = (Media.SeriesStatus ?? "").Trim().ToLowerInvariant();
                if (rawStatus.Contains("returning") || rawStatus.Contains("airing"))
                {
                    SeriesStatusText = isFa ? "در حال پخش" : "Returning Series";
                    SeriesStatusColor = "#2ED573";
                }
                else if (rawStatus.Contains("ended") || rawStatus.Contains("finished"))
                {
                    SeriesStatusText = isFa ? "تمام شده" : "Ended";
                    SeriesStatusColor = "#A4B0BE";
                }
                else if (rawStatus.Contains("cancel"))
                {
                    SeriesStatusText = isFa ? "کنسل شده" : "Canceled";
                    SeriesStatusColor = "#FF4757";
                }
                else if (rawStatus.Contains("planned") || rawStatus.Contains("production"))
                {
                    SeriesStatusText = isFa ? "در دست ساخت" : "Planned";
                    SeriesStatusColor = "#FFA502";
                }
                else
                {
                    SeriesStatusText = !string.IsNullOrEmpty(Media.SeriesStatus) ? Media.SeriesStatus : (isFa ? "نامشخص" : "Unknown");
                    SeriesStatusColor = "#00D2D3";
                }

                if (Media.FirstAirDate.HasValue)
                    FirstAirDateText = DateTimeFormatterService.FormatShortDate(Media.FirstAirDate.Value);
                else if (!string.IsNullOrEmpty(Media.Year))
                    FirstAirDateText = FormattedYear;
                else
                    FirstAirDateText = isFa ? "نامشخص" : "Unknown";

                if (Media.LastAirDate.HasValue)
                    LastAirDateText = DateTimeFormatterService.FormatShortDate(Media.LastAirDate.Value);
                else
                    LastAirDateText = isFa ? "نامشخص" : "Unknown";

                if (!string.IsNullOrEmpty(Media.NetworkName))
                    NetworkText = Media.NetworkName;
                else
                    NetworkText = isFa ? "نامشخص" : "Unknown";

                if (!string.IsNullOrEmpty(Media.AirDay))
                {
                    string rawDay = Media.AirDay.ToLowerInvariant();
                    string dayText = Media.AirDay;

                    if (isFa)
                    {
                        if (rawDay.Contains("saturday")) dayText = "شنبه";
                        else if (rawDay.Contains("sunday")) dayText = "یکشنبه";
                        else if (rawDay.Contains("monday")) dayText = "دوشنبه";
                        else if (rawDay.Contains("tuesday")) dayText = "سه‌شنبه";
                        else if (rawDay.Contains("wednesday")) dayText = "چهارشنبه";
                        else if (rawDay.Contains("thursday")) dayText = "پنج‌شنبه";
                        else if (rawDay.Contains("friday")) dayText = "جمعه";
                    }

                    AirScheduleText = !string.IsNullOrEmpty(Media.AirTime)
                        ? (isFa ? $"{dayText} ساعت {Media.AirTime}" : $"{dayText} at {Media.AirTime}")
                        : dayText;
                }
                else
                {
                    AirScheduleText = isFa ? "نامشخص" : "Unknown";
                }

                if (!string.IsNullOrEmpty(Media.NextEpisodeDate))
                {
                    HasNextEpisode = true;
                    string formattedDate = DateTimeFormatterService.FormatDate(Media.NextEpisodeDate);
                    if (Media.NextEpisodeNumber.HasValue)
                    {
                        string seasonPart = Media.NextEpisodeSeason.HasValue ? (isFa ? $"فصل {Media.NextEpisodeSeason} - " : $"S{Media.NextEpisodeSeason} - ") : "";
                        NextEpisodeText = isFa ? $"{seasonPart}قسمت {Media.NextEpisodeNumber} ({formattedDate})" : $"{seasonPart}Ep {Media.NextEpisodeNumber} ({formattedDate})";
                    }
                    else
                    {
                        NextEpisodeText = formattedDate;
                    }
                }
                else
                {
                    HasNextEpisode = false;
                    NextEpisodeText = isFa ? "نامشخص" : "Unknown";
                }

                int s = Media.TotalSeasonsCount ?? Media.NumberOfSeasons ?? 0;
                int e = Media.TotalEpisodesCount ?? Media.NumberOfEpisodes ?? 0;
                SeasonsCountText = s > 0 ? (isFa ? $"{s} فصل" : $"{s} Seasons") : (isFa ? "نامشخص" : "Unknown");
                EpisodesCountText = e > 0 ? (isFa ? $"{e} قسمت" : $"{e} Episodes") : (isFa ? "نامشخص" : "Unknown");
                TotalEpisodesSummaryText = s > 0 && e > 0 
                    ? (isFa ? $"{s} فصل - {e} قسمت" : $"{s} Seasons - {e} Episodes") 
                    : (isFa ? "نامشخص" : "Unknown");
            }
            else
            {
                SeriesStatusText = isFa ? "فیلم سینمایی" : "Movie";
                SeriesStatusColor = "#3A86FF";
            }
        }

        public async Task LoadDetailsAsync()
        {
            if (Media == null) return;

            IsLoading = true;
            try
            {
                using var db = new AppDbContext();
                int totalSeasons = Media.TotalSeasonsCount ?? Media.NumberOfSeasons ?? 1;
                SeasonItems.Clear();

                for (int i = 1; i <= Math.Max(1, totalSeasons); i++)
                {
                    int totalEps = 10;
                    int watchedEps = 0;

                    if (Media.TmdbId.HasValue)
                    {
                        var eps = db.TvEpisodes.Where(e => e.TmdbSeriesId == Media.TmdbId.Value && e.SeasonNumber == i).ToList();
                        if (eps.Count > 0)
                        {
                            totalEps = eps.Count;
                            watchedEps = eps.Count(e => e.IsWatched);
                        }
                    }

                    var item = new SeasonTrackerItem
                    {
                        SeasonNumber = i,
                        SeasonName = IsPersianLanguage ? $"فصل {i}" : $"Season {i}",
                        TotalEpisodes = totalEps,
                        WatchedEpisodes = watchedEps
                    };
                    item.Recalculate();
                    SeasonItems.Add(item);
                }

                RecalculateOverall();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در بارگذاری جزئیات: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void IncrementWatched(SeasonTrackerItem item)
        {
            if (item == null || item.WatchedEpisodes >= item.TotalEpisodes) return;
            item.WatchedEpisodes++;
            SaveProgress(item);
            RecalculateOverall();
        }

        [RelayCommand]
        private void DecrementWatched(SeasonTrackerItem item)
        {
            if (item == null || item.WatchedEpisodes <= 0) return;
            item.WatchedEpisodes--;
            SaveProgress(item);
            RecalculateOverall();
        }

        [RelayCommand]
        private void MarkAllWatched(SeasonTrackerItem item)
        {
            if (item == null) return;
            if (item.WatchedEpisodes >= item.TotalEpisodes)
            {
                item.WatchedEpisodes = 0;
            }
            else
            {
                item.WatchedEpisodes = item.TotalEpisodes;
            }
            SaveProgress(item);
            RecalculateOverall();
        }

        private void SaveProgress(SeasonTrackerItem item)
        {
            try
            {
                using var db = new AppDbContext();
                if (Media.TmdbId.HasValue)
                {
                    var eps = db.TvEpisodes.Where(e => e.TmdbSeriesId == Media.TmdbId.Value && e.SeasonNumber == item.SeasonNumber).OrderBy(e => e.EpisodeNumber).ToList();
                    for (int i = 0; i < eps.Count; i++)
                    {
                        eps[i].IsWatched = i < item.WatchedEpisodes;
                    }
                }

                var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbMedia != null)
                {
                    int totalEps = SeasonItems.Sum(s => s.TotalEpisodes);
                    int watchedEps = SeasonItems.Sum(s => s.WatchedEpisodes);
                    dbMedia.WatchProgressPercent = totalEps > 0 ? (double)watchedEps / totalEps * 100 : 0;
                    dbMedia.IsTracked = true;
                }
                db.SaveChanges();
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving progress: {ex.Message}");
            }
        }

        private void RecalculateOverall()
        {
            int totalEps = SeasonItems.Sum(s => s.TotalEpisodes);
            int watchedEps = SeasonItems.Sum(s => s.WatchedEpisodes);

            OverallProgressPercent = totalEps > 0 ? Math.Min(100.0, (double)watchedEps / totalEps * 100.0) : 0;
            OverallProgressText = IsPersianLanguage 
                ? $"{watchedEps} از {totalEps} قسمت دیده شده ({Math.Round(OverallProgressPercent)}%)"
                : $"{watchedEps} of {totalEps} episodes watched ({Math.Round(OverallProgressPercent)}%)";
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new TrackerViewModel()));
        }

        [RelayCommand]
        private async Task RefreshFromTmdbAsync()
        {
            if (Media == null || IsLoading) return;
            IsLoading = true;
            try
            {
                var settings = SettingsManager.LoadSettings();
                string apiKey = SettingsManager.GetTmdbApiKey();
                string language = string.IsNullOrEmpty(settings.TmdbLanguage) ? "fa-IR" : settings.TmdbLanguage;

                await _mediaService.IdentifySeriesDetailsAsync(Media, apiKey, language);

                using var db = new AppDbContext();
                var dbItem = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
                if (dbItem != null)
                {
                    dbItem.FirstAirDate = Media.FirstAirDate;
                    dbItem.LastAirDate = Media.LastAirDate;
                    dbItem.NetworkName = Media.NetworkName;
                    dbItem.AirDay = Media.AirDay;
                    dbItem.AirTime = Media.AirTime;
                    dbItem.TotalSeasonsCount = Media.TotalSeasonsCount;
                    dbItem.TotalEpisodesCount = Media.TotalEpisodesCount;
                    dbItem.NextEpisodeDate = Media.NextEpisodeDate;
                    dbItem.NextEpisodeNumber = Media.NextEpisodeNumber;
                    dbItem.SeriesStatus = Media.SeriesStatus;
                    await db.SaveChangesAsync();
                }

                if (Media.TmdbId.HasValue)
                {
                    var (fetchedSeasons, fetchedEpisodes) = await _mediaService.FetchSeriesDetailsAsync(Media.TmdbId.Value);
                    if (fetchedSeasons.Count > 0)
                    {
                        var oldSeasons = db.TvSeasons.Where(s => s.TmdbSeriesId == Media.TmdbId.Value);
                        db.TvSeasons.RemoveRange(oldSeasons);
                        db.TvSeasons.AddRange(fetchedSeasons);
                    }
                    if (fetchedEpisodes.Count > 0)
                    {
                        var oldEpisodes = db.TvEpisodes.Where(e => e.TmdbSeriesId == Media.TmdbId.Value);
                        db.TvEpisodes.RemoveRange(oldEpisodes);
                        db.TvEpisodes.AddRange(fetchedEpisodes);
                    }
                    await db.SaveChangesAsync();
                }

                InitTrackerInfo();
                await LoadDetailsAsync();
                WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در بروزرسانی: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void RemoveFromTracker()
        {
            if (Media == null) return;
            using var db = new AppDbContext();
            var dbMedia = db.VideoFiles.FirstOrDefault(v => v.Id == Media.Id);
            if (dbMedia != null)
            {
                dbMedia.IsTracked = false;
                db.SaveChanges();
            }
            WeakReferenceMessenger.Default.Send(new MediaUpdatedMessage());
            GoBack();
        }
    }
}
