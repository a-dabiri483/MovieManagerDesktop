using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class DayDetailWindow : Window
    {
        private readonly CalendarDayItem _day;

        public DayDetailWindow(CalendarDayItem day)
        {
            InitializeComponent();
            _day = day;

            var pc = new System.Globalization.PersianCalendar();
            int jYear = pc.GetYear(day.Date);
            int jMonth = pc.GetMonth(day.Date);
            int jDay = pc.GetDayOfMonth(day.Date);
            string[] persianMonths = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

            TitleText.Text = $"انتشارهای {jDay} {persianMonths[jMonth]} {jYear}";
            SubtitleText.Text = $"{day.Releases.Count} عنوان";
            
            ReleasesItemsControl.ItemsSource = day.Releases;

            if (day.Releases.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
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

                    string translated = await TranslateTextAsync(item.Overview);
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

        private async System.Threading.Tasks.Task<string> TranslateTextAsync(string text)
        {
            try
            {
                using var client = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
                client.Timeout = TimeSpan.FromSeconds(10);
                
                string encoded = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=fa&dt=t&q={encoded}";
                
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    // Google Translate returns [[["translated","original",...],...],...]
                    string result = "";
                    if (root.GetArrayLength() > 0)
                    {
                        var sentences = root[0];
                        for (int i = 0; i < sentences.GetArrayLength(); i++)
                        {
                            result += sentences[i][0].GetString();
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("Translation error", ex);
            }
            return null;
        }
    }
}
