using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using MovieManagerDesktop.Services;
using MovieManagerDesktop.Services.Network;

namespace MovieManagerDesktop.Views
{
    public partial class LogViewerWindow : Window
    {
        private const int MaxMemoryLines = 1000;
        private const int MaxRenderedLines = 400;
        private readonly List<string> _allRawLogs = new();
        private string _activeCategory = "ALL";
        private string _currentSearch = string.Empty;
        private bool _isInitialized = false;

        // Cached SolidColorBrushes
        private static readonly SolidColorBrush BrushTime = new(Color.FromRgb(100, 116, 139));       // Slate #64748B
        private static readonly SolidColorBrush BrushNetwork = new(Color.FromRgb(56, 189, 248));     // Sky Blue #38BDF8
        private static readonly SolidColorBrush BrushPlayer = new(Color.FromRgb(52, 211, 153));      // Emerald #34D399
        private static readonly SolidColorBrush BrushScanner = new(Color.FromRgb(192, 132, 252));    // Purple #C084FC
        private static readonly SolidColorBrush BrushCloud = new(Color.FromRgb(6, 182, 212));        // Cyan #06B6D4
        private static readonly SolidColorBrush BrushSubtitle = new(Color.FromRgb(251, 191, 36));    // Amber #FBBF24
        private static readonly SolidColorBrush BrushDatabase = new(Color.FromRgb(45, 212, 191));    // Teal #2DD4BF
        private static readonly SolidColorBrush BrushSystem = new(Color.FromRgb(148, 163, 184));     // Slate #94A3B8
        private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb(74, 222, 128));     // Green #4ADE80
        private static readonly SolidColorBrush BrushWarning = new(Color.FromRgb(251, 191, 36));     // Yellow #FBBF24
        private static readonly SolidColorBrush BrushError = new(Color.FromRgb(248, 113, 113));      // Red #F87171
        private static readonly SolidColorBrush BrushTextDefault = new(Color.FromRgb(226, 232, 240)); // Slate #E2E8F0

        static LogViewerWindow()
        {
            // Freeze brushes for best WPF performance across threads
            BrushTime.Freeze();
            BrushNetwork.Freeze();
            BrushPlayer.Freeze();
            BrushScanner.Freeze();
            BrushCloud.Freeze();
            BrushSubtitle.Freeze();
            BrushDatabase.Freeze();
            BrushSystem.Freeze();
            BrushSuccess.Freeze();
            BrushWarning.Freeze();
            BrushError.Freeze();
            BrushTextDefault.Freeze();
        }

        public LogViewerWindow()
        {
            InitializeComponent();
            _isInitialized = true;
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/logo.png", UriKind.RelativeOrAbsolute);
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch { }

            UpdateVpnStatus();
            LoadExistingLogs();
            LoggerService.LogAdded += LoggerService_LogAdded;
            this.Closed += LogViewerWindow_Closed;
        }

        private void UpdateVpnStatus()
        {
            try
            {
                bool isVpn = ProxyHttpClientHandler.IsVpnActive(out string vpnInfo);
                if (isVpn)
                {
                    VpnStatusBadge.Background = new SolidColorBrush(Color.FromRgb(30, 58, 95)); // Navy Blue
                    VpnStatusText.Text = $"فعال: {vpnInfo}";
                    VpnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Sky blue
                }
                else
                {
                    VpnStatusBadge.Background = new SolidColorBrush(Color.FromRgb(22, 46, 42)); // Dark green
                    VpnStatusText.Text = "مستقیم (اینترنت عادی / بدون VPN)";
                    VpnStatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Emerald
                }
            }
            catch { }
        }

        private void LoadExistingLogs()
        {
            try
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
                var logFilePath = Path.Combine(appData, "Logs", "app.log");

                if (!File.Exists(logFilePath))
                {
                    logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                }

                _allRawLogs.Clear();
                if (File.Exists(logFilePath))
                {
                    var lines = File.ReadAllLines(logFilePath);
                    int start = Math.Max(0, lines.Length - MaxMemoryLines);
                    for (int i = start; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            _allRawLogs.Add(lines[i]);
                        }
                    }
                }

