using MovieManagerDesktop.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MovieManagerDesktop.Controls
{
    public static class CachedImage
    {
        public static readonly DependencyProperty SourceUrlProperty =
            DependencyProperty.RegisterAttached(
                "SourceUrl",
                typeof(string),
                typeof(CachedImage),
                new PropertyMetadata(null, OnSourceUrlChanged));

        public static string GetSourceUrl(DependencyObject obj)
        {
            return (string)obj.GetValue(SourceUrlProperty);
        }

        public static void SetSourceUrl(DependencyObject obj, string value)
        {
            obj.SetValue(SourceUrlProperty, value);
        }

        private static async void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image)
            {
                var url = e.NewValue as string;
                if (string.IsNullOrWhiteSpace(url))
                {
                    image.Source = null;
                    return;
                }

                try
                {
                    string localPath = await ImageCacheService.GetCachedImageAsync(url);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        // Use a decode width suitable for full-screen backdrops and HD posters
                        bitmap.DecodePixelWidth = 1280; 
                        bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze(); // Cross-thread safety
                        
                        image.Source = bitmap;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading cached image: {ex.Message}");
                }
            }
        }
    }
}
