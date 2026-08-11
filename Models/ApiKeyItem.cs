using CommunityToolkit.Mvvm.ComponentModel;

namespace MovieManagerDesktop.Models
{
    public partial class ApiKeyItem : ObservableObject
    {
        [ObservableProperty]
        private string _key;

        public string MaskedKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Key)) return "***";
                var clean = Key.Trim();
                if (clean.Length <= 6) return "***";
                return $"{clean.Substring(0, 3)}***{clean.Substring(clean.Length - 3)}";
            }
        }

        public ApiKeyItem(string key)
        {
            _key = key;
        }

        partial void OnKeyChanged(string value)
        {
            OnPropertyChanged(nameof(MaskedKey));
        }
    }
}
