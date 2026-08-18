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

        private string _lastMessage = string.Empty;
        private DateTime _lastMessageTime = DateTime.MinValue;

        private ToastService() { }

        public void Show(string title, string message, ToastType type = ToastType.Info, int? durationMs = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // Debounce identical consecutive messages within 1.5s
            if (_lastMessage == message && (DateTime.UtcNow - _lastMessageTime).TotalMilliseconds < 1500)
            {
                return;
            }
            _lastMessage = message;
            _lastMessageTime = DateTime.UtcNow;

            int effectiveDuration = durationMs ?? type switch
            {
                ToastType.Error => 5000,
                ToastType.Warning => 4500,
                ToastType.Success => 3200,
                ToastType.Info => 3000,
                _ => 3000
            };

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            void Action()
            {
                var toast = new ToastMessage
                {
                    Title = string.IsNullOrWhiteSpace(title) ? GetDefaultTitle(type) : title,
                    Message = message,
                    Type = type
                };

                // Keep max 3 toasts on screen to avoid visual clutter
                while (Toasts.Count >= 3)
                {
                    Toasts.RemoveAt(0);
                }

                Toasts.Add(toast);

                // Auto remove after specified duration
                Task.Delay(effectiveDuration).ContinueWith(_ =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var t = Toasts.FirstOrDefault(x => x.Id == toast.Id);
                        if (t != null)
                        {
                            t.IsClosing = true;
                            Task.Delay(300).ContinueWith(__ =>
                            {
                                Application.Current?.Dispatcher?.Invoke(() => Toasts.Remove(t));
                            });
                        }
                    });
                });
            }

            if (dispatcher.CheckAccess())
            {
                Action();
            }
            else
            {
                dispatcher.Invoke(Action);
            }
        }

        private static string GetDefaultTitle(ToastType type) => type switch
        {
            ToastType.Success => "موفق",
            ToastType.Error => "خطا",
            ToastType.Warning => "هشدار",
            ToastType.Info => "اطلاعات",
            _ => "پیام"
        };

        public void ShowSuccess(string message, string title = "موفق", int? durationMs = null) => Show(title, message, ToastType.Success, durationMs);
        public void ShowError(string message, string title = "خطا", int? durationMs = null) => Show(title, message, ToastType.Error, durationMs);
        public void ShowWarning(string message, string title = "هشدار", int? durationMs = null) => Show(title, message, ToastType.Warning, durationMs);
        public void ShowInfo(string message, string title = "اطلاعات", int? durationMs = null) => Show(title, message, ToastType.Info, durationMs);

        public void Remove(ToastMessage toast)
        {
            if (toast == null) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.Invoke(() =>
            {
                if (Toasts.Contains(toast))
                {
                    toast.IsClosing = true;
                    Task.Delay(250).ContinueWith(_ =>
                    {
                        Application.Current?.Dispatcher?.Invoke(() => Toasts.Remove(toast));
                    });
                }
            });
        }
    }
}
