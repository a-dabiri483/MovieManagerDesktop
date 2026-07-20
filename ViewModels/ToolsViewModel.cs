using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ToolsViewModel : ObservableObject
    {
        [RelayCommand]
        private void OpenFolderIcon()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new FolderIconToolViewModel()));
        }

        [RelayCommand]
        private void OpenNameCleaner()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new NameCleanerToolViewModel()));
        }

        [RelayCommand]
        private void OpenSeriesOrganizer()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesOrganizerToolViewModel()));
        }

        [RelayCommand]
        private void OpenSeriesFileRenamer()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new SeriesFileRenamerViewModel()));
        }
    }
}
