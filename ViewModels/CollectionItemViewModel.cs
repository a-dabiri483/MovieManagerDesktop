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

        [ObservableProperty]
        private string _originalName;

        public CollectionItemViewModel(string name, int count, string posterUrl)
        {
            OriginalName = name;
            // Clean Name to prevent duplicate "مجموعه" or "Collection"
            string cleanName = name ?? "";
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"(?i)collection", "").Trim();
            cleanName = cleanName.Replace("مجموعه", "").Trim();
            if (!cleanName.StartsWith("مجموعه"))
            {
                cleanName = $"مجموعه {cleanName}";
            }

            Name = cleanName;
            MovieCount = count;
            PosterUrl = posterUrl;
        }

        [RelayCommand]
        private void OpenCollectionMovies()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new CollectionMoviesViewModel(OriginalName)));
        }
    }
}
