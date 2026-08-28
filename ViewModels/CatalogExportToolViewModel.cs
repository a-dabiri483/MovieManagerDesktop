using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using MovieManagerDesktop.Data;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public class CatalogExportItem
    {
        public string Title { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string MediaType { get; set; } = "Movie";
        public string MediaTypeDisplay => MediaType == "Series" ? "سریال" : "فیلم سینمایی";
        public double? Rating { get; set; }
        public string? Quality { get; set; }
        public string? Genres { get; set; }
        public string? Director { get; set; }
        public string? Actors { get; set; }
        public string? Overview { get; set; }
        public string? PosterUrl { get; set; }
        public string? FilePath { get; set; }
        public int EpisodeCount { get; set; } = 1;
        public int SeasonCount { get; set; } = 1;
        public string EpisodeSeasonSummary { get; set; } = string.Empty;
    }

    public partial class CatalogExportToolViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _filterTypeIndex = 0; // 0: All, 1: Movies, 2: Series, 3: Favorites, 4: Watched

        [ObservableProperty]
        private int _formatIndex = 0; // 0: Word (.doc), 1: Excel (.csv), 2: Interactive HTML

        [ObservableProperty]
        private bool _includePosters = true;

        [ObservableProperty]
        private bool _includeOverview = true;

        [ObservableProperty]
        private bool _includeActors = true;

        [ObservableProperty]
        private bool _includeQuality = true;

        [ObservableProperty]
        private bool _isExporting = false;

        [ObservableProperty]
        private int _progressPercent = 0;

        [ObservableProperty]
        private string _statusMessage = "تنظیمات مورد نظر را انتخاب و روی «تولید کاتالوگ» کلیک کنید.";

        [ObservableProperty]
        private int _matchingItemsCount = 0;

        public CatalogExportToolViewModel()
        {
            UpdateMatchingCount();
        }

        partial void OnFilterTypeIndexChanged(int value)
        {
            UpdateMatchingCount();
        }

        private void UpdateMatchingCount()
        {
            try
            {
                using var db = new AppDbContext();
                var rawFiles = GetFilteredQuery(db).ToList();
                var grouped = GetGroupedExportItems(rawFiles);
                MatchingItemsCount = grouped.Count;
            }
            catch { }
        }

        private IQueryable<VideoFile> GetFilteredQuery(AppDbContext db)
        {
            return FilterTypeIndex switch
            {
                1 => db.VideoFiles.Where(v => v.MediaType == "Movie"),
                2 => db.VideoFiles.Where(v => v.MediaType == "Series"),
                3 => db.VideoFiles.Where(v => v.IsFavorite),
                4 => db.VideoFiles.Where(v => v.IsWatched),
                _ => db.VideoFiles.AsQueryable()
            };
        }

        private List<CatalogExportItem> GetGroupedExportItems(List<VideoFile> rawFiles)
        {
            var grouped = rawFiles
                .GroupBy(v => 
                {
                    bool isSeries = v.MediaType?.Equals("Series", StringComparison.OrdinalIgnoreCase) == true 
                                    || v.Season != null 
                                    || v.Episode != null;
                    
                    string cleanTitle = (v.FormattedTitle ?? Path.GetFileNameWithoutExtension(v.FileName) ?? "ناشناس").Trim().ToLowerInvariant();
                    return isSeries ? $"Series:{cleanTitle}" : $"Movie:{cleanTitle}:{v.Year}";
                })
                .Select(g => 
                {
                    var first = g.First();
                    bool isSeries = first.MediaType?.Equals("Series", StringComparison.OrdinalIgnoreCase) == true 
                                    || g.Any(x => x.Season != null || x.Episode != null);

                    var item = new CatalogExportItem
                    {
                        Title = first.FormattedTitle ?? Path.GetFileNameWithoutExtension(first.FileName) ?? "بدون عنوان",
                        Year = first.Year,
                        MediaType = isSeries ? "Series" : "Movie",
                        Rating = g.Select(x => x.Rating).FirstOrDefault(r => r.HasValue && r > 0) ?? first.Rating,
                        Quality = g.Select(x => x.Resolution ?? x.Quality).FirstOrDefault(q => !string.IsNullOrEmpty(q)) ?? first.Resolution ?? first.Quality,
                        Genres = g.Select(x => x.Genres).FirstOrDefault(gn => !string.IsNullOrEmpty(gn)) ?? first.Genres,
                        Director = g.Select(x => x.Director).FirstOrDefault(d => !string.IsNullOrEmpty(d)) ?? first.Director,
                        Actors = g.Select(x => x.Actors).FirstOrDefault(a => !string.IsNullOrEmpty(a)) ?? first.Actors,
                        Overview = g.Select(x => x.Overview).FirstOrDefault(o => !string.IsNullOrEmpty(o)) ?? first.Overview,
                        PosterUrl = g.Select(x => x.PosterUrl).FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p)) 
                                    ?? g.Select(x => x.PosterUrl).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                        FilePath = isSeries ? (Path.GetDirectoryName(first.FilePath) ?? first.FilePath) : first.FilePath
                    };

                    if (isSeries)
                    {
                        int totalEpisodes = g.Count();
                        var distinctSeasons = g.Select(x => x.Season).Where(s => s != null && s > 0).Distinct().OrderBy(s => s).ToList();
                        int seasonCount = distinctSeasons.Count > 0 ? distinctSeasons.Count : (first.NumberOfSeasons ?? 1);

                        item.EpisodeCount = totalEpisodes;
                        item.SeasonCount = seasonCount;

                        if (seasonCount > 1)
                        {
                            item.EpisodeSeasonSummary = $"{seasonCount} فصل ({totalEpisodes} قسمت)";
                        }
                        else
                        {
                            item.EpisodeSeasonSummary = $"{totalEpisodes} قسمت";
                        }
                    }

                    return item;
                })
                .OrderBy(x => x.Title)
                .ToList();

            return grouped;
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        [RelayCommand]
        private async Task ExportCatalogAsync()
        {
            var dialog = new SaveFileDialog
            {
                Title = "ذخیره کاتالوگ فیلم‌ها و سریال‌ها"
            };

            switch (FormatIndex)
            {
                case 0:
                    dialog.Filter = "سند مایکروسافت ورد (*.doc)|*.doc|فایل وب (*.html)|*.html";
                    dialog.FileName = $"MovieManager_Catalog_{DateTime.Now:yyyyMMdd}.doc";
                    break;
                case 1:
                    dialog.Filter = "فایل اکسل (*.csv)|*.csv|فایل متنی (*.txt)|*.txt";
                    dialog.FileName = $"MovieManager_Export_{DateTime.Now:yyyyMMdd}.csv";
                    break;
                case 2:
                    dialog.Filter = "کاتالوگ تعاملی وب (*.html)|*.html";
                    dialog.FileName = $"MovieManager_WebCatalog_{DateTime.Now:yyyyMMdd}.html";
                    break;
            }

            if (dialog.ShowDialog() != true) return;

            string targetPath = dialog.FileName;
            IsExporting = true;
            ProgressPercent = 0;
            StatusMessage = "در حال تجمیع اطلاعات فیلم‌ها و سریال‌ها...";

            await Task.Run(async () =>
            {
                try
                {
                    List<VideoFile> rawFiles;
                    using (var db = new AppDbContext())
                    {
                        rawFiles = GetFilteredQuery(db).ToList();
                    }

                    var items = GetGroupedExportItems(rawFiles);

                    if (items.Count == 0)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            ToastService.Instance.ShowWarning("هیچ عنوانی با فیلتر انتخابی برای خروجی یافت نشد.");
                            IsExporting = false;
                        });
                        return;
                    }

                    App.Current.Dispatcher.Invoke(() => StatusMessage = $"در حال آماده‌سازی کاتالوگ {items.Count} عنوان اثر...");

                    if (FormatIndex == 0)
                    {
                        // Word format (Formatted HTML doc compatible with Microsoft Word)
                        GenerateWordDoc(items, targetPath);
                    }
                    else if (FormatIndex == 1)
                    {
                        // Excel / CSV format
                        GenerateCsv(items, targetPath);
                    }
                    else if (FormatIndex == 2)
                    {
                        // Interactive standalone HTML catalog
                        GenerateInteractiveHtml(items, targetPath);
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressPercent = 100;
                        StatusMessage = $"کاتالوگ با موفقیت ایجاد و ذخیره شد ({items.Count} اثر): {Path.GetFileName(targetPath)}";
                        ToastService.Instance.ShowSuccess($"کاتالوگ {items.Count} اثر با موفقیت ذخیره شد.");
                    });
                }
                catch (Exception ex)
                {
                    LoggerService.Error("Failed to export catalog", ex);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"خطا در ایجاد کاتالوگ: {ex.Message}";
                        ToastService.Instance.ShowError("خطایی در حین ایجاد کاتالوگ رخ داد.");
                    });
                }
                finally
                {
                    App.Current.Dispatcher.Invoke(() => IsExporting = false);
                }
            });
        }

        private void GenerateWordDoc(List<CatalogExportItem> items, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>");
            writer.WriteLine("<head><meta charset='utf-8'>");
            writer.WriteLine("<title>MovieManager Catalog</title>");
            writer.WriteLine("<style>");
            writer.WriteLine("body { font-family: 'Tahoma', 'Segoe UI', Arial, sans-serif; direction: rtl; text-align: right; background: #fff; margin: 20px; color: #222; }");
            writer.WriteLine("h1 { color: #eb3b5a; text-align: center; border-bottom: 2px solid #eb3b5a; padding-bottom: 10px; }");
            writer.WriteLine(".meta-info { text-align: center; color: #777; margin-bottom: 30px; font-size: 13px; }");
            writer.WriteLine(".media-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; page-break-inside: avoid; border: 1px solid #ddd; }");
            writer.WriteLine(".media-table td { padding: 10px; vertical-align: top; border: 1px solid #eee; }");
            writer.WriteLine(".poster-cell { width: 120px; text-align: center; background: #fdfdfd; }");
            writer.WriteLine(".poster-img { width: 110px; border-radius: 6px; }");
            writer.WriteLine(".title { font-size: 16px; font-weight: bold; color: #1e272e; margin-bottom: 4px; }");
            writer.WriteLine(".badge { display: inline-block; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: bold; margin-left: 6px; }");
            writer.WriteLine(".badge-rating { background: #ffd32a; color: #000; }");
            writer.WriteLine(".badge-quality { background: #20bf6b; color: #fff; }");
            writer.WriteLine(".badge-type { background: #8854d0; color: #fff; }");
            writer.WriteLine(".details-row { margin: 4px 0; font-size: 12px; color: #4b6584; }");
            writer.WriteLine(".overview { margin-top: 6px; font-size: 11.5px; line-height: 1.5; color: #57606f; }");
            writer.WriteLine("</style></head><body>");

            writer.WriteLine("<h1>کاتالوگ جامع فیلم‌ها و سریال‌ها</h1>");
            writer.WriteLine($"<div class='meta-info'>تعداد کل عناوین: {items.Count} اثر | تاریخ ایجاد: {DateTime.Now:yyyy/MM/dd HH:mm} | تولید شده توسط MovieManager</div>");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                writer.WriteLine("<table class='media-table'><tr>");

                if (IncludePosters && !string.IsNullOrWhiteSpace(item.PosterUrl))
                {
                    string posterSrc = item.PosterUrl;
                    if (File.Exists(posterSrc))
                    {
                        posterSrc = new Uri(posterSrc).AbsoluteUri;
                    }
                    writer.WriteLine($"<td class='poster-cell'><img class='poster-img' src='{posterSrc}' alt='{System.Net.WebUtility.HtmlEncode(item.Title)}'/></td>");
                }

                writer.WriteLine("<td>");
                string typeBadge = item.MediaType == "Series" ? $"سریال ({item.EpisodeSeasonSummary})" : "فیلم سینمایی";
                writer.WriteLine($"<div class='title'>{i + 1}. {System.Net.WebUtility.HtmlEncode(item.Title)} {(string.IsNullOrEmpty(item.Year) ? "" : $"({item.Year})")}");
                if (item.Rating.HasValue && item.Rating > 0) writer.WriteLine($"<span class='badge badge-rating'>★ {item.Rating:0.0}</span>");
                if (IncludeQuality && !string.IsNullOrEmpty(item.Quality)) writer.WriteLine($"<span class='badge badge-quality'>{System.Net.WebUtility.HtmlEncode(item.Quality)}</span>");
                writer.WriteLine($"<span class='badge badge-type'>{typeBadge}</span>");
                writer.WriteLine("</div>");

                if (item.MediaType == "Series" && !string.IsNullOrEmpty(item.EpisodeSeasonSummary))
                {
                    writer.WriteLine($"<div class='details-row'><b>وضعیت قسمت‌ها:</b> {System.Net.WebUtility.HtmlEncode(item.EpisodeSeasonSummary)}</div>");
                }

                if (!string.IsNullOrWhiteSpace(item.Genres)) writer.WriteLine($"<div class='details-row'><b>ژانر:</b> {System.Net.WebUtility.HtmlEncode(item.Genres)}</div>");
                if (IncludeActors && !string.IsNullOrWhiteSpace(item.Actors)) writer.WriteLine($"<div class='details-row'><b>بازیگران:</b> {System.Net.WebUtility.HtmlEncode(item.Actors)}</div>");
                if (!string.IsNullOrWhiteSpace(item.Director)) writer.WriteLine($"<div class='details-row'><b>کارگردان:</b> {System.Net.WebUtility.HtmlEncode(item.Director)}</div>");
                if (IncludeOverview && !string.IsNullOrWhiteSpace(item.Overview)) writer.WriteLine($"<div class='overview'>{System.Net.WebUtility.HtmlEncode(item.Overview)}</div>");

                writer.WriteLine("</td></tr></table>");

                if (i % 100 == 0)
                {
                    App.Current.Dispatcher.Invoke(() => ProgressPercent = (int)((i / (double)items.Count) * 100));
                }
            }

            writer.WriteLine("</body></html>");
        }

        private void GenerateCsv(List<CatalogExportItem> items, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Title,Year,MediaType,EpisodesInfo,Rating,Quality,Genres,Director,Actors,FilePath");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string title = (item.Title ?? "").Replace("\"", "\"\"");
                string year = item.Year ?? "";
                string mediaType = item.MediaTypeDisplay;
                string episodesInfo = (item.MediaType == "Series" ? item.EpisodeSeasonSummary : "تک قسمتی").Replace("\"", "\"\"");
                string rating = item.Rating.HasValue ? item.Rating.Value.ToString("0.0") : "";
                string quality = (item.Quality ?? "").Replace("\"", "\"\"");
                string genres = (item.Genres ?? "").Replace("\"", "\"\"");
                string director = (item.Director ?? "").Replace("\"", "\"\"");
                string actors = (item.Actors ?? "").Replace("\"", "\"\"");
                string path = (item.FilePath ?? "").Replace("\"", "\"\"");

                writer.WriteLine($"\"{title}\",\"{year}\",\"{mediaType}\",\"{episodesInfo}\",\"{rating}\",\"{quality}\",\"{genres}\",\"{director}\",\"{actors}\",\"{path}\"");

                if (i % 200 == 0)
                {
                    App.Current.Dispatcher.Invoke(() => ProgressPercent = (int)((i / (double)items.Count) * 100));
                }
            }
        }

        private void GenerateInteractiveHtml(List<CatalogExportItem> items, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html lang='fa' dir='rtl'>");
            writer.WriteLine("<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
            writer.WriteLine("<title>MovieManager Interactive Catalog</title>");
            writer.WriteLine("<style>");
            writer.WriteLine("* { box-sizing: border-box; margin: 0; padding: 0; }");
            writer.WriteLine("body { font-family: system-ui, -apple-system, sans-serif; background: #0B0E14; color: #fff; padding: 24px; }");
            writer.WriteLine(".header { text-align: center; margin-bottom: 30px; }");
            writer.WriteLine(".header h1 { font-size: 28px; color: #EB3B5A; margin-bottom: 8px; }");
            writer.WriteLine(".header p { color: #888; font-size: 14px; }");
            writer.WriteLine(".search-box { width: 100%; max-width: 500px; margin: 0 auto 30px; display: block; padding: 14px 20px; border-radius: 25px; border: 1px solid #2A303C; background: #14172B; color: #fff; font-size: 15px; outline: none; }");
            writer.WriteLine(".grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 20px; max-width: 1400px; margin: 0 auto; }");
            writer.WriteLine(".card { background: #14172B; border: 1px solid #2A303C; border-radius: 14px; padding: 10px; transition: transform 0.2s, border-color 0.2s; overflow: hidden; position: relative; }");
            writer.WriteLine(".card:hover { transform: translateY(-4px); border-color: #EB3B5A; }");
            writer.WriteLine(".poster-wrap { position: relative; width: 100%; height: 280px; border-radius: 10px; overflow: hidden; background: #222; }");
            writer.WriteLine(".poster { width: 100%; height: 100%; object-fit: cover; }");
            writer.WriteLine(".badge-series { position: absolute; bottom: 8px; right: 8px; background: rgba(136,84,208,0.9); color: #fff; font-size: 11px; padding: 3px 7px; border-radius: 6px; font-weight: bold; }");
            writer.WriteLine(".card-title { font-size: 15px; font-weight: bold; margin: 10px 0 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");
            writer.WriteLine(".card-meta { display: flex; justify-content: space-between; font-size: 12px; color: #888; margin-bottom: 6px; }");
            writer.WriteLine(".rating { color: #FFD32A; font-weight: bold; }");
            writer.WriteLine(".genre { font-size: 11px; color: #666; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");
            writer.WriteLine("</style></head><body>");

            writer.WriteLine("<div class='header'>");
            writer.WriteLine("<h1>کاتالوگ فیلم‌ها و سریال‌های من</h1>");
            writer.WriteLine($"<p>مجموعاً {items.Count} اثر (فیلم و سریال) | ساخته‌شده با نرم‌افزار مدیریت فیلم و سریال MovieManager</p>");
            writer.WriteLine("</div>");

            writer.WriteLine("<input type='text' id='search' class='search-box' placeholder='جستجو در عنوان یا سال...' onkeyup='filterCards()'>");

            writer.WriteLine("<div class='grid' id='cardsGrid'>");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string posterSrc = "https://placehold.co/200x300/101222/FFF?text=No+Poster";
                if (IncludePosters && !string.IsNullOrWhiteSpace(item.PosterUrl))
                {
                    if (File.Exists(item.PosterUrl))
                    {
                        posterSrc = new Uri(item.PosterUrl).AbsoluteUri;
                    }
                    else if (item.PosterUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        posterSrc = item.PosterUrl;
                    }
                }

                writer.WriteLine($"<div class='card' data-title='{System.Net.WebUtility.HtmlEncode(item.Title)} {item.Year}'>");
                writer.WriteLine("<div class='poster-wrap'>");
                writer.WriteLine($"<img class='poster' src='{posterSrc}' alt='{System.Net.WebUtility.HtmlEncode(item.Title)}' loading='lazy'/>");
                if (item.MediaType == "Series")
                {
                    writer.WriteLine($"<span class='badge-series'>{System.Net.WebUtility.HtmlEncode(item.EpisodeSeasonSummary)}</span>");
                }
                writer.WriteLine("</div>");

                writer.WriteLine($"<div class='card-title'>{System.Net.WebUtility.HtmlEncode(item.Title)}</div>");
                writer.WriteLine("<div class='card-meta'>");
                writer.WriteLine($"<span>{item.Year} | {item.MediaTypeDisplay}</span>");
                writer.WriteLine($"<span class='rating'>★ {(item.Rating.HasValue ? item.Rating.Value.ToString("0.0") : "-")}</span>");
                writer.WriteLine("</div>");
                writer.WriteLine($"<div class='genre'>{System.Net.WebUtility.HtmlEncode(item.Genres ?? "")}</div>");
                writer.WriteLine("</div>");

                if (i % 100 == 0)
                {
                    App.Current.Dispatcher.Invoke(() => ProgressPercent = (int)((i / (double)items.Count) * 100));
                }
            }

            writer.WriteLine("</div>");

            writer.WriteLine("<script>");
            writer.WriteLine("function filterCards() {");
            writer.WriteLine("  let filter = document.getElementById('search').value.toLowerCase();");
            writer.WriteLine("  let cards = document.getElementsByClassName('card');");
            writer.WriteLine("  for (let card of cards) {");
            writer.WriteLine("    let title = card.getAttribute('data-title').toLowerCase();");
            writer.WriteLine("    card.style.display = title.includes(filter) ? '' : 'none';");
            writer.WriteLine("  }");
            writer.WriteLine("}");
            writer.WriteLine("</script>");

            writer.WriteLine("</body></html>");
        }
    }
}
