using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MovieManagerDesktop.Views
{
    public partial class VideoPlayerWindow : Window
    {
        private DispatcherTimer _timer;
        private bool _isPlaying = true;

        public VideoPlayerWindow(string filePath)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                titleText.Text = Path.GetFileName(filePath);
                mediaPlayer.Source = new Uri(filePath);
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan && !timelineSlider.IsMouseCaptured)
            {
                timelineSlider.Value = mediaPlayer.Position.TotalSeconds / mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                timeText.Text = $"{mediaPlayer.Position:hh\\:mm\\:ss} / {mediaPlayer.NaturalDuration.TimeSpan:hh\\:mm\\:ss}";
            }
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                timeText.Text = $"00:00:00 / {mediaPlayer.NaturalDuration.TimeSpan:hh\\:mm\\:ss}";
            }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            playPauseIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Play;
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                mediaPlayer.Pause();
                playPauseIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Play;
            }
            else
            {
                mediaPlayer.Play();
                playPauseIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Pause;
            }
            _isPlaying = !_isPlaying;
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (timelineSlider.IsMouseCaptured && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPos = TimeSpan.FromSeconds(e.NewValue * mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds);
                mediaPlayer.Position = newPos;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = e.NewValue;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            mediaPlayer.Stop();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            mediaPlayer.Stop();
            base.OnClosed(e);
        }
    }
}
