using System;
using System.Linq;
using System.Windows;

namespace MovieManagerDesktop.Services
{
    public static class WindowHelper
    {
        public static bool? SafeShowDialog(Window dialog)
        {
            if (dialog == null) return false;

            try
            {
                var activeWindow = Application.Current?.Windows?.OfType<Window>()
                    .FirstOrDefault(w => w != dialog && w.IsActive && w.IsLoaded && w.IsVisible)
                    ?? (Application.Current?.MainWindow != null && Application.Current.MainWindow != dialog && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow.IsVisible ? Application.Current.MainWindow : null);

                if (activeWindow != null)
                {
                    dialog.Owner = activeWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.Owner = null;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                return dialog.ShowDialog();
            }
            catch
            {
                try
                {
                    dialog.Owner = null;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    return dialog.ShowDialog();
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
