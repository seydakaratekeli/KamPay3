
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KamPay.Models;
using KamPay.Services;
using System.Linq;
using KamPay.Views;

namespace KamPay.ViewModels
{
    public partial class NotificationsViewModel : ObservableObject
    {
        private readonly INotificationService _notificationService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string emptyMessage = "Henüz bildiriminiz yok.";

        public ObservableCollection<Notification> Notifications { get; } = new();

        public NotificationsViewModel(INotificationService notificationService, IAuthenticationService authService)
        {
            _notificationService = notificationService;
            _authService = authService;

            _ = LoadNotificationsAsync();
        }

        [RelayCommand]
        private async Task LoadNotificationsAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    EmptyMessage = "Bildirimleri görmek için giriþ yapmalýsýnýz.";
                    return;
                }

                var result = await _notificationService.GetUserNotificationsAsync(currentUser.UserId);

                if (result.Success && result.Data != null)
                {
                    Notifications.Clear();
                    foreach (var notification in result.Data)
                    {
                        Notifications.Add(notification);
                    }
                    EmptyMessage = Notifications.Any() ? string.Empty : "Henüz bildiriminiz yok.";
                }
                else
                {
                    EmptyMessage = "Bildirimler yüklenemedi.";
                }
            }
            catch (Exception ex)
            {
                EmptyMessage = "Bir hata oluþtu.";
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }


        [RelayCommand]
        private async Task NotificationTappedAsync(Notification notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.ActionUrl)) return;

            try
            {
                // Bildirimi okundu olarak iþaretle
                if (!notification.IsRead)
                {
                    await _notificationService.MarkAsReadAsync(notification.NotificationId);
                    notification.IsRead = true;
                }

                await Shell.Current.GoToAsync(notification.ActionUrl);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", $"Sayfa açýlamadý: {ex.Message}", "Tamam");
            }
        }


        [RelayCommand]
        private async Task RefreshNotificationsAsync()
        {
            IsRefreshing = true;
            await LoadNotificationsAsync();
            IsRefreshing = false;
        }

        [RelayCommand]
        private async Task MarkAsReadAsync(Notification notification)
        {
            if (notification == null || notification.IsRead) return;

            var result = await _notificationService.MarkAsReadAsync(notification.NotificationId);
            if (result.Success)
            {
                notification.IsRead = true; // UI'da anýnda güncelleme için
            }
        }


        [RelayCommand]
        private async Task GoToRelatedPageAsync(Notification notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.ActionUrl)) return;

            // Bildirimi okundu olarak iþaretle
            await MarkAsReadAsync(notification);

            await Shell.Current.GoToAsync(notification.ActionUrl);
        }
    }
}