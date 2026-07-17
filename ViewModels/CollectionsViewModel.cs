using CommunityToolkit.Mvvm.ComponentModel;
using MovieManagerDesktop.Data;
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

        private void FilterCollections()
        {
            Collections.Clear();
            var query = _searchQuery?.ToLowerInvariant() ?? "";

            var filtered = string.IsNullOrWhiteSpace(query) 
                ? _allItems 
                : _allItems.Where(c => c.Name.ToLowerInvariant().Contains(query));

            foreach (var c in filtered)
            {
                Collections.Add(c);
            }
        }
    }
}
