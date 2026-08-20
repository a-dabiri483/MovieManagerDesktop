using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.ViewModels
{
    public partial class CalendarDayItem : ObservableObject
    {
        [ObservableProperty]
        private int _dayNumber;

        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private bool _isToday;

        [ObservableProperty]
        private bool _isFriday;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int _moviesCount;

        [ObservableProperty]
        private int _seriesCount;

        public bool HasReleases => (MoviesCount + SeriesCount) > 0;

        public ObservableCollection<CalendarMediaItem> Releases { get; } = new();

        public string FirstPosterUrl => Releases.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.PosterUrl))?.PosterUrl ?? string.Empty;
    }

    public partial class CalendarMediaItem : ObservableObject
    {
        public int TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalTitle { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string BackdropPath { get; set; } = string.Empty;
        public string PosterUrl => !string.IsNullOrWhiteSpace(PosterPath) ? (PosterPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? PosterPath : $"https://image.tmdb.org/t/p/w500{PosterPath.TrimStart('/')}") : string.Empty;
        public string BackdropUrl => !string.IsNullOrWhiteSpace(BackdropPath) ? (BackdropPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? BackdropPath : $"https://image.tmdb.org/t/p/w780{BackdropPath.TrimStart('/')}") : string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public double VoteAverage { get; set; }
        public string Overview { get; set; } = string.Empty;
        public string MediaType { get; set; } = "movie"; // "movie" or "tv"
        public string Genres { get; set; } = string.Empty;
        public string FormattedGenres => GenreTranslatorService.TranslateList(Genres);
    }

    public partial class CalendarViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _monthName = string.Empty;

        public ObservableCollection<CalendarDayItem> Days { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isFetching;

        [ObservableProperty]
        private bool _hasCachedData;

        private DateTime _currentMonthDate;
        private readonly HttpClient _httpClient;
        private static readonly string CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CalendarCache");

        public CalendarViewModel()
        {
            _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
            _currentMonthDate = DateTime.Today;
            BuildCalendarGrid();
        }

        [RelayCommand]
        private void NextMonth()
        {
            if (DateTimeFormatterService.IsJalali)
            {
                var pc = new System.Globalization.PersianCalendar();
                _currentMonthDate = pc.AddMonths(_currentMonthDate, 1);
            }
            else
            {
                _currentMonthDate = _currentMonthDate.AddMonths(1);
            }
            BuildCalendarGrid();
        }

        [RelayCommand]
        private void PreviousMonth()
        {
            if (DateTimeFormatterService.IsJalali)
            {
                var pc = new System.Globalization.PersianCalendar();
                _currentMonthDate = pc.AddMonths(_currentMonthDate, -1);
            }
            else
            {
                _currentMonthDate = _currentMonthDate.AddMonths(-1);
            }
            BuildCalendarGrid();
        }

        [RelayCommand]
        private void SelectDay(CalendarDayItem day)
        {
            if (day == null || day.DayNumber == 0 || day.Releases.Count == 0) return;
            
            // Open DayDetailWindow
            var window = new MovieManagerDesktop.Views.DayDetailWindow(day);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        private string GetMonthKey()
        {
            if (DateTimeFormatterService.IsJalali)
            {
                var pc = new System.Globalization.PersianCalendar();
                int year = pc.GetYear(_currentMonthDate);
                int month = pc.GetMonth(_currentMonthDate);
                return $"JALALI_{year}_{month}";
            }
            else
            {
                return $"GREG_{_currentMonthDate.Year}_{_currentMonthDate.Month}";
            }
        }

        private void BuildCalendarGrid()
        {
            Days.Clear();
            if (DateTimeFormatterService.IsJalali)
            {
                var pc = new System.Globalization.PersianCalendar();
                int currentYear = pc.GetYear(_currentMonthDate);
                int currentMonth = pc.GetMonth(_currentMonthDate);
                
                string monthName = DateTimeFormatterService.GetJalaliMonthName(currentMonth);
                MonthName = $"{monthName} {currentYear}";

                int daysInMonth = pc.GetDaysInMonth(currentYear, currentMonth);
                DateTime firstDay = pc.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);

                int firstDayOfWeek = (int)pc.GetDayOfWeek(firstDay);
                int paddingDays = (firstDayOfWeek + 1) % 7;

                for (int i = 0; i < paddingDays; i++)
                {
                    Days.Add(new CalendarDayItem { DayNumber = 0 });
                }

                for (int i = 1; i <= daysInMonth; i++)
                {
                    DateTime date = pc.ToDateTime(currentYear, currentMonth, i, 0, 0, 0, 0);
                    var dayItem = new CalendarDayItem
                    {
                        DayNumber = i,
                        Date = date,
                        IsToday = date.Date == DateTime.Today,
                        IsFriday = date.DayOfWeek == DayOfWeek.Friday
                    };
                    Days.Add(dayItem);
                }
            }
            else
            {
                int currentYear = _currentMonthDate.Year;
                int currentMonth = _currentMonthDate.Month;
                string monthName = DateTimeFormatterService.GetGregorianMonthName(currentMonth);
                MonthName = $"{monthName} {currentYear}";

                int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
                DateTime firstDay = new DateTime(currentYear, currentMonth, 1);

                int firstDayOfWeek = (int)firstDay.DayOfWeek;
                int paddingDays = firstDayOfWeek;

                for (int i = 0; i < paddingDays; i++)
                {
                    Days.Add(new CalendarDayItem { DayNumber = 0 });
                }

                for (int i = 1; i <= daysInMonth; i++)
                {
                    DateTime date = new DateTime(currentYear, currentMonth, i);
                    var dayItem = new CalendarDayItem
                    {
                        DayNumber = i,
                        Date = date,
                        IsToday = date.Date == DateTime.Today,
                        IsFriday = date.DayOfWeek == DayOfWeek.Sunday
                    };
                    Days.Add(dayItem);
                }
            }

            // Try to load cached data for this month
            LoadCachedData();
        }

        private void LoadCachedData()
        {
            try
            {
                string cacheFile = Path.Combine(CacheDir, GetMonthKey() + ".json");
                if (File.Exists(cacheFile))
                {
                    string json = File.ReadAllText(cacheFile);
                    var cached = JsonSerializer.Deserialize<List<CalendarMediaItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cached != null && cached.Count > 0)
                    {
                        ApplyReleasesToDays(cached);
                        HasCachedData = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error loading calendar cache", ex);
            }

            HasCachedData = false;
        }

        [RelayCommand]
        private async Task FetchMonthContentAsync()
        {
            IsFetching = true;
            try
            {
                DateTime firstDay;
                DateTime lastDay;

                if (DateTimeFormatterService.IsJalali)
                {
                    var pc = new System.Globalization.PersianCalendar();
                    int currentYear = pc.GetYear(_currentMonthDate);
                    int currentMonth = pc.GetMonth(_currentMonthDate);
                    int daysInMonth = pc.GetDaysInMonth(currentYear, currentMonth);

                    firstDay = pc.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);
                    lastDay = pc.ToDateTime(currentYear, currentMonth, daysInMonth, 0, 0, 0, 0);
                }
                else
                {
                    int currentYear = _currentMonthDate.Year;
                    int currentMonth = _currentMonthDate.Month;
                    int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

                    firstDay = new DateTime(currentYear, currentMonth, 1);
                    lastDay = new DateTime(currentYear, currentMonth, daysInMonth);
                }

                var allItems = await FetchReleasesFromTmdbAsync(firstDay, lastDay);

                // Flatten for caching
                var flatList = allItems.SelectMany(kvp => kvp.Value).ToList();

                // Save to cache
                SaveCache(flatList);

                // Apply to days
                ApplyReleasesToDays(flatList);
                HasCachedData = true;

                ToastService.Instance.ShowSuccess($"عناوین ماه {MonthName} با موفقیت دریافت شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error fetching month content", ex);
                ToastService.Instance.ShowError($"خطا در دریافت عناوین: {ex.Message}");
            }
            finally
            {
                IsFetching = false;
            }
        }

        private void SaveCache(List<CalendarMediaItem> items)
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                string cacheFile = Path.Combine(CacheDir, GetMonthKey() + ".json");
                string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error saving calendar cache", ex);
            }
        }

        private void ApplyReleasesToDays(List<CalendarMediaItem> items)
        {
            // Clear existing releases in days
            foreach (var day in Days)
            {
                day.Releases.Clear();
                day.MoviesCount = 0;
                day.SeriesCount = 0;
            }

            foreach (var item in items)
            {
                if (DateTime.TryParse(item.ReleaseDate, out DateTime releaseDate))
                {
                    var targetDay = Days.FirstOrDefault(d => d.Date.Date == releaseDate.Date);
                    if (targetDay != null)
                    {
                        targetDay.Releases.Add(item);
                        if (item.MediaType == "movie")
                            targetDay.MoviesCount++;
                        else
                            targetDay.SeriesCount++;
                    }
                }
            }
        }

        private async Task<Dictionary<DateTime, List<CalendarMediaItem>>> FetchReleasesFromTmdbAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, List<CalendarMediaItem>>();
            string apiKey = SettingsManager.GetTmdbApiKey();
            string startStr = startDate.ToString("yyyy-MM-dd");
            string endStr = endDate.ToString("yyyy-MM-dd");

            // Fetch Movies
            string movieUrl = $"https://api.themoviedb.org/3/discover/movie?api_key={apiKey}&primary_release_date.gte={startStr}&primary_release_date.lte={endStr}&sort_by=popularity.desc&page=1";
            await FetchAndParseAsync(movieUrl, "movie", result);

            // Fetch TV Shows
            string tvUrl = $"https://api.themoviedb.org/3/discover/tv?api_key={apiKey}&first_air_date.gte={startStr}&first_air_date.lte={endStr}&sort_by=popularity.desc&page=1";
            await FetchAndParseAsync(tvUrl, "tv", result);

            return result;
        }

        private async Task FetchAndParseAsync(string url, string mediaType, Dictionary<DateTime, List<CalendarMediaItem>> result)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var el in results.EnumerateArray())
                        {
                            string dateField = mediaType == "movie" ? "release_date" : "first_air_date";
                            string titleField = mediaType == "movie" ? "title" : "name";
                            string originalTitleField = mediaType == "movie" ? "original_title" : "original_name";

                            string dateStr = el.TryGetProperty(dateField, out var d) ? d.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out DateTime releaseDate))
                            {
                                int id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                                string title = el.TryGetProperty(titleField, out var t) ? t.GetString() ?? "" : "";
                                string originalTitle = el.TryGetProperty(originalTitleField, out var ot) ? ot.GetString() ?? "" : "";
                                string posterPath = el.TryGetProperty("poster_path", out var pp) ? pp.GetString() ?? "" : "";
                                string backdropPath = el.TryGetProperty("backdrop_path", out var bp) ? bp.GetString() ?? "" : "";
                                double voteAverage = el.TryGetProperty("vote_average", out var va) ? va.GetDouble() : 0.0;
                                string overview = el.TryGetProperty("overview", out var ov) ? ov.GetString() ?? "" : "";

                                var mediaItem = new CalendarMediaItem
                                {
                                    TmdbId = id,
                                    Title = title,
                                    OriginalTitle = originalTitle,
                                    PosterPath = !string.IsNullOrEmpty(posterPath) ? $"https://image.tmdb.org/t/p/w500{posterPath}" : "",
                                    BackdropPath = !string.IsNullOrEmpty(backdropPath) ? $"https://image.tmdb.org/t/p/w780{backdropPath}" : "",
                                    ReleaseDate = dateStr,
                                    VoteAverage = voteAverage,
                                    Overview = overview,
                                    MediaType = mediaType
                                };

                                if (!result.ContainsKey(releaseDate.Date))
                                {
                                    result[releaseDate.Date] = new List<CalendarMediaItem>();
                                }

                                result[releaseDate.Date].Add(mediaItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MovieManagerDesktop.Services.LoggerService.Error($"Error fetching {mediaType} calendar", ex);
            }
        }
    }
}