                RebuildRenderedLogs();
            }
            catch { }
        }

        private void LoggerService_LogAdded(object? sender, string logEntry)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _allRawLogs.Add(logEntry);
                if (_allRawLogs.Count > MaxMemoryLines * 1.5)
                {
                    _allRawLogs.RemoveRange(0, 200);
                }

                UpdateCounters();

                if (MatchesFilter(logEntry) && LogRichTextBox?.Document != null)
                {
                    LogRichTextBox.Document.Blocks.Add(CreateFormattedParagraph(logEntry));

                    if (AutoScrollCheckBox?.IsChecked == true)
                    {
                        LogRichTextBox.ScrollToEnd();
                    }
                }
            }));
        }

        private bool MatchesFilter(string logEntry)
        {
            // 1. Level Filter
            bool isError = logEntry.Contains("| ERROR");
            bool isWarn = logEntry.Contains("| WARN");
            bool isInfo = !isError && !isWarn;

            if (isError && ShowErrorCheckBox?.IsChecked != true) return false;
            if (isWarn && ShowWarnCheckBox?.IsChecked != true) return false;
            if (isInfo && ShowInfoCheckBox?.IsChecked != true) return false;

            // 2. Category Filter
            if (_activeCategory != "ALL")
            {
                if (_activeCategory == "Errors")
                {
                    if (!isError && !isWarn) return false;
                }
                else if (_activeCategory == "Cloud")
                {
                    if (!logEntry.Contains("[Cloud]", StringComparison.OrdinalIgnoreCase) && 
                        !logEntry.Contains("[Backup]", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (!logEntry.Contains($"[{_activeCategory}]", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 3. Search text
            if (!string.IsNullOrWhiteSpace(_currentSearch))
            {
                if (!logEntry.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void RebuildRenderedLogs()
        {
            if (!_isInitialized || LogRichTextBox == null) return;
            
            UpdateCounters();

            var filtered = _allRawLogs.Where(MatchesFilter).ToList();

            // Build fresh FlowDocument in memory for instant high-speed rendering
            var newDoc = new FlowDocument
            {
                PageWidth = 6000,
                LineHeight = 1.15
            };

            int start = Math.Max(0, filtered.Count - MaxRenderedLines);
            for (int i = start; i < filtered.Count; i++)
            {
                newDoc.Blocks.Add(CreateFormattedParagraph(filtered[i]));
            }

            LogRichTextBox.Document = newDoc;

            if (AutoScrollCheckBox?.IsChecked == true)
            {
                LogRichTextBox.ScrollToEnd();
            }
        }

        private void UpdateCounters()
        {
            if (!_isInitialized || TotalCountText == null || NetworkCountText == null || WarnCountText == null || ErrorCountText == null) return;
            int total = _allRawLogs.Count;
            int network = _allRawLogs.Count(l => l.Contains("[Network]"));
            int warn = _allRawLogs.Count(l => l.Contains("| WARN"));
            int error = _allRawLogs.Count(l => l.Contains("| ERROR"));

            TotalCountText.Text = total.ToString("N0");
            NetworkCountText.Text = network.ToString("N0");
            WarnCountText.Text = warn.ToString("N0");
            ErrorCountText.Text = error.ToString("N0");
        }

        private Paragraph CreateFormattedParagraph(string logEntry)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 1, 0, 1),
                LineHeight = 1.15
            };

            string line = logEntry;
            
            // 1. Timestamp (e.g. "[2026-08-22 19:20:00]" or "2026-08-22 19:20:00")
            if (line.StartsWith("[") && line.Length > 21 && line[20] == ']')
            {
                string timePart = line.Substring(0, 21);
                line = line.Substring(21).TrimStart();
                paragraph.Inlines.Add(new Run(timePart + " ") { Foreground = BrushTime });
            }
            else if (line.Length >= 19 && char.IsDigit(line[0]) && char.IsDigit(line[1]) && line[4] == '-' && line[10] == ' ')
            {
                string timePart = "[" + line.Substring(0, 19) + "]";
                line = line.Substring(19).TrimStart();
                paragraph.Inlines.Add(new Run(timePart + " ") { Foreground = BrushTime });
            }

            // Remove leading "| " if present
            if (line.StartsWith("|"))
            {
                line = line.Substring(1).TrimStart();
            }

            // 2. Level indicator (e.g. "ERROR |" or "WARN  |" or "INFO  |")
            if (line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                int pipe = line.IndexOf('|');
                line = (pipe >= 0 ? line.Substring(pipe + 1) : line.Substring(5)).TrimStart();
                paragraph.Inlines.Add(new Run("● ERROR ") { Foreground = BrushError, FontWeight = FontWeights.Bold });
            }
            else if (line.StartsWith("WARN", StringComparison.OrdinalIgnoreCase))
            {
                int pipe = line.IndexOf('|');
                line = (pipe >= 0 ? line.Substring(pipe + 1) : line.Substring(4)).TrimStart();
                paragraph.Inlines.Add(new Run("▲ WARN ") { Foreground = BrushWarning, FontWeight = FontWeights.SemiBold });
            }
            else if (line.StartsWith("INFO", StringComparison.OrdinalIgnoreCase))
            {
                int pipe = line.IndexOf('|');
                line = (pipe >= 0 ? line.Substring(pipe + 1) : line.Substring(4)).TrimStart();
            }

            // 3. Category / Tag (e.g. "[Network]", "[Player]", "[Scanner]", "[Cloud]", "[Subtitles]", "[Database]", "[System]")
            if (line.StartsWith("["))
            {
                int endBracket = line.IndexOf(']');
                if (endBracket > 0)
                {
                    string tag = line.Substring(0, endBracket + 1);
                    line = line.Substring(endBracket + 1).TrimStart();

                    var tagBrush = tag switch
                    {
                        var t when t.Contains("Network", StringComparison.OrdinalIgnoreCase) => BrushNetwork,
                        var t when t.Contains("Player", StringComparison.OrdinalIgnoreCase) => BrushPlayer,
                        var t when t.Contains("Scanner", StringComparison.OrdinalIgnoreCase) => BrushScanner,
                        var t when t.Contains("Cloud", StringComparison.OrdinalIgnoreCase) || t.Contains("Backup", StringComparison.OrdinalIgnoreCase) => BrushCloud,
                        var t when t.Contains("Subtitle", StringComparison.OrdinalIgnoreCase) => BrushSubtitle,
                        var t when t.Contains("Database", StringComparison.OrdinalIgnoreCase) => BrushDatabase,
                        _ => BrushSystem
                    };

                    paragraph.Inlines.Add(new Run(tag + " ") { Foreground = tagBrush, FontWeight = FontWeights.Bold });
                }
            }

            // 4. Content highlighting
            var run = new Run(line);
            if (line.Contains("✔") || line.Contains("OK") || line.Contains("success", StringComparison.OrdinalIgnoreCase))
            {
                run.Foreground = BrushSuccess;
            }
            else if (line.Contains("✖") || line.Contains("BLOCKED") || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                run.Foreground = BrushError;
            }
            else if (line.Contains("⚑") || line.Contains("Flip") || line.Contains("Switching") || line.Contains("⚠"))
            {
                run.Foreground = BrushWarning;
            }
            else
            {
                run.Foreground = BrushTextDefault;
            }

            paragraph.Inlines.Add(run);
            return paragraph;
        }

        private void CategoryFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _activeCategory = tag;
                RebuildRenderedLogs();
            }
        }

        private void LevelFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            RebuildRenderedLogs();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _currentSearch = SearchTextBox.Text.Trim();
            if (ClearSearchBtn != null)
                ClearSearchBtn.Visibility = string.IsNullOrEmpty(_currentSearch) ? Visibility.Collapsed : Visibility.Visible;
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            RebuildRenderedLogs();
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
        }

        private void RefreshLogs_Click(object sender, RoutedEventArgs e)
        {
            UpdateVpnStatus();
            LoadExistingLogs();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _allRawLogs.Clear();
            LogRichTextBox.Document = new FlowDocument();
            UpdateCounters();
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filtered = _allRawLogs.Where(MatchesFilter).ToList();
                if (filtered.Count == 0)
                {
                    MessageBox.Show("هیچ لاگی برای کپی وجود ندارد.", "کپی لاگ‌ها", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string fullText = string.Join(Environment.NewLine, filtered);
                Clipboard.SetText(fullText);
                MessageBox.Show($"{filtered.Count:N0} خط لاگ با موفقیت در کلیپ‌بورد کپی شد.", "کپی لاگ‌ها", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در کپی لاگ‌ها: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filtered = _allRawLogs.Where(MatchesFilter).ToList();
                if (filtered.Count == 0)
                {
                    MessageBox.Show("هیچ لاگی برای ذخیره وجود ندارد.", "ذخیره لاگ", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Log File (*.log)|*.log|Text File (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = $"MovieManager_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.log",
                    Title = "ذخیره فایل لاگ‌های سیستم"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllLines(saveDialog.FileName, filtered);
                    MessageBox.Show($"فایل لاگ با موفقیت در مسیر زیر ذخیره شد:\n{saveDialog.FileName}", "ذخیره لاگ", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در ذخیره فایل: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogViewerWindow_Closed(object? sender, EventArgs e)
        {
            LoggerService.LogAdded -= LoggerService_LogAdded;
        }
    }
}
