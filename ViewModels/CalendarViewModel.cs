using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
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
        private string _firstPosterUrl;

        public ObservableCollection<CalendarMediaItem> Releases { get; } = new();
    }

    public partial class CalendarMediaItem : ObservableObject
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string PosterUrl { get; set; }
        public string MediaType { get; set; }
        public string Overview { get; set; }
        public string ReleaseDate { get; set; }
    }

    public partial class CalendarViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _monthName;

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
            var pc = new System.Globalization.PersianCalendar();
            _currentMonthDate = pc.AddMonths(_currentMonthDate, 1);
            BuildCalendarGrid();
        }

        [RelayCommand]
        private void PreviousMonth()
        {
            var pc = new System.Globalization.PersianCalendar();
            _currentMonthDate = pc.AddMonths(_currentMonthDate, -1);
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
            var pc = new System.Globalization.PersianCalendar();
            int year = pc.GetYear(_currentMonthDate);
            int month = pc.GetMonth(_currentMonthDate);
            return $"JALALI_{year}_{month}";
        }

        private void BuildCalendarGrid()
        {
            Days.Clear();
            var pc = new System.Globalization.PersianCalendar();
            
            int currentYear = pc.GetYear(_currentMonthDate);
            int currentMonth = pc.GetMonth(_currentMonthDate);
            
            string[] persianMonths = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
            MonthName = $"{persianMonths[currentMonth]} {currentYear}";

            int daysInMonth = pc.GetDaysInMonth(currentYear, currentMonth);
            
            DateTime firstDay = pc.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);

            // Saturday=0 in Persian week. DayOfWeek: Saturday=6 in .NET
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
                var pc = new System.Globalization.PersianCalendar();
                int currentYear = pc.GetYear(_currentMonthDate);
                int currentMonth = pc.GetMonth(_currentMonthDate);
                int daysInMonth = pc.GetDaysInMonth(currentYear, currentMonth);

                DateTime firstDay = pc.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);
                DateTime lastDay = pc.ToDateTime(currentYear, currentMonth, daysInMonth, 0, 0, 0, 0);

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
                if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);
                string cacheFile = Path.Combine(CacheDir, GetMonthKey() + ".json");
                var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Error saving calendar cache", ex);
            }
        }

        private void ApplyReleasesToDays(List<CalendarMediaItem> items)
        {
            // Clear existing releases
            foreach (var day in Days)
            {
                day.Releases.Clear();
                day.FirstPosterUrl = null;
            }

            foreach (var day in Days)
            {
                if (day.DayNumber == 0) continue;
                string dateStr = day.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var dayItems = items.Where(i => i.ReleaseDate == dateStr).ToList();
                foreach (var item in dayItems)
                {
                    day.Releases.Add(item);
                }
                if (dayItems.Count > 0)
                {
                    day.FirstPosterUrl = dayItems.FirstOrDefault(i => !string.IsNullOrEmpty(i.PosterUrl))?.PosterUrl;
                }
            }
        }

        private async Task<Dictionary<DateTime, List<CalendarMediaItem>>> FetchReleasesFromTmdbAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, List<CalendarMediaItem>>();
            string apiKey = SettingsManager.GetTmdbApiKey();

            string gte = startDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string lte = endDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            var tasks = new List<Task>();
            for (int i = 1; i <= 2; i++)
            {
                // Fetch TV Shows
                string tvUrl = $"https://api.themoviedb.org/3/discover/tv?api_key={apiKey}&first_air_date.gte={gte}&first_air_date.lte={lte}&sort_by=popularity.desc&page={i}";
                // Fetch Movies
                string movieUrl = $"https://api.themoviedb.org/3/discover/movie?api_key={apiKey}&primary_release_date.gte={gte}&primary_release_date.lte={lte}&sort_by=popularity.desc&page={i}";
                
                tasks.Add(FetchAndParseAsync(tvUrl, "Series", result));
                tasks.Add(FetchAndParseAsync(movieUrl, "Movie", result));
            }

            await Task.WhenAll(tasks);

            return result;
        }

        private async Task FetchAndParseAsync(string url, string mediaType, Dictionary<DateTime, List<CalendarMediaItem>> result)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("results", out var resultsArray))
                    {
                        foreach (var item in resultsArray.EnumerateArray())
                        {
                            string dateStr = "";
                            if (mediaType == "Series" && item.TryGetProperty("first_air_date", out var fa)) dateStr = fa.GetString();
                            if (mediaType == "Movie" && item.TryGetProperty("release_date", out var rd)) dateStr = rd.GetString();

                            if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime releaseDate))
                            {
                                string title = "";
                                if (item.TryGetProperty("name", out var n)) title = n.GetString();
                                else if (item.TryGetProperty("title", out var t)) title = t.GetString();
                                else if (item.TryGetProperty("original_name", out var on)) title = on.GetString();

                                string posterPath = "";
                                if (item.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null)
                                    posterPath = $"https://image.tmdb.org/t/p/w200{pp.GetString()}";

                                string overview = "";
                                if (item.TryGetProperty("overview", out var ov) && ov.ValueKind != JsonValueKind.Null)
                                    overview = ov.GetString();

                                int id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;

                                var mediaItem = new CalendarMediaItem
                                {
                                    Id = id,
                                    Title = title,
                                    PosterUrl = posterPath,
                                    MediaType = mediaType,
                                    Overview = overview,
                                    ReleaseDate = dateStr
                                };

                                lock (result)
                                {
                                    if (!result.ContainsKey(releaseDate.Date))
                                        result[releaseDate.Date] = new List<CalendarMediaItem>();
                                    
                                    result[releaseDate.Date].Add(mediaItem);
                                }
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
