using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public enum RenameStatus
    {
        Pending,
        Success,
        Error
    }

    public partial class FolderRenameItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _originalPath = string.Empty;

        [ObservableProperty]
        private string _originalName = string.Empty;

        [ObservableProperty]
        private string _newName = string.Empty;

        [ObservableProperty]
        private RenameStatus _status = RenameStatus.Pending;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasIcon;

        public string StatusLabel => Status switch
        {
            RenameStatus.Pending => "در انتظار",
            RenameStatus.Success => "موفق",
            RenameStatus.Error => "خطا",
            _ => "نامشخص"
        };

        partial void OnStatusChanged(RenameStatus value)
        {
            OnPropertyChanged(nameof(StatusLabel));
        }
    }
}
