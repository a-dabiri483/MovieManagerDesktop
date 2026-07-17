using CommunityToolkit.Mvvm.ComponentModel;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ScannedItemViewModel : ObservableObject
    {
        public VideoFile File { get; }
        
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _status = "آماده بررسی";
        
        public ScannedItemViewModel(VideoFile file)
        {
            File = file;
        }
    }
}
