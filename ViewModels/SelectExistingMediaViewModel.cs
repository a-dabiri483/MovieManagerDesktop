using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class SelectExistingMediaViewModel : ObservableObject
    {
        private readonly System.Collections.Generic.List<ScannedGroupViewModel> _targetGroups;
        private readonly ScanViewModel _parent;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public ObservableCollection<VideoFile> ExistingMedia { get; } = new();
        public System.Action CloseAction { get; set; }

        public SelectExistingMediaViewModel(System.Collections.Generic.List<ScannedGroupViewModel> targetGroups, ScanViewModel parent)
        {
            _targetGroups = targetGroups;
            _parent = parent;
            LoadExistingMedia();
        }

        partial void OnSearchQueryChanged(string value)
        {
            LoadExistingMedia();
        }

        private void LoadExistingMedia()
        {
            using var db = new AppDbContext();
            var query = db.VideoFiles.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var search = SearchQuery.ToLowerInvariant();
                query = query.Where(v => v.FormattedTitle.ToLower().Contains(search) || (v.FileName != null && v.FileName.ToLower().Contains(search)));
            }

            // Get unique titles in memory to avoid EF projection issues
            var list = query
                .OrderBy(v => v.FormattedTitle)
                .Take(200)
                .AsEnumerable()
                .GroupBy(v => v.FormattedTitle)
                .Select(g => g.FirstOrDefault())
                .Where(v => v != null)
                .Take(50)
                .ToList();

            ExistingMedia.Clear();
            foreach (var item in list)
            {
                if(item != null)
                    ExistingMedia.Add(item);
            }
        }

        [RelayCommand]
        private async Task SelectMediaAsync(VideoFile selected)
        {
            if (selected == null) return;

            CloseAction?.Invoke();

            foreach (var group in _targetGroups)
            {
                // Associate target group with this selected media
                group.Representative.TmdbId = selected.TmdbId;
                group.Representative.PosterUrl = selected.PosterUrl;
                group.Representative.BackdropUrl = selected.BackdropUrl;
                group.Representative.FormattedTitle = selected.FormattedTitle;
                group.Representative.Year = selected.Year;
                group.Representative.CollectionName = selected.CollectionName;
                group.TitleOverride = selected.FormattedTitle;

                // Try to fetch full details and save
                await _parent.RetryGroupCommand.ExecuteAsync(group);
            }
        }
    }
}
