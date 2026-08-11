using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ApiSearchDialogViewModel : ObservableObject
    {
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
        public System.Action<TmdbSearchResult> SelectAction { get; set; }

        public ApiSearchDialogViewModel(string initialQuery)
        {
            _identifyService = new IdentifyMediaService();
            SearchQuery = initialQuery;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || IsSearching) return;

            IsSearching = true;
            SearchResults.Clear();
            LoggerService.Info($"[Search] Manual search started for '{SearchQuery}'");

            System.Collections.Generic.List<TmdbSearchResult> results;

            if (IsSearchTypeAnime)
            {
                results = await _identifyService.SearchAnimeManualAsync(SearchQuery);
            }
            else
            {
                results = await _identifyService.SearchMediaAsync(SearchQuery);
                // Optional filtering based on RadioButtons
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

            if (results != null)
            {
                foreach (var r in results)
                {
                    SearchResults.Add(r);
                }
            }

            LoggerService.Info($"[Search] Found {SearchResults.Count} results for '{SearchQuery}'");
            IsSearching = false;
        }

        [RelayCommand]
        private void SelectResult(TmdbSearchResult result)
        {
            if (result == null) return;
            SelectAction?.Invoke(result);
            CloseAction?.Invoke();
        }
        
        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}
