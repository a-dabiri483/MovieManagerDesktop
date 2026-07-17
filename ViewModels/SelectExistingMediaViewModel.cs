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
        private readonly ScannedGroupViewModel _targetGroup;
        private readonly ScanViewModel _parent;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public ObservableCollection<VideoFile> ExistingMedia { get; } = new();
        public System.Action CloseAction { get; set; }

        public SelectExistingMediaViewModel(ScannedGroupViewModel targetGroup, ScanViewModel parent)
        {
            _targetGroup = targetGroup;
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

            // Associate target group with this selected media
            _targetGroup.Representative.TmdbId = selected.TmdbId;
            _targetGroup.Representative.PosterUrl = selected.PosterUrl;
            _targetGroup.Representative.BackdropUrl = selected.BackdropUrl;
            _targetGroup.Representative.FormattedTitle = selected.FormattedTitle;
            _targetGroup.Representative.Year = selected.Year;
            _targetGroup.Representative.CollectionName = selected.CollectionName;
            _targetGroup.TitleOverride = selected.FormattedTitle;

            // Try to fetch full details and save
            CloseAction?.Invoke();
            await _parent.RetryGroupCommand.ExecuteAsync(_targetGroup);
        }
    }
}
