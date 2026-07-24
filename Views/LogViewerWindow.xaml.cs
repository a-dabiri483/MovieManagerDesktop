using System;
using System.IO;
using System.Windows;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Views
{
    public partial class LogViewerWindow : Window
    {
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
                    // Read the last 100 lines so it doesn't freeze with huge logs
                    var lines = File.ReadAllLines(logFilePath);
                    int linesToRead = Math.Min(100, lines.Length);
                    var recentLines = new string[linesToRead];
                    Array.Copy(lines, lines.Length - linesToRead, recentLines, 0, linesToRead);
                    
                    LogTextBox.Text = string.Join(Environment.NewLine, recentLines) + Environment.NewLine;
                    LogTextBox.ScrollToEnd();
                }
            }
            catch { }
        }

        private void LoggerService_LogAdded(object? sender, string logEntry)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogTextBox.AppendText(logEntry + Environment.NewLine);
                if (AutoScrollCheckBox.IsChecked == true)
                {
                    LogTextBox.ScrollToEnd();
                }
            }));
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void LogViewerWindow_Closed(object? sender, EventArgs e)
        {
            LoggerService.LogAdded -= LoggerService_LogAdded;
        }
    }
}
