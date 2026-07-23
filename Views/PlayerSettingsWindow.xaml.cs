using System.Windows;
using System.Windows.Input;
using MovieManagerDesktop.Services;
using System;

namespace MovieManagerDesktop.Views
{
    public partial class PlayerSettingsWindow : Window
    {
        private double _currentSpeed = 1.0;

        public PlayerSettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void SpeedUp_Click(object sender, RoutedEventArgs e)
        {
            _currentSpeed = Math.Min(3.0, Math.Round(_currentSpeed + 0.1, 1));
            SpeedText.Text = $"{_currentSpeed:0.0}x";
            await MpvIntegrationService.SendCommandAsync("osd-msg add speed 0.1");
        }

        private async void SpeedDown_Click(object sender, RoutedEventArgs e)
        {
            _currentSpeed = Math.Max(0.2, Math.Round(_currentSpeed - 0.1, 1));
            SpeedText.Text = $"{_currentSpeed:0.0}x";
            await MpvIntegrationService.SendCommandAsync("osd-msg add speed -0.1");
        }

        private async void CycleSubtitle_Click(object sender, RoutedEventArgs e)
        {
            await MpvIntegrationService.SendCommandAsync("osd-msg cycle sub");
        }

        private async void CycleAudio_Click(object sender, RoutedEventArgs e)
        {
            await MpvIntegrationService.SendCommandAsync("osd-msg cycle audio");
        }

        private double _subDelay = 0.0;
        private async void SubDelayUp_Click(object sender, RoutedEventArgs e)
        {
            _subDelay = Math.Round(_subDelay + 0.1, 1);
            SubDelayText.Text = $"{_subDelay:0.0}s";
            await MpvIntegrationService.SendCommandAsync($"osd-msg set sub-delay {_subDelay}");
        }

        private async void SubDelayDown_Click(object sender, RoutedEventArgs e)
        {
            _subDelay = Math.Round(_subDelay - 0.1, 1);
            SubDelayText.Text = $"{_subDelay:0.0}s";
            await MpvIntegrationService.SendCommandAsync($"osd-msg set sub-delay {_subDelay}");
        }

        private double _audioDelay = 0.0;
        private async void AudioDelayUp_Click(object sender, RoutedEventArgs e)
        {
            _audioDelay = Math.Round(_audioDelay + 0.1, 1);
            AudioDelayText.Text = $"{_audioDelay:0.0}s";
            await MpvIntegrationService.SendCommandAsync($"osd-msg set audio-delay {_audioDelay}");
        }

        private async void AudioDelayDown_Click(object sender, RoutedEventArgs e)
        {
            _audioDelay = Math.Round(_audioDelay - 0.1, 1);
            AudioDelayText.Text = $"{_audioDelay:0.0}s";
            await MpvIntegrationService.SendCommandAsync($"osd-msg set audio-delay {_audioDelay}");
        }

        private async void SkipIntro_Click(object sender, RoutedEventArgs e)
        {
            await MpvIntegrationService.SendCommandAsync("osd-msg seek 85 exact");
        }
    }
}
