using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using System.Linq;
using System.Windows.Media;

namespace MovieManagerDesktop.ViewModels
{
    public partial class PersonItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private int _movieCount;

        [ObservableProperty]
        private string _initials;

        [ObservableProperty]
        private string _personType; // "Actor" or "Director"

        public PersonItemViewModel(string name, int count, string type)
        {
            Name = name;
            MovieCount = count;
            PersonType = type;
            Initials = GetInitials(name);
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
            return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpper();
        }

        [RelayCommand]
        private void OpenPersonMovies()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new PersonMoviesViewModel(Name, PersonType)));
        }
    }
}
