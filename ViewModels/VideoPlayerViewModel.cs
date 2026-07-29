using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using MovieManagerDesktop.Messages;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class VideoPlayerViewModel : ObservableObject, IDisposable
    {
        private LibVLC _libVLC;
        
        [ObservableProperty]
        private MediaPlayer _mediaPlayer;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private float _position;

        [ObservableProperty]
        private long _time;

        [ObservableProperty]
        private long _length;

        [ObservableProperty]
        private int _volume = 100;
        
        [ObservableProperty]
        private string _currentTimeDisplay = "00:00:00";
        
        [ObservableProperty]
        private string _totalTimeDisplay = "00:00:00";

        [ObservableProperty]
        private bool _isControlsVisible = true;

        public VideoPlayerViewModel(string filePath)
        {
            Title = Path.GetFileName(filePath);
            
            // Initialize LibVLC
            Core.Initialize();
            _libVLC = new LibVLC();
            MediaPlayer = new MediaPlayer(_libVLC);
            
            MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            MediaPlayer.PositionChanged += MediaPlayer_PositionChanged;
            MediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            MediaPlayer.Playing += (s, e) => IsPlaying = true;
            MediaPlayer.Paused += (s, e) => IsPlaying = false;
            MediaPlayer.Stopped += (s, e) => IsPlaying = false;

            var media = new Media(_libVLC, filePath, FromType.FromPath);
            MediaPlayer.Media = media;
        }

        private void MediaPlayer_LengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            Length = e.Length;
            TotalTimeDisplay = TimeSpan.FromMilliseconds(Length).ToString(@"hh\:mm\:ss");
        }

        private void MediaPlayer_PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
        {
            Position = e.Position;
        }

        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            Time = e.Time;
            CurrentTimeDisplay = TimeSpan.FromMilliseconds(Time).ToString(@"hh\:mm\:ss");
        }

        [RelayCommand]
        private void PlayPause()
        {
            if (MediaPlayer == null) return;
            
            if (MediaPlayer.IsPlaying)
            {
                MediaPlayer.Pause();
            }
            else
            {
                MediaPlayer.Play();
            }
        }

        [RelayCommand]
        private void Stop()
        {
            MediaPlayer?.Stop();
        }

        [RelayCommand]
        private void Seek(float position)
        {
            if (MediaPlayer != null && MediaPlayer.IsSeekable)
            {
                MediaPlayer.Position = position;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            Dispose();
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new HomeViewModel())); // Or back to details
        }

        public void Dispose()
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
                MediaPlayer.PositionChanged -= MediaPlayer_PositionChanged;
                MediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                MediaPlayer.Stop();
                MediaPlayer.Dispose();
                MediaPlayer = null;
            }

            if (_libVLC != null)
            {
                _libVLC.Dispose();
                _libVLC = null;
            }
        }
    }
}
