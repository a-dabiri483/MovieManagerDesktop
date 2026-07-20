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

            var results = await _identifyService.SearchMediaAsync(SearchQuery);

            if (results != null)
            {
                foreach (var r in results)
                {
                    SearchResults.Add(r);
                }
            }

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
