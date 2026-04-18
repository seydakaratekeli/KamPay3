using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Models;
using KamPay.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    public partial class NotificationsViewModel : ObservableObject, IDisposable
    {
        private readonly INotificationService _notificationService;
        private readonly IAuthenticationService _authService;
        private bool _disposed = false;

        // Localization Kısayolu
        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        public ObservableCollection<Notification> Notifications { get; } = new();

        public bool HasNotifications => Notifications.Count > 0;

        public NotificationsViewModel(
            INotificationService notificationService,
            IAuthenticationService authService)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        //  Sayfa göründüğünde otomatik yükleme için
        public async Task InitializeAsync()
        {
            await LoadNotificationsAsync();
        }

        [RelayCommand]
        private async Task LoadNotificationsAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                var user = await _authService.GetCurrentUserAsync();
                if (user == null)
                {
                    await Shell.Current.DisplayAlert(Res["Error"], Res["LoginRequired"], Res["Ok"]);
                    return;
                }

                var result = await _notificationService.GetUserNotificationsAsync(user.UserId);

                if (result.Success && result.Data != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Notifications.Clear();
                        foreach (var item in result.Data.OrderByDescending(n => n.CreatedAt))
                        {
                            Notifications.Add(item);
                        }
                        OnPropertyChanged(nameof(HasNotifications));
                    });

                    // Okunmamış bildirim sayısını sıfırlamak için global mesaj gönder
                    WeakReferenceMessenger.Default.Send(new UnreadGeneralNotificationStatusMessage(false));
                }
                else if (!result.Success)
                {
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message ?? Res["NotificationsLoadError"], Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ LoadNotifications hatası: {ex}");
                await Shell.Current.DisplayAlert(Res["Error"], $"{Res["NotificationsLoadError"]}: {ex.Message}", Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task RefreshNotificationsAsync()
        {
            IsRefreshing = true;
            await LoadNotificationsAsync();
        }

        [RelayCommand]
        private async Task MarkAsReadAsync(Notification notification)
        {
            if (notification == null) return;

            try
            {
                // ✅ Notification artık ObservableObject — IsRead değişince UI anında güncellenir
                notification.IsRead = true;

                var result = await _notificationService.MarkAsReadAsync(notification.NotificationId);

                if (!result.Success)
                {
                    notification.IsRead = false;
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ MarkAsRead hatası: {ex.Message}");
                notification.IsRead = false;
            }
        }

        [RelayCommand]
        private async Task MarkAllAsReadAsync()
        {
            if (!Notifications.Any() || IsLoading) return;

            try
            {
                IsLoading = true;

                var user = await _authService.GetCurrentUserAsync();
                if (user == null) return;

                // ✅ Her Notification kendi PropertyChanged'ını fırlatır — OnPropertyChanged(nameof(Notifications)) gereksiz
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var n in Notifications)
                        n.IsRead = true;
                });

                var result = await _notificationService.MarkAllAsReadAsync(user.UserId);

                if (!result.Success)
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ MarkAllAsRead hatası: {ex.Message}");
                await Shell.Current.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteNotificationAsync(Notification notification)
        {
            if (notification == null || IsLoading) return;

            try
            {
                IsLoading = true;

                var result = await _notificationService.DeleteNotificationAsync(notification.NotificationId);
                
                if (result.Success)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Notifications.Remove(notification);
                        OnPropertyChanged(nameof(HasNotifications));
                    });
                }
                else
                {
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ DeleteNotification hatası: {ex.Message}");
                await Shell.Current.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ClearAllAsync()
        {
            if (!Notifications.Any() || IsLoading) return;

            var confirm = await Shell.Current.DisplayAlert(
                Res["Warning"],
                Res["ConfirmClearAllNotifications"],
                Res["Yes"],
                Res["Cancel"]);

            if (!confirm) return;

            try
            {
                IsLoading = true;

                var user = await _authService.GetCurrentUserAsync();
                if (user == null) return;

                var result = await _notificationService.DeleteAllNotificationsAsync(user.UserId);

                if (result.Success)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Notifications.Clear();
                        OnPropertyChanged(nameof(HasNotifications));
                    });
                    
                    await Shell.Current.DisplayAlert(Res["Success"], Res["AllNotificationsDeleted"], Res["Ok"]);
                }
                else
                {
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ ClearAll hatası: {ex.Message}");
                await Shell.Current.DisplayAlert(Res["Error"], ex.Message, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Messenger temizliği 
                    // WeakReferenceMessenger.Default.UnregisterAll(this);
                }
                _disposed = true;
            }
        }
    }
}
