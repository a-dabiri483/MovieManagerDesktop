using System.Windows.Controls;
using System.Windows.Media;

using System.Windows.Input;

namespace MovieManagerDesktop.Views
{
    public partial class MoviesView : UserControl
    {
        public MoviesView()
        {
            InitializeComponent();
            this.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ScrollViewer_ScrollChanged));
        }

        private bool _isRestoringScroll = false;
        private bool _hasRestoredScroll = false;

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MoviesViewModel vm)
            {
                this.LayoutUpdated += MoviesView_LayoutUpdated;
            }
        }

        private void MoviesView_LayoutUpdated(object? sender, System.EventArgs e)
        {
            if (_hasRestoredScroll) return;

            if (DataContext is ViewModels.MoviesViewModel vm)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(MoviesItemsControl);
                if (scrollViewer != null)
                {
                    _hasRestoredScroll = true;
                    this.LayoutUpdated -= MoviesView_LayoutUpdated;

                    _isRestoringScroll = true;
                    Dispatcher.BeginInvoke(new System.Action(() => {
                        scrollViewer.ScrollToVerticalOffset(vm.ScrollPosition);
                        _isRestoringScroll = false;
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isRestoringScroll) return;

            if (DataContext is ViewModels.MoviesViewModel vm && e.OriginalSource is ScrollViewer scrollViewer)
            {
                vm.ScrollPosition = scrollViewer.VerticalOffset;
            }
        }

        private static T FindVisualChild<T>(System.Windows.DependencyObject obj) where T : System.Windows.DependencyObject
        {
            if (obj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    System.Windows.DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child != null && child is T)
                    {
                        return (T)child;
                    }
                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null) return childItem;
                }
            }
            return null;
        }

        private void Grid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (DataContext is ViewModels.MoviesViewModel vm)
                {
                    vm.PosterSize += (e.Delta > 0) ? 10 : -10;
                    if (vm.PosterSize < 150) vm.PosterSize = 150;
                    if (vm.PosterSize > 350) vm.PosterSize = 350;
                }
                e.Handled = true;
            }
            else
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(MoviesItemsControl);
                if (scrollViewer != null)
                {
                    if (e.Delta > 0)
                        scrollViewer.LineUp();
                    else
                        scrollViewer.LineDown();
                        
                    e.Handled = true;
                }
            }
        }

        private void MoviesItemsControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (sender is ListBox listBox)
                {
                    var focusedElement = Keyboard.FocusedElement as System.Windows.DependencyObject;
                    while (focusedElement != null && !(focusedElement is ListBoxItem))
                    {
                        focusedElement = VisualTreeHelper.GetParent(focusedElement);
                    }

                    if (focusedElement is ListBoxItem item && item.DataContext is ViewModels.GalleryItemViewModel vm)
                    {
                        if (DataContext is ViewModels.MoviesViewModel mainVm)
                        {
                            mainVm.OpenDetailsCommand.Execute(vm);
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private void MoviesItemsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.MoviesViewModel vm)
            {
                vm.SelectedCount = vm.Movies.Count(m => m.IsSelected);
                vm.IsInSelectionMode = vm.SelectedCount > 0;
            }
        }
    }
}
