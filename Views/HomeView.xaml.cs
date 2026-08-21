using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MovieManagerDesktop.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        private void ContinueWatchingScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Shift + Mouse Wheel = Horizontal scroll
                    if (e.Delta < 0)
                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + 120);
                    else
                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - 120);
                    e.Handled = true;
                }
                else
                {
                    // Normal Mouse Wheel = Pass vertical scroll up to main page ScrollViewer
                    var parentScrollViewer = FindParent<ScrollViewer>(scrollViewer);
                    if (parentScrollViewer != null)
                    {
                        parentScrollViewer.ScrollToVerticalOffset(parentScrollViewer.VerticalOffset - e.Delta);
                        e.Handled = true;
                    }
                }
            }
        }

        private void ScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            ContinueWatchingScrollViewer?.ScrollToHorizontalOffset(ContinueWatchingScrollViewer.HorizontalOffset + 280);
        }

        private void ScrollRight_Click(object sender, RoutedEventArgs e)
        {
            ContinueWatchingScrollViewer?.ScrollToHorizontalOffset(ContinueWatchingScrollViewer.HorizontalOffset - 280);
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
