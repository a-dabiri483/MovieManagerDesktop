using MovieManagerDesktop.Services;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MovieManagerDesktop.Converters
{
    public class ImageUrlToCachedImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var url = value as string;
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // Create a dummy BitmapImage or an empty one first, then load it async
            var bitmap = new BitmapImage();
            
            LoadImageAsync(bitmap, url);
            
            return bitmap;
        }

        private async void LoadImageAsync(BitmapImage bitmap, string url)
        {
            try
            {
                string localPath = await ImageCacheService.GetCachedImageAsync(url);
                if (!string.IsNullOrEmpty(localPath))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 400;
                    bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Freeze for cross-thread access if needed
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image {url}: {ex.Message}");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
