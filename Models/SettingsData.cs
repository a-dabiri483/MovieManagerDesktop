using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FolderIconManager.WPF.Models
{
    public partial class SettingsData : ObservableObject
    {
        [ObservableProperty]
        private string _iconDownloadPath = string.Empty;

        [ObservableProperty]
        private bool _autoApplyIcons;

        [ObservableProperty]
        private bool _backupOriginal;

        [ObservableProperty]
        private bool _showNotifications;

        [ObservableProperty]
        private List<string> _tmdbApiKeys = new();
    }
}
