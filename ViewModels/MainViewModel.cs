using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        [ObservableProperty]
        private bool _isPlayerActive = false;

        partial void OnCurrentViewModelChanged(ObservableObject? value)
        {
            IsPlayerActive = value is PlayerViewModel;
        }

        private HomeViewModel? _homeViewModel;
        private MoviesViewModel? _moviesViewModel;
        private FavoritesViewModel? _favoritesViewModel;
        private CollectionsViewModel? _collectionsViewModel;

        private HomeViewModel GetHomeViewModel() => _homeViewModel ??= new HomeViewModel();
        private MoviesViewModel GetMoviesViewModel() => _moviesViewModel ??= new MoviesViewModel();
        private FavoritesViewModel GetFavoritesViewModel() => _favoritesViewModel ??= new FavoritesViewModel();
        private CollectionsViewModel GetCollectionsViewModel() => _collectionsViewModel ??= new CollectionsViewModel();

        public MainViewModel()
        {
            CurrentViewModel = GetHomeViewModel();

            // Register for navigation messages
            WeakReferenceMessenger.Default.Register<NavigationMessage>(this, (r, m) =>
            {
                if (m.ViewModel != null && m.ViewModel.GetType() == typeof(MoviesViewModel))
                {
                    CurrentViewModel = GetMoviesViewModel();
                }
                else if (m.ViewModel != null && m.ViewModel.GetType() == typeof(FavoritesViewModel))
                {
                    CurrentViewModel = GetFavoritesViewModel();
                }
                else if (m.ViewModel != null && m.ViewModel.GetType() == typeof(HomeViewModel))
                {
                    CurrentViewModel = GetHomeViewModel();
                }
                else
                {
                    CurrentViewModel = m.ViewModel;
                }
            });

            WeakReferenceMessenger.Default.Register<MediaUpdatedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _homeViewModel?.LoadHomeDataDirect();
                });
            });
        }

        [RelayCommand]
        private void NavigateToScan()
        {
            CurrentViewModel = new ScanViewModel();
        }

        [RelayCommand]
        private void NavigateToTools()
        {
            CurrentViewModel = new ToolsViewModel();
        }

        [RelayCommand]
        private void NavigateToMovies()
        {
            CurrentViewModel = GetMoviesViewModel();
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentViewModel = new SettingsViewModel();
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            CurrentViewModel = GetHomeViewModel();
        }

        [RelayCommand]
        private void NavigateToFavorites()
        {
            CurrentViewModel = GetFavoritesViewModel();
            _ = _favoritesViewModel?.LoadMoviesAsync();
        }

        [RelayCommand]
        private void NavigateToCollections()
        {
            CurrentViewModel = GetCollectionsViewModel();
            _ = _collectionsViewModel?.LoadCollectionsAsync();
        }

        [RelayCommand]
        private void NavigateToCalendar()
        {
            CurrentViewModel = new CalendarViewModel();
        }
    }
}
