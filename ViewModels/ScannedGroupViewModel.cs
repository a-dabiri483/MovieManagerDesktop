using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Models;
using System.Collections.Generic;
using System.Linq;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ScannedGroupViewModel : ObservableObject
    {
        public List<VideoFile> Files { get; }
        
        [ObservableProperty]
        private bool _isChecked = true;

        [ObservableProperty]
        private string _status = "آماده بررسی";
        
        [ObservableProperty]
        private string _titleOverride;
        
        [ObservableProperty]
        private string _yearOverride;
        
        [ObservableProperty]
        private string _idOverride = string.Empty;
        
        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isError;

        [ObservableProperty]
        private bool _isRegistered;
        
        public VideoFile Representative => Files.First();
        
        public string MediaType => Representative.MediaType ?? "Unknown";
        public string DisplayTitle => Representative.FormattedTitle;
        public string FileCountText => Files.Count > 1 ? $"{Files.Count} فایل (قسمت)" : "۱ فایل";
        
        public List<string> ExistingSeries { get; }
        
        public ScannedGroupViewModel(List<VideoFile> files, List<string> existingSeries = null)
        {
            Files = files;
            TitleOverride = Representative.FormattedTitle;
            YearOverride = Representative.Year ?? "";
            ExistingSeries = existingSeries ?? new List<string>();
        }
        
        [RelayCommand]
        private void ToggleEdit()
        {
            IsEditing = !IsEditing;
        }
    }
}
