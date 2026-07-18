using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MovieManagerDesktop.ViewModels
{
    public partial class CollectionsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pageTitle = "مجموعه‌ها";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        private List<CollectionItemViewModel> _allItems = new();

        public ObservableCollection<CollectionItemViewModel> Collections { get; } = new();

        public CollectionsViewModel()
        {
            var settings = SettingsManager.LoadSettings();
            PosterSize = settings.PosterSize > 50 ? settings.PosterSize : 220;
            _ = LoadCollectionsAsync();
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterCollections();
        }

        public async Task LoadCollectionsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            
            try
            {
                var items = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var allVideos = db.VideoFiles.Where(v => !string.IsNullOrEmpty(v.CollectionName)).ToList();

                    var groups = allVideos.GroupBy(v => v.CollectionName)
                                          .Select(g => new CollectionItemViewModel(g.Key, g.Count(), g.FirstOrDefault(v => !string.IsNullOrEmpty(v.PosterUrl))?.PosterUrl))
                                          .OrderByDescending(c => c.MovieCount)
                                          .ToList();

                    return groups;
                });

                _allItems = items;
                FilterCollections();
            }
            catch { }
            finally
            {
                IsLoading = false;
            }
        }

        [ObservableProperty]
        private int _sortIndex = 0; // 0: Name (A-Z), 1: Items Count (High-Low)

        partial void OnSortIndexChanged(int value)
        {
            FilterCollections();
        }

        [ObservableProperty]
        private bool _showFilters;

        [RelayCommand]
        private void ToggleFilters()
        {
            ShowFilters = !ShowFilters;
        }

        [ObservableProperty]
        private int _posterSize = 220;
        
        public int PosterHeight => (int)(PosterSize * 1.5);

        partial void OnPosterSizeChanged(int value)
        {
            OnPropertyChanged(nameof(PosterHeight));
            // Save settings if you want, but for now just update the UI
            var settings = SettingsManager.LoadSettings();
            settings.PosterSize = value;
            SettingsManager.SaveSettings(settings);
        }

        [RelayCommand]
        private void ResetSize()
        {
            PosterSize = 220;
        }

        [ObservableProperty]
        private int _sortDirectionIndex = 0; // 0: نزولی, 1: صعودی

        partial void OnSortDirectionIndexChanged(int value)
        {
            FilterCollections();
        }

        private void FilterCollections()
        {
            Collections.Clear();
            string FixPersian(string? text) => (text ?? "").ToLowerInvariant().Replace("ي", "ی").Replace("ك", "ک");
            
            var query = FixPersian(_searchQuery);
            var searchTerms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var filtered = searchTerms.Length == 0
                ? _allItems 
                : _allItems.Where(c => 
                {
                    string name = FixPersian(c.Name);
                    return searchTerms.All(term => name.Contains(term));
                });

            bool isAscending = SortDirectionIndex == 1;

            if (SortIndex == 0) // Name
                filtered = isAscending ? filtered.OrderBy(c => c.Name) : filtered.OrderByDescending(c => c.Name);
            else if (SortIndex == 1) // Count
                filtered = isAscending ? filtered.OrderBy(c => c.MovieCount) : filtered.OrderByDescending(c => c.MovieCount);

            foreach (var c in filtered)
            {
                Collections.Add(c);
            }
        }
    }
}
