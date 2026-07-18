using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MovieManagerDesktop.ViewModels
{
    public partial class PosterSelectionViewModel : ObservableObject
    {
        private readonly List<string> _allPosters;
        private const int PageSize = 4;
        private int _currentIndex = 0;

        [ObservableProperty]
        private ObservableCollection<string> _posters = new();

        [ObservableProperty]
        private string? _selectedPosterUrl;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasMorePosters;

        public Action? RequestClose;

        public PosterSelectionViewModel(IEnumerable<string> posters)
        {
            _allPosters = posters.ToList();
            LoadMore();
        }

        [RelayCommand]
        private void LoadMore()
        {
            if (_currentIndex >= _allPosters.Count) return;

            var nextBatch = _allPosters.Skip(_currentIndex).Take(PageSize);
            foreach (var poster in nextBatch)
            {
                Posters.Add(poster);
            }
            
            _currentIndex += PageSize;
            HasMorePosters = _currentIndex < _allPosters.Count;
        }

        [RelayCommand]
        private void SelectPoster(string url)
        {
            SelectedPosterUrl = url;
            RequestClose?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            SelectedPosterUrl = null;
            RequestClose?.Invoke();
        }
    }
}
