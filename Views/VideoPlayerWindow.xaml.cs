using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MovieManagerDesktop.Views
{
    public partial class VideoPlayerWindow : Window
    {
        private DispatcherTimer _mouseIdleTimer;

        public VideoPlayerWindow(string filePath)
        {
            InitializeComponent();
            
            // Set DataContext
            this.DataContext = new ViewModels.VideoPlayerViewModel(filePath);
            
            _mouseIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _mouseIdleTimer.Tick += MouseIdleTimer_Tick;

            this.MouseMove += VideoPlayerWindow_MouseMove;
            this.MouseLeave += VideoPlayerWindow_MouseLeave;
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.VideoPlayerViewModel;
            if (vm != null && vm.MediaPlayer != null && !vm.MediaPlayer.IsPlaying)
            {
                vm.MediaPlayer.Play();
            }
        }

        private void VideoPlayerWindow_MouseMove(object sender, MouseEventArgs e)
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

        private void VideoPlayerWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            _mouseIdleTimer.Stop();
            ControlsGrid.Visibility = Visibility.Hidden;
        }
        
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.IsMouseOver && slider.IsMouseCaptured)
            {
                var vm = this.DataContext as ViewModels.VideoPlayerViewModel;
                if (vm != null && vm.SeekCommand.CanExecute((float)e.NewValue))
                {
                    vm.SeekCommand.Execute((float)e.NewValue);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.VideoPlayerViewModel;
            vm?.Dispose();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            var vm = this.DataContext as ViewModels.VideoPlayerViewModel;
            vm?.Dispose();
            base.OnClosed(e);
        }
    }
}
