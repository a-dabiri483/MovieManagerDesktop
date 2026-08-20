using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MovieManagerDesktop.Helpers
{
    public static class ScrollViewerHelper
    {
        public static readonly DependencyProperty BubbleMouseWheelProperty =
            DependencyProperty.RegisterAttached(
                "BubbleMouseWheel",
                typeof(bool),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject obj) => (bool)obj.GetValue(BubbleMouseWheelProperty);
        public static void SetBubbleMouseWheel(DependencyObject obj, bool value) => obj.SetValue(BubbleMouseWheelProperty, value);

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                else
                {
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                }
            }
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // If Shift key is held, allow horizontal scrolling with the mouse wheel
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                else
                {
                    // Otherwise, seamlessly bubble vertical mouse wheel to parent container!
                    e.Handled = true;
                    var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = sender
                    };

                    DependencyObject parent = VisualTreeHelperGetParent(scrollViewer);
                    if (parent is UIElement uiElement)
                    {
                        uiElement.RaiseEvent(eventArg);
                    }
                    else if (scrollViewer.Parent is UIElement logicalParent)
                    {
                        logicalParent.RaiseEvent(eventArg);
                    }
                }
            }
        }

        private static DependencyObject VisualTreeHelperGetParent(DependencyObject child)
        {
            try
            {
                return System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            catch
            {
                return null;
            }
        }
    }
}
