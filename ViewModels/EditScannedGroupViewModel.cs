using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using MovieManagerDesktop.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class EditScannedGroupViewModel : ObservableObject
    {
        private readonly ScannedGroupViewModel _targetGroup;
        private readonly ScanViewModel _parent;
        private readonly IdentifyMediaService _identifyService;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _isSearchTypeMovie = true;

        [ObservableProperty]
        private bool _isSearchTypeSeries = false;

        [ObservableProperty]
        private bool _isSearchTypeAnime = false;

        public ObservableCollection<TmdbSearchResult> SearchResults { get; } = new();
        public System.Action CloseAction { get; set; }

        public EditScannedGroupViewModel(ScannedGroupViewModel targetGroup, ScanViewModel parent)
        {
            _targetGroup = targetGroup;
            _parent = parent;
            _identifyService = new IdentifyMediaService();
            
            // Default search query to current title override or actual title
            SearchQuery = string.IsNullOrWhiteSpace(targetGroup.TitleOverride) 
                ? targetGroup.Representative.FormattedTitle 
                : targetGroup.TitleOverride;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || IsSearching) return;

            IsSearching = true;
            SearchResults.Clear();
            
            LoggerService.Info($"[جستجوی دستی] شروع جستجو برای: {SearchQuery} (انیمه: {IsSearchTypeAnime})");

            try
            {
                System.Collections.Generic.List<TmdbSearchResult>? results = null;

                if (IsSearchTypeAnime)
                {
                    results = await _identifyService.SearchAnimeManualAsync(SearchQuery);
                }
                else
                {
                    results = await _identifyService.SearchMediaAsync(SearchQuery);
                    if (results != null)
                    {
                        if (IsSearchTypeMovie)
                        {
                            results = results.FindAll(r => (r.MediaType ?? "").ToLower() != "tv" && (r.MediaType ?? "").ToLower() != "series");
                        }
                        else if (IsSearchTypeSeries)
                        {
                            results = results.FindAll(r => (r.MediaType ?? "").ToLower() == "tv" || (r.MediaType ?? "").ToLower() == "series");
                        }
                    }
                }

                if (results != null && results.Count > 0)
                {
                    LoggerService.Info($"[جستجوی دستی] تعداد نتایج یافت شده: {results.Count}");
                    foreach (var r in results)
                    {
                        SearchResults.Add(r);
                    }
                }
                else
                {
                    ToastService.Instance.ShowWarning($"موردی برای «{SearchQuery}» یافت نشد. املای عنوان یا نوع جستجو را بررسی کنید.");
                }
            }
            catch (System.Exception ex)
            {
                LoggerService.Error($"[جستجوی دستی] خطا در جستجو: {ex.Message}", ex);
                string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم برقراری ارتباط با سرور. لطفاً وضعیت اینترنت یا قندشکن را بررسی کنید."
                    : $"خطا در جستجو: {ex.Message}";
                ToastService.Instance.ShowError(errMessage);
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private async Task SelectResultAsync(TmdbSearchResult result)
        {
            if (result == null) return;

            LoggerService.Info($"[جستجوی دستی] آیتم انتخاب شد: {result.Title} (ID: {result.Id}) - اعمال روی گروه...");

            _targetGroup.IdOverride = result.Id.ToString();
            _targetGroup.TitleOverride = result.Title;
            _targetGroup.YearOverride = result.ReleaseYear;

            CloseAction?.Invoke();
            await _parent.RetryGroupCommand.ExecuteAsync(_targetGroup);
        }
    }
}
