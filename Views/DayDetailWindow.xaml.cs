using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MovieManagerDesktop.ViewModels;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class DayDetailWindow : Window
    {
        private readonly CalendarDayItem _day;

        public DayDetailWindow(CalendarDayItem day)
        {
            InitializeComponent();
            _day = day;

            string formattedDate = DateTimeFormatterService.FormatDate(day.Date.ToString("yyyy-MM-dd"));
            TitleText.Text = $"انتشارهای {formattedDate}";
            SubtitleText.Text = $"{day.Releases.Count} عنوان";
            
            ReleasesItemsControl.ItemsSource = day.Releases;

            if (day.Releases.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CalendarMediaItem item)
            {
                if (string.IsNullOrWhiteSpace(item.Overview))
                {
                    MovieManagerDesktop.Services.ToastService.Instance.ShowInfo("متنی برای ترجمه وجود ندارد.");
                    return;
                }

                try
                {
                    btn.IsEnabled = false;
                    MovieManagerDesktop.Services.ToastService.Instance.ShowInfo("در حال ترجمه...");

                    string translated = await MovieManagerDesktop.Services.TranslationService.TranslateTextAsync(item.Overview);
                    if (!string.IsNullOrWhiteSpace(translated) && translated != item.Overview)
                    {
                        item.Overview = translated;
                        // Force refresh
                        ReleasesItemsControl.ItemsSource = null;
                        ReleasesItemsControl.ItemsSource = _day.Releases;
                        MovieManagerDesktop.Services.ToastService.Instance.ShowSuccess("ترجمه با موفقیت انجام شد.");
                    }
                    else
                    {
                        MovieManagerDesktop.Services.ToastService.Instance.ShowInfo("ترجمه‌ای یافت نشد.");
                    }
                }
                catch (Exception ex)
                {
                    MovieManagerDesktop.Services.ToastService.Instance.ShowError($"خطا در ترجمه: {ex.Message}");
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }
    }
}
