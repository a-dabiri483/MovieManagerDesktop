using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class PlayerView : UserControl
    {
        private PlayerViewModel? ViewModel => DataContext as PlayerViewModel;
        private Window? _parentWindow;
        private WindowState _previousWindowState = WindowState.Normal;
        private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
        private ResizeMode _previousResizeMode = ResizeMode.CanResize;

        public PlayerView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _parentWindow = Window.GetWindow(this);

            if (ViewModel != null && ViewModel.MediaPlayer != null)
            {
                VlcVideoView.MediaPlayer = ViewModel.MediaPlayer;

                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            Focus();
            Keyboard.Focus(this);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.Dispose();
            }

            // Restore parent window state if was fullscreen
            RestoreWindowState();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
            {
                if (ViewModel?.IsFullscreen == true)
                {
                    ApplyFullscreen();
                }
                else
                {
                    RestoreWindowState();
                }
            }
        }

        private void ApplyFullscreen()
        {
            if (_parentWindow == null) return;
            _previousWindowState = _parentWindow.WindowState;
            _previousWindowStyle = _parentWindow.WindowStyle;
            _previousResizeMode = _parentWindow.ResizeMode;

            _parentWindow.WindowState = WindowState.Normal;
            _parentWindow.WindowStyle = WindowStyle.None;
            _parentWindow.ResizeMode = ResizeMode.NoResize;
            _parentWindow.WindowState = WindowState.Maximized;
        }

        private void RestoreWindowState()
        {
            if (_parentWindow == null) return;
            _parentWindow.WindowState = _previousWindowState;
            _parentWindow.WindowStyle = _previousWindowStyle;
            _parentWindow.ResizeMode = _previousResizeMode;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.HandleKeyDown(e.Key, Keyboard.Modifiers);
                e.Handled = true;
            }
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            ViewModel?.HandleMouseMove();
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ViewModel != null && ViewModel.IsPlaying)
            {
                ViewModel.ShowControls = false;
            }
        }

        private void VlcVideoView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                ViewModel?.TogglePlayPause();
            }
        }

        private void VlcVideoView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.ToggleFullscreen();
            e.Handled = true;
        }

        private void VlcVideoView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel != null)
            {
                int delta = e.Delta > 0 ? 5 : -5;
                ViewModel.AdjustVolume(delta);
            }
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.StartSeek();
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null)
            {
                ViewModel.SeekTo(slider.Value);
                ViewModel.EndSeek();
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // If dragging, seek
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ViewModel != null && ViewModel.MediaPlayer != null)
            {
                ViewModel.MediaPlayer.Volume = (int)e.NewValue;
            }
        }

        private void SeekBackward5s_Click(object sender, RoutedEventArgs e) => ViewModel?.SeekRelative(-5);
        private void SeekForward5s_Click(object sender, RoutedEventArgs e) => ViewModel?.SeekRelative(5);

        private void TogglePlaylistDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ShowPlaylistDrawer = !ViewModel.ShowPlaylistDrawer;
        }

        private void ToggleBookmarksDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ShowBookmarksDrawer = !ViewModel.ShowBookmarksDrawer;
        }

        private void ToggleShortcutsHelp_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ShowShortcutsHelp = !ViewModel.ShowShortcutsHelp;
        }

        private void OpenAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateAudioTracksList();
                ViewModel.ShowAudioTracksPopup = !ViewModel.ShowAudioTracksPopup;
                ViewModel.ShowSubtitlesPopup = false;
            }
        }

        private void CloseAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ShowAudioTracksPopup = false;
        }

        private void OpenSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateSubtitleTracksList();
                ViewModel.ShowSubtitlesPopup = !ViewModel.ShowSubtitlesPopup;
                ViewModel.ShowAudioTracksPopup = false;
            }
        }

        private void CloseSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ShowSubtitlesPopup = false;
        }

        private void ToggleSpeedMenu_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AdjustSpeed(0.25f);
        }

        private void ToggleAspectRatioMenu_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CycleAspectRatio();
        }
    }
}
