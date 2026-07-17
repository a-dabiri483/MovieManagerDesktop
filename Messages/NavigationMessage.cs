using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Messages
{
    public class NavigationMessage
    {
        public ObservableObject ViewModel { get; }
        public NavigationMessage(ObservableObject viewModel)
        {
            ViewModel = viewModel;
        }
    }
}
