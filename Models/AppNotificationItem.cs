using System;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace MovieManagerDesktop.Models
{
    public partial class AppNotificationItem : ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        [property: JsonPropertyName("title")]
        private string _title = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("message")]
        private string _message = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("type")]
        private string _type = "info"; // "info" | "warning" | "update" | "success"

        [ObservableProperty]
        [property: JsonPropertyName("action_title")]
        private string _actionTitle = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("action_url")]
        private string _actionUrl = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("received_at")]
        private DateTime _receivedAt = DateTime.Now;

        [ObservableProperty]
        [property: JsonPropertyName("is_read")]
        private bool _isRead = false;

        [JsonIgnore]
        public bool HasAction => !string.IsNullOrWhiteSpace(ActionUrl);

        [JsonIgnore]
        public PackIconKind TypeIcon => Type?.ToLowerInvariant() switch
        {
            "warning" => PackIconKind.AlertCircleOutline,
            "update" => PackIconKind.ArrowUpBoldCircleOutline,
            "success" => PackIconKind.CheckCircleOutline,
            _ => PackIconKind.InformationOutline
        };

        [JsonIgnore]
        public Brush TypeColorBrush => Type?.ToLowerInvariant() switch
        {
            "warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
            "update" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // Emerald
            "success" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),  // Green
            _ => new SolidColorBrush(Color.FromRgb(56, 189, 248))          // Sky Blue
        };

        [JsonIgnore]
        public Brush TypeBackgroundBrush => Type?.ToLowerInvariant() switch
        {
            "warning" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
            "update" => new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
            "success" => new SolidColorBrush(Color.FromArgb(40, 34, 197, 94)),
            _ => new SolidColorBrush(Color.FromArgb(40, 56, 189, 248))
        };

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - ReceivedAt;
                if (diff.TotalMinutes < 1) return "همین الان";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} دقیقه پیش";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ساعت پیش";
                if (diff.TotalDays < 2) return "دیروز";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} روز پیش";
                if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} هفته پیش";
                return $"{(int)(diff.TotalDays / 30)} ماه پیش";
            }
        }
    }
}
