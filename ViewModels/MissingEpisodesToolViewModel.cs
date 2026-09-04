using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
    public partial class MissingEpisodesToolViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _mediaService;
        private List<MissingSeriesGroup> _allMissingGroups = new();

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _filterIndex = 0; // 0: All, 1: Ongoing, 2: Ended

        [ObservableProperty]
        private int _sortIndex = 0; // 0: Most Missing, 1: Least Missing, 2: Completion %, 3: Name

        [ObservableProperty]
        private int _totalScannedSeries;

        [ObservableProperty]
        private int _totalMissingSeries;

        [ObservableProperty]
        private int _totalMissingEpisodes;

        [ObservableProperty]
        private int _totalCompletedSeries;

        public ObservableCollection<MissingSeriesGroup> FilteredGroups { get; } = new();

        public MissingEpisodesToolViewModel()
        {
            _mediaService = new IdentifyMediaService();
            _ = ScanAsync();
        }

        [RelayCommand]
        public async Task ScanAsync()
        {
            if (IsScanning) return;

            IsScanning = true;
            StatusMessage = "در حال اسکن آرشیو و بررسی ردیاب سریال‌ها...";

            try
            {
                await Task.Run(async () =>
                {
                    using var db = new AppDbContext();

                    // Load all series files from local library
                    var seriesFiles = await db.VideoFiles.AsNoTracking()
                        .Where(v => v.MediaType == "Series")
                        .ToListAsync();

                    if (seriesFiles.Count == 0)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            TotalScannedSeries = 0;
                            TotalMissingSeries = 0;
                            TotalMissingEpisodes = 0;
                            TotalCompletedSeries = 0;
                            _allMissingGroups.Clear();
                            FilteredGroups.Clear();
                            StatusMessage = "هیچ سریالی در آرشیو یافت نشد.";
                        });
                        return;
                    }

                    // 1. Single database query for all TvEpisodes in memory
                    var allDbEpisodes = await db.TvEpisodes.AsNoTracking()
                        .Where(e => e.SeasonNumber > 0)
                        .ToListAsync();

                    var episodesBySeriesId = allDbEpisodes
                        .GroupBy(e => e.TmdbSeriesId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).ToList());

                    // 2. Pre-index seriesFiles in memory by TmdbId and by FormattedTitle for O(1) lookups
                    var filesByTmdbId = seriesFiles
                        .Where(f => f.TmdbId.HasValue && f.TmdbId.Value > 0)
                        .GroupBy(f => f.TmdbId!.Value)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    var filesByTitle = seriesFiles
                        .Where(f => !string.IsNullOrWhiteSpace(f.FormattedTitle))
                        .GroupBy(f => f.FormattedTitle!.Trim().ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // 3. Group by TMDb ID or title to get unique series
                    var distinctSeries = seriesFiles
                        .GroupBy(v => v.TmdbId.HasValue && v.TmdbId.Value > 0 
                            ? (object)v.TmdbId.Value 
                            : (object)(v.FormattedTitle ?? v.FileName ?? string.Empty).Trim().ToLowerInvariant())
                        .Select(g => g.First())
                        .ToList();

                    var missingList = new List<MissingSeriesGroup>();
                    int completedCount = 0;
                    int totalMissingEps = 0;

                    var today = DateTime.Today;

                    foreach (var s in distinctSeries)
                    {
                        List<VideoFile>? localFiles = null;
                        if (s.TmdbId.HasValue && filesByTmdbId.TryGetValue(s.TmdbId.Value, out var byId))
                        {
                            localFiles = byId;
                        }
                        else if (!string.IsNullOrWhiteSpace(s.FormattedTitle) && filesByTitle.TryGetValue(s.FormattedTitle.Trim().ToLowerInvariant(), out var byTitle))
                        {
                            localFiles = byTitle;
                        }
                        else
                        {
                            localFiles = new List<VideoFile> { s };
                        }

                        var localSet = new HashSet<(int Season, int Episode)>();
                        foreach (var lf in localFiles)
                        {
                            if (lf.Season.HasValue && lf.Episode.HasValue)
                            {
                                localSet.Add((lf.Season.Value, lf.Episode.Value));
                            }
                        }

                        List<TvEpisode>? dbEpisodes = null;
                        if (s.TmdbId.HasValue)
                        {
                            episodesBySeriesId.TryGetValue(s.TmdbId.Value, out dbEpisodes);
                        }
                        dbEpisodes ??= new List<TvEpisode>();

                        var missingEpisodesForThisSeries = new List<MissingEpisodeInfo>();
                        int airedCount = 0;
                        bool needsSync = false;
                        string seriesTitle = !string.IsNullOrWhiteSpace(s.FormattedTitle) ? s.FormattedTitle : s.FileName;

                        if (dbEpisodes.Count > 0)
                        {
                            foreach (var ep in dbEpisodes)
                            {
                                bool hasAired = false;
                                if (!string.IsNullOrWhiteSpace(ep.AirDate))
                                {
                                    if (DateTime.TryParse(ep.AirDate, out var dt))
                                    {
                                        hasAired = dt.Date <= today;
                                    }
                                }
                                else
                                {
                                    if ((s.SeriesStatus ?? string.Empty).Contains("end", StringComparison.OrdinalIgnoreCase))
                                    {
                                        hasAired = true;
                                    }
                                }

                                if (hasAired)
                                {
                                    airedCount++;
                                    if (!localSet.Contains((ep.SeasonNumber, ep.EpisodeNumber)))
                                    {
                                        missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                        {
                                            SeriesTitle = seriesTitle,
                                            SeasonNumber = ep.SeasonNumber,
                                            EpisodeNumber = ep.EpisodeNumber,
                                            EpisodeName = ep.Name,
                                            AirDate = ep.AirDate,
                                            StillUrl = ep.StillUrl,
                                            Overview = ep.Overview
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Fallback: TvEpisodes not yet populated in local database!
                            // Use TotalEpisodesCount, TotalSeasonsCount, and local files
                            int expectedTotal = localFiles.Max(f => f.TotalEpisodesCount ?? f.NumberOfEpisodes ?? 0);
                            int expectedSeasons = localFiles.Max(f => f.TotalSeasonsCount ?? f.NumberOfSeasons ?? 0);
                            if (expectedSeasons <= 0) expectedSeasons = 1;

                            needsSync = s.TmdbId.HasValue && s.TmdbId.Value > 0;

                            if (expectedTotal > 0 && localFiles.Count < expectedTotal)
                            {
                                airedCount = expectedTotal;
                                int missingCount = expectedTotal - localFiles.Count;

                                var seasonsWithFiles = localFiles
                                    .Where(f => f.Season.HasValue && f.Season.Value > 0)
                                    .Select(f => f.Season!.Value)
                                    .Distinct()
                                    .ToHashSet();

                                // 1. Check for gaps in existing seasons
                                foreach (var sn in seasonsWithFiles)
                                {
                                    var epsInSeason = localFiles
                                        .Where(f => f.Season == sn && f.Episode.HasValue && f.Episode.Value > 0)
                                        .Select(f => f.Episode!.Value)
                                        .OrderBy(x => x)
                                        .ToList();

                                    if (epsInSeason.Count > 0)
                                    {
                                        int maxEp = epsInSeason.Max();
                                        for (int e = 1; e < maxEp; e++)
                                        {
                                            if (!localSet.Contains((sn, e)))
                                            {
                                                missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                                {
                                                    SeriesTitle = seriesTitle,
                                                    SeasonNumber = sn,
                                                    EpisodeNumber = e,
                                                    EpisodeName = $"قسمت {e}",
                                                    AirDate = null
                                                });
                                            }
                                        }
                                    }
                                }

                                // 2. Identify completely missing seasons (like Season 1 for 1923!)
                                var missingSeasons = new List<int>();
                                for (int sn = 1; sn <= expectedSeasons; sn++)
                                {
                                    if (!seasonsWithFiles.Contains(sn))
                                    {
                                        missingSeasons.Add(sn);
                                    }
                                }

                                int remainingMissingCount = missingCount - missingEpisodesForThisSeries.Count;
                                if (remainingMissingCount > 0)
                                {
                                    if (missingSeasons.Count == 1)
                                    {
                                        int targetSeason = missingSeasons[0];
                                        for (int epNum = 1; epNum <= remainingMissingCount; epNum++)
                                        {
                                            missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                            {
                                                SeriesTitle = seriesTitle,
                                                SeasonNumber = targetSeason,
                                                EpisodeNumber = epNum,
                                                EpisodeName = $"قسمت {epNum} (فصل {targetSeason})",
                                                AirDate = null
                                            });
                                        }
                                    }
                                    else if (missingSeasons.Count > 1)
                                    {
                                        int perSeason = remainingMissingCount / missingSeasons.Count;
                                        int remainder = remainingMissingCount % missingSeasons.Count;

                                        for (int i = 0; i < missingSeasons.Count; i++)
                                        {
                                            int sn = missingSeasons[i];
                                            int countForThisSeason = perSeason + (i < remainder ? 1 : 0);
                                            for (int epNum = 1; epNum <= countForThisSeason; epNum++)
                                            {
                                                missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                                {
                                                    SeriesTitle = seriesTitle,
                                                    SeasonNumber = sn,
                                                    EpisodeNumber = epNum,
                                                    EpisodeName = $"قسمت {epNum} (فصل {sn})",
                                                    AirDate = null
                                                });
                                            }
                                        }
                                    }
                                    else
                                    {
                                        int latestSeason = seasonsWithFiles.Count > 0 ? seasonsWithFiles.Max() : 1;
                                        int maxExistingEp = localFiles.Where(f => f.Season == latestSeason && f.Episode.HasValue)
                                                                      .Select(f => f.Episode!.Value)
                                                                      .DefaultIfEmpty(0)
                                                                      .Max();

                                        for (int i = 1; i <= remainingMissingCount; i++)
                                        {
                                            int epNum = maxExistingEp + i;
                                            missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                            {
                                                SeriesTitle = seriesTitle,
                                                SeasonNumber = latestSeason,
                                                EpisodeNumber = epNum,
                                                EpisodeName = $"قسمت {epNum}",
                                                AirDate = null
                                            });
                                        }
                                    }
                                }
                            }
                            else if (localFiles.Count > 0)
                            {
                                // Gaps detection even if expectedTotal is missing
                                var seasonsWithFiles = localFiles
                                    .Where(f => f.Season.HasValue && f.Season.Value > 0)
                                    .Select(f => f.Season!.Value)
                                    .Distinct()
                                    .OrderBy(x => x);

                                foreach (var sn in seasonsWithFiles)
                                {
                                    var eps = localFiles.Where(f => f.Season == sn && f.Episode.HasValue && f.Episode.Value > 0)
                                                        .Select(f => f.Episode!.Value)
                                                        .OrderBy(x => x)
                                                        .ToList();
                                    if (eps.Count > 0)
                                    {
                                        int maxEp = eps.Max();
                                        for (int e = 1; e < maxEp; e++)
                                        {
                                            if (!localSet.Contains((sn, e)))
                                            {
                                                missingEpisodesForThisSeries.Add(new MissingEpisodeInfo
                                                {
                                                    SeriesTitle = seriesTitle,
                                                    SeasonNumber = sn,
                                                    EpisodeNumber = e,
                                                    EpisodeName = $"قسمت {e}",
                                                    AirDate = null
                                                });
                                            }
                                        }
                                    }
                                }
                                if (missingEpisodesForThisSeries.Count > 0)
                                {
                                    airedCount = localFiles.Count + missingEpisodesForThisSeries.Count;
                                }
                            }
                        }

                        if (missingEpisodesForThisSeries.Count > 0)
                        {
                            var group = new MissingSeriesGroup
                            {
                                SeriesId = s.Id,
                                TmdbId = s.TmdbId,
                                Title = seriesTitle,
                                Year = s.Year,
                                PosterUrl = s.PosterUrl,
                                BackdropUrl = s.BackdropUrl,
                                Genres = s.Genres,
                                SeriesStatus = s.SeriesStatus,
                                TotalAiredEpisodes = airedCount,
                                TotalLocalEpisodes = localSet.Count,
                                NeedsOnlineSync = needsSync,
                                MissingEpisodes = new ObservableCollection<MissingEpisodeInfo>(missingEpisodesForThisSeries)
                            };

                            missingList.Add(group);
                            totalMissingEps += missingEpisodesForThisSeries.Count;
                        }
                        else
                        {
                            completedCount++;
                        }
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TotalScannedSeries = distinctSeries.Count;
                        TotalMissingSeries = missingList.Count;
                        TotalMissingEpisodes = totalMissingEps;
                        TotalCompletedSeries = completedCount;
                        _allMissingGroups = missingList;

                        ApplyFilterAndSort();
                        StatusMessage = $"اسکن کامل شد. {missingList.Count} سریال دارای قسمت ناقص شناسایی شدند.";
                    });
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در اسکن: {ex.Message}";
                ToastService.Instance.ShowError($"خطا در اسکن: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ApplyFilterAndSort()
        {
            var query = _allMissingGroups.AsEnumerable();

            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLowerInvariant();
                query = query.Where(g => g.Title.ToLowerInvariant().Contains(q));
            }

            // Status Filter
            if (FilterIndex == 1) // Ongoing
            {
                query = query.Where(g => g.IsOngoing);
            }
            else if (FilterIndex == 2) // Ended
            {
                query = query.Where(g => !g.IsOngoing);
            }

            // Sort
            query = SortIndex switch
            {
                0 => query.OrderByDescending(g => g.MissingCount),
                1 => query.OrderBy(g => g.MissingCount),
                2 => query.OrderBy(g => g.ProgressPercent),
                3 => query.OrderBy(g => g.Title),
                _ => query.OrderByDescending(g => g.MissingCount)
            };

            FilteredGroups.Clear();
            foreach (var item in query)
            {
                FilteredGroups.Add(item);
            }
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();
        partial void OnFilterIndexChanged(int value) => ApplyFilterAndSort();
        partial void OnSortIndexChanged(int value) => ApplyFilterAndSort();

        [RelayCommand]
        private void ToggleExpand(MissingSeriesGroup? group)
        {
            if (group == null) return;
            group.IsExpanded = !group.IsExpanded;
        }

        [RelayCommand]
        private void SearchDownloadForSeries(MissingSeriesGroup? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Title)) return;

            string query = $"دانلود سریال {group.Title}";

            try
            {
                Clipboard.SetText(query);
            }
            catch { }

            try
            {
                string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = searchUrl,
                    UseShellExecute = true
                });

                ToastService.Instance.ShowSuccess($"جستجوی «{query}» در مرورگر باز شد (در کلیپ‌بورد نیز کپی شد).");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در باز کردن مرورگر: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SearchDownloadForEpisode(MissingEpisodeInfo? ep)
        {
            if (ep == null) return;

            string query = !string.IsNullOrEmpty(ep.SeriesTitle) 
                ? $"دانلود سریال {ep.SeriesTitle} {ep.EpisodeCode}" 
                : $"دانلود {ep.EpisodeCode}";

            try
            {
                Clipboard.SetText(query);
            }
            catch { }

            try
            {
                string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = searchUrl,
                    UseShellExecute = true
                });

                ToastService.Instance.ShowSuccess($"جستجوی «{query}» در مرورگر باز شد.");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در باز کردن مرورگر: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopySeriesTitle(MissingSeriesGroup? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Title)) return;

            try
            {
                Clipboard.SetText(group.Title);
                ToastService.Instance.ShowSuccess($"نام سریال «{group.Title}» در کلیپ‌بورد کپی شد.");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در کپی نام سریال: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyMissingForSeries(MissingSeriesGroup? group)
        {
            if (group == null || group.MissingEpisodes.Count == 0) return;

            var lines = group.MissingEpisodes.Select(ep => $"{group.Title} {ep.EpisodeCode}");
            var text = string.Join(Environment.NewLine, lines);

            try
            {
                Clipboard.SetText(text);
                ToastService.Instance.ShowSuccess($"لیست {group.MissingEpisodes.Count} قسمت کسری «{group.Title}» در کلیپ‌بورد کپی شد.");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در کپی: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyAllMissing()
        {
            if (FilteredGroups.Count == 0)
            {
                ToastService.Instance.ShowWarning("هیچ موردی برای کپی وجود ندارد.");
                return;
            }

            var allLines = new List<string>();
            foreach (var g in FilteredGroups)
            {
                foreach (var ep in g.MissingEpisodes)
                {
                    allLines.Add($"{g.Title} {ep.EpisodeCode}");
                }
            }

            if (allLines.Count == 0) return;

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, allLines));
                ToastService.Instance.ShowSuccess($"لیست {allLines.Count} قسمت کسری در کلیپ‌بورد کپی شد.");
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در کپی: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshOnlineForSeriesAsync(MissingSeriesGroup? group)
        {
            if (group == null || !group.TmdbId.HasValue) return;

            group.IsUpdating = true;
            try
            {
                var tmdbId = group.TmdbId.Value;
                var (seasons, episodes) = await _mediaService.FetchSeriesDetailsAsync(tmdbId);

                if (episodes.Count > 0)
                {
                    await Task.Run(async () =>
                    {
                        using var db = new AppDbContext();
                        var oldEps = await db.TvEpisodes.Where(e => e.TmdbSeriesId == tmdbId).ToListAsync();
                        db.TvEpisodes.RemoveRange(oldEps);

                        var oldSeasons = await db.TvSeasons.Where(s => s.TmdbSeriesId == tmdbId).ToListAsync();
                        db.TvSeasons.RemoveRange(oldSeasons);

                        db.TvSeasons.AddRange(seasons);
                        db.TvEpisodes.AddRange(episodes);
                        await db.SaveChangesAsync();
                    });

                    ToastService.Instance.ShowSuccess($"اطلاعات قسمت‌های «{group.Title}» با موفقیت از اینترنت بروزرسانی شد.");
                    await ScanAsync();
                }
                else
                {
                    ToastService.Instance.ShowWarning("اطلاعات جدیدی از TMDb دریافت نشد.");
                }
            }
            catch (Exception ex)
            {
                ToastService.Instance.ShowError($"خطا در بروزرسانی: {ex.Message}");
            }
            finally
            {
                group.IsUpdating = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }
    }
}
