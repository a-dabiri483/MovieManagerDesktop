using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System;

namespace MovieManagerDesktop.Views
{
    public partial class VideoPlayerView : UserControl
    {
        private DispatcherTimer _mouseIdleTimer;

        public VideoPlayerView()
        {
            InitializeComponent();
            
            _mouseIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _mouseIdleTimer.Tick += MouseIdleTimer_Tick;

            this.MouseMove += VideoPlayerView_MouseMove;
            this.MouseLeave += VideoPlayerView_MouseLeave;
        }

        private void VideoPlayerView_MouseMove(object sender, MouseEventArgs e)
        {
            ControlsGrid.Visibility = Visibility.Visible;
            this.Cursor = Cursors.Arrow;
            
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();
        }

        private void MouseIdleTimer_Tick(object sender, EventArgs e)
        {
            _mouseIdleTimer.Stop();
            ControlsGrid.Visibility = Visibility.Hidden;
            this.Cursor = Cursors.None;
        }

        private void VideoPlayerView_MouseLeave(object sender, MouseEventArgs e)
        {
            _mouseIdleTimer.Stop();
            ControlsGrid.Visibility = Visibility.Hidden;
        }
        
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Update position if slider is dragged by user
            if (sender is Slider slider && slider.IsMouseOver && slider.IsMouseCaptured)
            {
                var vm = this.DataContext as ViewModels.VideoPlayerViewModel;
                if (vm != null && vm.SeekCommand.CanExecute((float)e.NewValue))
                {
                    vm.SeekCommand.Execute((float)e.NewValue);
                }
            }
        }
    }
}
