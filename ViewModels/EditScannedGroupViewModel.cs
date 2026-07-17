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

            var results = await _identifyService.SearchMediaAsync(SearchQuery);

            foreach (var r in results)
            {
                SearchResults.Add(r);
            }

            IsSearching = false;
        }

        [RelayCommand]
        private async Task SelectResultAsync(TmdbSearchResult result)
        {
            if (result == null) return;

            _targetGroup.IdOverride = result.Id.ToString();
            _targetGroup.TitleOverride = result.Title;
            _targetGroup.YearOverride = result.ReleaseYear;

            CloseAction?.Invoke();
            await _parent.RetryGroupCommand.ExecuteAsync(_targetGroup);
        }
    }
}
