using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class LogViewerWindow : Window
    {
        private const int MaxLogLines = 500;
        private int _currentLineCount = 0;

        public LogViewerWindow()
        {
            InitializeComponent();
            LoadExistingLogs();
            LoggerService.LogAdded += LoggerService_LogAdded;
            this.Closed += LogViewerWindow_Closed;
        }

        private void LoadExistingLogs()
        {
            try
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
                var logFilePath = Path.Combine(appData, "Logs", "app.log");
                
                if (File.Exists(logFilePath))
                {
                    var lines = File.ReadAllLines(logFilePath);
                    int linesToRead = Math.Min(100, lines.Length);
                    
                    for (int i = lines.Length - linesToRead; i < lines.Length; i++)
                    {
                        AppendColoredLog(lines[i]);
                    }
                    
                    ScrollToEnd();
                }
            }
            catch { }
        }

        private void LoggerService_LogAdded(object? sender, string logEntry)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // If Network Only filter is on, skip non-network logs
                if (NetworkOnlyCheckBox.IsChecked == true && !logEntry.Contains("[Network]"))
                {
                    return;
                }

                AppendColoredLog(logEntry);

                if (AutoScrollCheckBox.IsChecked == true)
                {
                    ScrollToEnd();
                }
            }));
        }

        private void AppendColoredLog(string logEntry)
        {
            // Trim old lines if too many
            if (_currentLineCount >= MaxLogLines)
            {
                var doc = LogRichTextBox.Document;
                if (doc.Blocks.FirstBlock != null)
                {
                    doc.Blocks.Remove(doc.Blocks.FirstBlock);
                    _currentLineCount--;
                }
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 1, 0, 1),
                LineHeight = 1
            };

            var run = new Run(logEntry);
            
            // Color coding based on log content
            if (logEntry.Contains("✔") && logEntry.Contains("Proxy OK"))
            {
                // Proxy success - purple/magenta
                run.Foreground = new SolidColorBrush(Color.FromRgb(199, 125, 255)); // #C77DFF
            }
            else if (logEntry.Contains("✔") && logEntry.Contains("Direct OK"))
            {
                // Direct success - green
                run.Foreground = new SolidColorBrush(Color.FromRgb(78, 203, 113)); // #4ECB71
            }
            else if (logEntry.Contains("✖✖") || logEntry.Contains("ALL proxies failed"))
            {
                // All proxies failed - bright red
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // #FF6B6B
                run.FontWeight = FontWeights.Bold;
            }
            else if (logEntry.Contains("BLOCKED") || logEntry.Contains("✖") && logEntry.Contains("FAILED"))
            {
                // Blocked or failed - yellow/warning
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 217, 61)); // #FFD93D
            }
            else if (logEntry.Contains("✖") && logEntry.Contains("ERROR"))
            {
                // Proxy error - orange-red
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 158, 64)); // #FF9E40
            }
            else if (logEntry.Contains("⚑") || logEntry.Contains("Route flipped"))
            {
                // Route flip - orange
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 158, 64)); // #FF9E40
                run.FontWeight = FontWeights.SemiBold;
            }
            else if (logEntry.Contains("⚠") || logEntry.Contains("Failure #"))
            {
                // Warning count - yellow
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 217, 61)); // #FFD93D
            }
            else if (logEntry.Contains("🔄") || logEntry.Contains("Trying") && logEntry.Contains("proxy"))
            {
                // Trying proxies - cyan
                run.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)); // #00D2FF
            }
            else if (logEntry.Contains("➜") && logEntry.Contains("Direct"))
            {
                // Direct request - light cyan
                run.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)); // #00D2FF
            }
            else if (logEntry.Contains("➜") && logEntry.Contains("Cached route"))
            {
                // Cached proxy route - purple
                run.Foreground = new SolidColorBrush(Color.FromRgb(199, 125, 255)); // #C77DFF
            }
            else if (logEntry.Contains("Proxy ["))
            {
                // Proxy attempt - light purple
                run.Foreground = new SolidColorBrush(Color.FromRgb(180, 140, 220)); // light purple
            }
            else if (logEntry.Contains("[Network]"))
            {
                // Other network logs - light gray
                run.Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 200)); // #AAB4C8
            }
            else if (logEntry.Contains("| ERROR"))
            {
                // General error
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // #FF6B6B
            }
            else if (logEntry.Contains("| WARN"))
            {
                // General warning
                run.Foreground = new SolidColorBrush(Color.FromRgb(255, 217, 61)); // #FFD93D
            }
            else
            {
                // Default - dim gray
                run.Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 160)); // #8C91A0
            }

            paragraph.Inlines.Add(run);
            LogRichTextBox.Document.Blocks.Add(paragraph);
            _currentLineCount++;
        }

        private void ScrollToEnd()
        {
            LogRichTextBox.ScrollToEnd();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogRichTextBox.Document.Blocks.Clear();
            _currentLineCount = 0;
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            // When filter toggles, reload logs with filter applied
            LogRichTextBox.Document.Blocks.Clear();
            _currentLineCount = 0;
            LoadExistingLogs();
        }

        private void LogViewerWindow_Closed(object? sender, EventArgs e)
        {
            LoggerService.LogAdded -= LoggerService_LogAdded;
        }
    }
}
