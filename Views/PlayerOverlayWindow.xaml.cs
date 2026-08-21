using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class PlayerOverlayWindow : Window
    {
        private PlayerViewModel? ViewModel => DataContext as PlayerViewModel;
        private Window? _parentPlayerWindow;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public PlayerOverlayWindow(PlayerViewModel viewModel, Window parentWindow)
        {
            InitializeComponent();
            DataContext = viewModel;
            _parentPlayerWindow = parentWindow;
            Owner = parentWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            // Forward all shortcut keys directly to ViewModel
            ViewModel.HandleKeyDown(e.Key, Keyboard.Modifiers);
            e.Handled = true;
        }

        private int GetHitTestDirection(Point pt)
        {
            if (ViewModel?.IsFullscreen == true || _parentPlayerWindow == null || _parentPlayerWindow.WindowState != WindowState.Normal)
                return 0;

            double edge = 10.0;
            bool left = pt.X <= edge;
            bool right = pt.X >= ActualWidth - edge;
            bool top = pt.Y <= edge;
            bool bottom = pt.Y >= ActualHeight - edge;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;
            return 0;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;
            Point pos = e.GetPosition(this);

            // Update resize cursor when near borders in windowed mode
            int ht = GetHitTestDirection(pos);
            switch (ht)
            {
                case HTLEFT:
                case HTRIGHT:
                    Cursor = Cursors.SizeWE;
                    break;
                case HTTOP:
                case HTBOTTOM:
                    Cursor = Cursors.SizeNS;
                    break;
                case HTTOPLEFT:
                case HTBOTTOMRIGHT:
                    Cursor = Cursors.SizeNWSE;
                    break;
                case HTTOPRIGHT:
                case HTBOTTOMLEFT:
                    Cursor = Cursors.SizeNESW;
                    break;
                default:
                    Cursor = Cursors.Arrow;
                    break;
            }

            ViewModel.HandleMouseMoveZone(pos.Y, ActualHeight, pos.X, ActualWidth);
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            // Check if mouse is over a scrollable control or inside any modal/popup/drawer
            if (e.OriginalSource is DependencyObject dep)
            {
                var scrollViewer = FindParent<ScrollViewer>(dep);
                var listBox = FindParent<ListBox>(dep);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta * 0.5));
                    e.Handled = true;
                    return;
                }
                if (listBox != null)
                {
                    var sv = FindVisualChild<ScrollViewer>(listBox);
                    if (sv != null)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.5));
                        e.Handled = true;
                        return;
                    }
                    return;
                }

                // If mouse is inside any modal/popup card even if outside viewport, don't adjust volume
                var parentCard = FindParentByName(dep, "SubtitleStudioCard") ??
                                 FindParentByName(dep, "OnlineSubCard") ??
                                 FindParentByName(dep, "ShortcutsHelpCard") ??
                                 FindParentByName(dep, "SubtitlesPopupCard") ??
                                 FindParentByName(dep, "AudioTracksPopupCard") ??
                                 FindParentByName(dep, "PlaylistDrawer") ??
                                 FindParentByName(dep, "BookmarksDrawer");

                if (parentCard != null)
                {
                    var svInCard = FindVisualChild<ScrollViewer>(parentCard);
                    if (svInCard != null)
                    {
                        svInCard.ScrollToVerticalOffset(svInCard.VerticalOffset - (e.Delta * 0.5));
                    }
                    e.Handled = true;
                    return;
                }
            }

            if (e.Delta > 0)
            {
                ViewModel.AdjustVolume(5);
            }
            else if (e.Delta < 0)
            {
                ViewModel.AdjustVolume(-5);
            }

            ViewModel.EnforceDisableInternalSubtitles();
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;

            // 1. Mouse Side Buttons: XButton1 (5s Back), XButton2 (5s Forward)
            if (e.ChangedButton == MouseButton.XButton1)
            {
                ViewModel.SeekRelative(-5);
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.XButton2)
            {
                ViewModel.SeekRelative(5);
                e.Handled = true;
                return;
            }

            // 2. Middle click = Fullscreen toggle
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                ViewModel.ToggleFullscreen();
                e.Handled = true;
                return;
            }

            // 3. Left Click Handling
            if (e.ChangedButton == MouseButton.Left)
            {
                // A. Check if clicking inside any modal / popup / drawer - let modal handle drag and controls!
                if (e.OriginalSource is DependencyObject depObj)
                {
                    if (FindParentByName(depObj, "SubtitleStudioCard") != null ||
                        FindParentByName(depObj, "OnlineSubCard") != null ||
                        FindParentByName(depObj, "ShortcutsHelpCard") != null ||
                        FindParentByName(depObj, "SubtitlesPopupCard") != null ||
                        FindParentByName(depObj, "AudioTracksPopupCard") != null ||
                        FindParentByName(depObj, "PlaylistDrawer") != null ||
                        FindParentByName(depObj, "BookmarksDrawer") != null ||
                        FindParentByName(depObj, "TranslationBadge") != null)
                    {
                        return;
                    }
                }

                Point pt = e.GetPosition(this);

                // B. Check if clicking on window resize borders
                if (_parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && !ViewModel.IsFullscreen)
                {
                    int ht = GetHitTestDirection(pt);
                    if (ht != 0)
                    {
                        var helper = new WindowInteropHelper(_parentPlayerWindow);
                        if (helper.Handle != IntPtr.Zero)
                        {
                            ReleaseCapture();
                            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)ht, IntPtr.Zero);
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // C. Check if click is on an interactive control (Button, Slider, ListBox, TextBox, etc.)
                if (e.OriginalSource is DependencyObject dep)
                {
                    var parentButton = FindParent<Button>(dep);
                    var parentSlider = FindParent<Slider>(dep);
                    var parentListBox = FindParent<ListBox>(dep);
                    var parentTextBox = FindParent<TextBox>(dep);

                    if (parentButton != null || parentSlider != null || parentListBox != null || parentTextBox != null)
                    {
                        return; // Let child control handle its own click
                    }

                    // Check if click is inside top/bottom control bars
                    var parentBorder = FindParent<Border>(dep);
                    if (parentBorder != null)
                    {
                        string? borderName = parentBorder.Name;
                        if (borderName == "BottomControlsBar" || borderName == "TopControlsBar")
                        {
                            return;
                        }
                    }
                }

                // D. Click-Outside to Dismiss Popups & Drawers
                if (ViewModel.HasOpenFlyout && !ViewModel.ShowSubtitleStudioModal)
                {
                    ViewModel.CloseAllPopups();
                    e.Handled = true;
                    return;
                }

                // E. Double-click on background video = Play/Pause
                if (e.ClickCount == 2)
                {
                    ViewModel.TogglePlayPause();
                    e.Handled = true;
                    return;
                }

                // F. Single-click on background video = Window Drag (in windowed mode)
                if (e.ClickCount == 1 && _parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && !ViewModel.IsFullscreen)
                {
                    try
                    {
                        _parentPlayerWindow.DragMove();
                    }
                    catch { }
                }
            }
        }

        private void TopControlsBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var parentButton = FindParent<Button>(dep);
                if (parentButton != null) return;
            }

            if (e.ClickCount == 2)
            {
                ViewModel?.ToggleFullscreen();
            }
            else if (e.LeftButton == MouseButtonState.Pressed && _parentPlayerWindow != null && _parentPlayerWindow.WindowState == WindowState.Normal && ViewModel?.IsFullscreen != true)
            {
                try
                {
                    _parentPlayerWindow.DragMove();
                }
                catch { }
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPlayerWindow != null)
            {
                _parentPlayerWindow.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ToggleFullscreen();
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null && e.LeftButton == MouseButtonState.Pressed)
            {
                slider.CaptureMouse();
                ViewModel.StartSeek();
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                e.Handled = true;
            }
        }

        private void SeekSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null && slider.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                e.Handled = true;
            }
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && ViewModel != null)
            {
                if (slider.IsMouseCaptured)
                {
                    slider.ReleaseMouseCapture();
                }
                Point pt = e.GetPosition(slider);
                if (slider.ActualWidth > 0)
                {
                    double ratio = Math.Clamp(pt.X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = ratio;
                    ViewModel.SeekTo(ratio);
                }
                ViewModel.EndSeek();
                e.Handled = true;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ViewModel?.HandleMouseMove();
        }

        private void SeekBackward5s_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SeekRelative(-5);
        }

        private void SeekForward5s_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.SeekRelative(5);
        }

        private void TogglePlaylistDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowPlaylistDrawer = !ViewModel.ShowPlaylistDrawer;
            }
        }

        private void ToggleBookmarksDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowBookmarksDrawer = !ViewModel.ShowBookmarksDrawer;
            }
        }

        private void ToggleShortcutsHelp_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowShortcutsHelp = !ViewModel.ShowShortcutsHelp;
            }
        }

        private void OpenAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateAudioTracksList();
                ViewModel.ShowAudioTracksPopup = !ViewModel.ShowAudioTracksPopup;
            }
        }

        private void CloseAudioTracksPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowAudioTracksPopup = false;
            }
        }

        private void OpenSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateSubtitleTracksList();
                ViewModel.ShowSubtitlesPopup = !ViewModel.ShowSubtitlesPopup;
            }
        }

        private void CloseSubtitlesPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSubtitlesPopup = false;
            }
        }

        private void ToggleSpeedMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSpeedPopup = !ViewModel.ShowSpeedPopup;
            }
        }

        private void CloseSpeedPopup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ShowSpeedPopup = false;
            }
        }

        private void SpeedButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.AdjustSpeed(-0.1f);
            e.Handled = true;
        }

        private void SpeedButton_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ViewModel?.AdjustSpeed(0.1f);
            }
            else if (e.Delta < 0)
            {
                ViewModel?.AdjustSpeed(-0.1f);
            }
            e.Handled = true;
        }

        private void ToggleAspectRatioMenu_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CycleAspectRatio();
        }

        private void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddBookmark();
        }

        private Point _studioDragStart;
        private bool _isDraggingStudio = false;

        private void StudioCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    var btn = FindParent<Button>(dep);
                    var slider = FindParent<Slider>(dep);
                    var thumb = FindParent<System.Windows.Controls.Primitives.Thumb>(dep);
                    var txt = FindParent<TextBox>(dep);
                    if (btn != null || slider != null || thumb != null || txt != null)
                    {
                        return;
                    }
                }

                _isDraggingStudio = true;
                _studioDragStart = e.GetPosition(this);
                SubtitleStudioCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void StudioCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingStudio && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _studioDragStart.X;
                double deltaY = currentPoint.Y - _studioDragStart.Y;

                StudioTranslateTransform.X += deltaX;
                StudioTranslateTransform.Y += deltaY;

                _studioDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void StudioCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingStudio)
            {
                _isDraggingStudio = false;
                SubtitleStudioCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private Point _onlineSubDragStart;
        private bool _isDraggingOnlineSub = false;

        private void OnlineSubCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    var btn = FindParent<Button>(dep);
                    var txt = FindParent<TextBox>(dep);
                    var sv = FindParent<ScrollViewer>(dep);
                    if (btn != null || txt != null)
                    {
                        return;
                    }
                }

                _isDraggingOnlineSub = true;
                _onlineSubDragStart = e.GetPosition(this);
                OnlineSubCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnlineSubCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingOnlineSub && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _onlineSubDragStart.X;
                double deltaY = currentPoint.Y - _onlineSubDragStart.Y;

                OnlineSubTranslateTransform.X += deltaX;
                OnlineSubTranslateTransform.Y += deltaY;

                _onlineSubDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void OnlineSubCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingOnlineSub)
            {
                _isDraggingOnlineSub = false;
                OnlineSubCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnlineSubSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel?.SearchOnlineSubtitlesCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void StudioScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.5));
                e.Handled = true;
            }
        }

        private void GenericModal_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.5));
                e.Handled = true;
            }
        }

        // ═══════════ SUBTITLES POPUP DRAG HANDLERS ═══════════
        private Point _subtitlesPopupDragStart;
        private bool _isDraggingSubtitlesPopup = false;

        private void SubtitlesPopupCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (FindParent<Button>(dep) != null || FindParent<Slider>(dep) != null || FindParent<TextBox>(dep) != null)
                    {
                        return;
                    }
                }

                _isDraggingSubtitlesPopup = true;
                _subtitlesPopupDragStart = e.GetPosition(this);
                SubtitlesPopupCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void SubtitlesPopupCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSubtitlesPopup && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _subtitlesPopupDragStart.X;
                double deltaY = currentPoint.Y - _subtitlesPopupDragStart.Y;

                SubtitlesPopupTranslateTransform.X += deltaX;
                SubtitlesPopupTranslateTransform.Y += deltaY;

                _subtitlesPopupDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void SubtitlesPopupCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSubtitlesPopup)
            {
                _isDraggingSubtitlesPopup = false;
                SubtitlesPopupCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // ═══════════ AUDIO TRACKS POPUP DRAG HANDLERS ═══════════
        private Point _audioPopupDragStart;
        private bool _isDraggingAudioPopup = false;

        private void AudioTracksPopupCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (FindParent<Button>(dep) != null || FindParent<Slider>(dep) != null || FindParent<TextBox>(dep) != null)
                    {
                        return;
                    }
                }

                _isDraggingAudioPopup = true;
                _audioPopupDragStart = e.GetPosition(this);
                AudioTracksPopupCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void AudioTracksPopupCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingAudioPopup && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _audioPopupDragStart.X;
                double deltaY = currentPoint.Y - _audioPopupDragStart.Y;

                AudioPopupTranslateTransform.X += deltaX;
                AudioPopupTranslateTransform.Y += deltaY;

                _audioPopupDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void AudioTracksPopupCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingAudioPopup)
            {
                _isDraggingAudioPopup = false;
                AudioTracksPopupCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // ═══════════ SHORTCUTS HELP MODAL DRAG HANDLERS ═══════════
        private Point _shortcutsHelpDragStart;
        private bool _isDraggingShortcutsHelp = false;

        private void ShortcutsCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (FindParent<Button>(dep) != null || FindParent<ScrollViewer>(dep) != null || FindParent<TextBox>(dep) != null)
                    {
                        return;
                    }
                }

                _isDraggingShortcutsHelp = true;
                _shortcutsHelpDragStart = e.GetPosition(this);
                ShortcutsHelpCard.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ShortcutsCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingShortcutsHelp && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _shortcutsHelpDragStart.X;
                double deltaY = currentPoint.Y - _shortcutsHelpDragStart.Y;

                ShortcutsTranslateTransform.X += deltaX;
                ShortcutsTranslateTransform.Y += deltaY;

                _shortcutsHelpDragStart = currentPoint;
                e.Handled = true;
            }
        }

        private void ShortcutsCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingShortcutsHelp)
            {
                _isDraggingShortcutsHelp = false;
                ShortcutsHelpCard.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private static FrameworkElement? FindParentByName(DependencyObject child, string name)
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.Name == name)
                {
                    return fe;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}
