using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace MovieManagerDesktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        [ObservableProperty]
        private bool _isPlayerActive = false;

        partial void OnCurrentViewModelChanged(ObservableObject value)
        {
            IsPlayerActive = false;
        }

        private HomeViewModel? _homeViewModel;
        private MoviesViewModel? _moviesViewModel;
        private FavoritesViewModel? _favoritesViewModel;
        private CollectionsViewModel? _collectionsViewModel;

        private HomeViewModel GetHomeViewModel() => _homeViewModel ??= new HomeViewModel();
        private MoviesViewModel GetMoviesViewModel() => _moviesViewModel ??= new MoviesViewModel();
        private FavoritesViewModel GetFavoritesViewModel() => _favoritesViewModel ??= new FavoritesViewModel();
        private CollectionsViewModel GetCollectionsViewModel() => _collectionsViewModel ??= new CollectionsViewModel();

        public NotificationCenterService NotificationCenter => NotificationCenterService.Instance;
        public ICollectionView FilteredNotifications { get; }

        [ObservableProperty]
        private string _selectedNotificationTab = "all";

        partial void OnSelectedNotificationTabChanged(string value)
        {
            FilteredNotifications?.Refresh();
        }

        [ObservableProperty]
        private string _notificationSearchQuery = string.Empty;

        partial void OnNotificationSearchQueryChanged(string value)
        {
            FilteredNotifications?.Refresh();
        }

        public MainViewModel()
        {
            CurrentViewModel = GetHomeViewModel();

            FilteredNotifications = CollectionViewSource.GetDefaultView(NotificationCenter.Notifications);
            FilteredNotifications.Filter = NotificationFilter;

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
                else if (m.ViewModel != null && m.ViewModel.GetType() == typeof(CollectionsViewModel))
                {
                    CurrentViewModel = GetCollectionsViewModel();
                }
                else if (m.ViewModel != null)
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

        private bool NotificationFilter(object obj)
        {
            if (obj is not AppNotificationItem item) return false;

            // Tab filter
            bool matchesTab = SelectedNotificationTab switch
            {
                "update" => string.Equals(item.Type, "update", StringComparison.OrdinalIgnoreCase),
                "news" => string.Equals(item.Type, "info", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Type, "success", StringComparison.OrdinalIgnoreCase),
                "warning" => string.Equals(item.Type, "warning", StringComparison.OrdinalIgnoreCase),
                _ => true
            };

            if (!matchesTab) return false;

            // Search filter
            if (!string.IsNullOrWhiteSpace(NotificationSearchQuery))
            {
                string query = NotificationSearchQuery.Trim();
                bool matchesSearch = (item.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                                     (item.Message?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
                if (!matchesSearch) return false;
            }

            return true;
        }

        [RelayCommand]
        private void SelectNotificationTab(string tab)
        {
            SelectedNotificationTab = tab;
        }

        [RelayCommand]
        private void ClearNotificationSearch()
        {
            NotificationSearchQuery = string.Empty;
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
            _ = _moviesViewModel?.LoadMoviesAsync();
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

        [ObservableProperty]
        private bool _isNotificationPopupOpen = false;

        [RelayCommand]
        private void ToggleNotificationPopup()
        {
            IsNotificationPopupOpen = !IsNotificationPopupOpen;
        }

        [RelayCommand]
        private void CloseNotificationPopup()
        {
            IsNotificationPopupOpen = false;
        }

        [RelayCommand]
        private void MarkAllNotificationsRead()
        {
            NotificationCenter.MarkAllAsRead();
            FilteredNotifications?.Refresh();
        }

        [RelayCommand]
        private void MarkNotificationRead(AppNotificationItem item)
        {
            NotificationCenter.MarkAsRead(item);
            FilteredNotifications?.Refresh();
        }

        [RelayCommand]
        private void DismissNotification(AppNotificationItem item)
        {
            NotificationCenter.Remove(item);
            FilteredNotifications?.Refresh();
        }

        [RelayCommand]
        private void ClearAllNotifications()
        {
            NotificationCenter.ClearAll();
            FilteredNotifications?.Refresh();
        }

        [RelayCommand]
        private void OpenNotificationAction(AppNotificationItem item)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.ActionUrl))
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.ActionUrl,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
            }
            if (item != null)
            {
                NotificationCenter.MarkAsRead(item);
                FilteredNotifications?.Refresh();
            }
        }
    }
}
