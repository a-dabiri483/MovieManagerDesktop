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

        private readonly MoviesViewModel _moviesViewModel = new MoviesViewModel();
        private readonly FavoritesViewModel _favoritesViewModel = new FavoritesViewModel();

        private readonly CollectionsViewModel _collectionsViewModel = new CollectionsViewModel();


        public MainViewModel()
        {
            CurrentViewModel = new HomeViewModel();

            // Register for navigation messages
            WeakReferenceMessenger.Default.Register<NavigationMessage>(this, (r, m) =>
            {
                if (m.ViewModel != null && m.ViewModel.GetType() == typeof(MoviesViewModel))
                {
                    CurrentViewModel = _moviesViewModel;
                }
                else if (m.ViewModel != null && m.ViewModel.GetType() == typeof(FavoritesViewModel))
                {
                    CurrentViewModel = _favoritesViewModel;
                }

                else
                {
                    CurrentViewModel = m.ViewModel;
                }
            });

            WeakReferenceMessenger.Default.Register<MediaUpdatedMessage>(this, (r, m) =>
            {
                if (CurrentViewModel is HomeViewModel)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentViewModel = new HomeViewModel();
                    });
                }
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
            CurrentViewModel = _moviesViewModel;
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentViewModel = new SettingsViewModel();
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            CurrentViewModel = new HomeViewModel();
        }

        [RelayCommand]
        private void NavigateToFavorites()
        {
            CurrentViewModel = _favoritesViewModel;
            _ = _favoritesViewModel.LoadMoviesAsync();
        }





        [RelayCommand]
        private void NavigateToCollections()
        {
            CurrentViewModel = _collectionsViewModel;
            _ = _collectionsViewModel.LoadCollectionsAsync();
        }
    }
}
