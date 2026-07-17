using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class ToastMessage : ObservableObject
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Message { get; set; }
        public ToastType Type { get; set; }

        [ObservableProperty]
        private bool _isClosing;
        
        // Helper properties for binding UI Colors/Icons
        public string BackgroundColor => Type switch
        {
            ToastType.Success => "#1A00E676", // Light Green
            ToastType.Error => "#1AFF1744",   // Light Red
            ToastType.Warning => "#1AFF9100", // Light Orange
            ToastType.Info => "#1A2979FF",    // Light Blue
            _ => "#1AFFFFFF"
        };
        
        public string BorderColor => Type switch
        {
            ToastType.Success => "#00E676",
            ToastType.Error => "#FF1744",
            ToastType.Warning => "#FF9100",
            ToastType.Info => "#2979FF",
            _ => "#FFFFFF"
        };

        public string IconKind => Type switch
        {
            ToastType.Success => "CheckCircleOutline",
            ToastType.Error => "CloseCircleOutline",
            ToastType.Warning => "AlertCircleOutline",
            ToastType.Info => "InformationOutline",
            _ => "BellOutline"
        };
    }
}
