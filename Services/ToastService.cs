using MovieManagerDesktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.Services
{
    public class ToastService
    {
        private static ToastService _instance;
        public static ToastService Instance => _instance ??= new ToastService();

        public ObservableCollection<ToastMessage> Toasts { get; } = new ObservableCollection<ToastMessage>();

        private ToastService() { }

        public void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 2000)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = new ToastMessage
                {
                    Title = title,
                    Message = message,
                    Type = type
                };
                
                // Keep max 3 toasts on screen to avoid clutter
                if (Toasts.Count >= 3)
                {
                    Toasts.RemoveAt(0);
                }

                Toasts.Add(toast);

                // Auto remove
                Task.Delay(durationMs).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var t = Toasts.FirstOrDefault(x => x.Id == toast.Id);
                        if (t != null)
                        {
                            t.IsClosing = true;
                            Task.Delay(300).ContinueWith(__ => 
                            {
                                Application.Current.Dispatcher.Invoke(() => Toasts.Remove(t));
                            });
                        }
                    });
                });
            });
        }

        public void ShowSuccess(string message, string title = "موفق") => Show(title, message, ToastType.Success);
        public void ShowError(string message, string title = "خطا") => Show(title, message, ToastType.Error);
        public void ShowWarning(string message, string title = "هشدار") => Show(title, message, ToastType.Warning);
        public void ShowInfo(string message, string title = "اطلاعات") => Show(title, message, ToastType.Info);
        
        public void Remove(ToastMessage toast)
        {
            if (Toasts.Contains(toast))
            {
                toast.IsClosing = true;
                Task.Delay(300).ContinueWith(_ => 
                {
                    Application.Current.Dispatcher.Invoke(() => Toasts.Remove(toast));
                });
            }
        }
    }
}
