using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class RenamerItemModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _originalFilePath = string.Empty;

        [ObservableProperty]
        private string _originalFileName = string.Empty;

        [ObservableProperty]
        private string _newFileName = string.Empty;

        [ObservableProperty]
        private string _status = "آماده"; // Ready, Success, Error
        
        [ObservableProperty]
        private bool _isRenamed = false;
        
        [ObservableProperty]
        private bool _isSubtitle = false;
        
        public bool IsVideo => !IsSubtitle;
    }
}
