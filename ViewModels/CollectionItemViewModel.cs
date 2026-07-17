using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using System.Windows.Media;

namespace MovieManagerDesktop.ViewModels
{
    public partial class CollectionItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private int _movieCount;

        [ObservableProperty]
        private string _posterUrl;

        public CollectionItemViewModel(string name, int count, string posterUrl)
        {
            Name = name;
            MovieCount = count;
            PosterUrl = posterUrl;
        }

        [RelayCommand]
        private void OpenCollectionMovies()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CollectionMoviesViewModel(Name)));
        }
    }
}
