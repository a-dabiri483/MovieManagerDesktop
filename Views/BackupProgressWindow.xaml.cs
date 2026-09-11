using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace MovieManagerDesktop.Views
{
    public partial class BackupProgressWindow : Window
    {
        private bool _isCompleted = false;

        public BackupProgressWindow(string title, string subtitle, bool isBackup = true)
        {
            InitializeComponent();

            TxtOperationTitle.Text = title;
            TxtOperationSubtitle.Text = subtitle;

            if (isBackup)
            {
                IconHeader.Kind = PackIconKind.CloudUploadOutline;
                MainStatusIcon.Kind = PackIconKind.CloudUploadOutline;
            }
            else
            {
                IconHeader.Kind = PackIconKind.BackupRestore;
                MainStatusIcon.Kind = PackIconKind.ProgressDownload;
            }
        }

        public void UpdateProgress(double percentage, string statusMessage)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_isCompleted) return;

                percentage = Math.Clamp(percentage, 0.0, 100.0);
                ProgressBarMain.Value = percentage;
                TxtPercentage.Text = $"{Math.Round(percentage)}%";

                if (!string.IsNullOrWhiteSpace(statusMessage))
                {
                    TxtStatusDetail.Text = statusMessage;
                }
            });
        }

        public void SetCompleted(string message = "عملیات با موفقیت انجام شد.")
        {
            _isCompleted = true;
            Dispatcher.InvokeAsync(() =>
            {
                ProgressBarMain.Value = 100;
                TxtPercentage.Text = "۱۰۰٪";
                TxtStatusDetail.Text = message;
                TxtStatusDetail.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));

                MainStatusIcon.Kind = PackIconKind.CheckCircleOutline;
                MainStatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                BadgeIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13372B"));
                BadgeIcon.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));

                BtnCloseHeader.Visibility = Visibility.Visible;
                BtnDone.Visibility = Visibility.Visible;
            });
        }

        public void SetFailed(string errorMessage)
        {
            _isCompleted = true;
            Dispatcher.InvokeAsync(() =>
            {
                TxtStatusDetail.Text = errorMessage;
                TxtStatusDetail.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

                MainStatusIcon.Kind = PackIconKind.AlertCircleOutline;
                MainStatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                BadgeIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E1B1B"));
                BadgeIcon.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

                BtnCloseHeader.Visibility = Visibility.Visible;
                BtnDone.Visibility = Visibility.Visible;
                BtnDone.Content = "بستن";
                BtnDone.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            });
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch { }
        }
    }
}
