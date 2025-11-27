// KamPay/Services/FirebaseNotificationService.cs

using CommunityToolkit.Mvvm.Messaging;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class FirebaseNotificationService : INotificationService
    {
        private readonly FirebaseClient _firebaseClient;

        public FirebaseNotificationService()
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        }

        private async Task CheckAndBroadcastUnreadStatus(string userId)
        {
            var result = await GetUserNotificationsAsync(userId);
            bool hasUnread = result.Success && result.Data != null && result.Data.Any(n => !n.IsRead);
            WeakReferenceMessenger.Default.Send(new UnreadGeneralNotificationStatusMessage(hasUnread));
        }

        // Yeni bir bildirim oluþturur ve Firebase'e kaydeder.
        public async Task<ServiceResult<bool>> CreateNotificationAsync(Notification notification)
        {
            try
            {
                if (notification == null || string.IsNullOrEmpty(notification.UserId))
                {
                    return ServiceResult<bool>.FailureResult("Bildirim veya kullanýcý ID'si geçersiz.");
                }

                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notification.NotificationId)
                    .PutAsync(notification);

              //  WeakReferenceMessenger.Default.Send(new UnreadGeneralNotificationStatusMessage(true));


                return ServiceResult<bool>.SuccessResult(true, "Bildirim oluþturuldu.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Bildirim oluþturulurken hata oluþtu.", ex.Message);
            }
        }

        /// Belirli bir kullanýcýnýn tüm bildirimlerini getirir.
        public async Task<ServiceResult<List<Notification>>> GetUserNotificationsAsync(string userId)
        {
            try
            {
                var notificationEntries = await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .OrderBy("UserId")
                    .EqualTo(userId)
                    .OnceAsync<Notification>();

                var notifications = notificationEntries
                    .Select(n => n.Object)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                return ServiceResult<List<Notification>>.SuccessResult(notifications);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<Notification>>.FailureResult("Bildirimler alýnamadý.", ex.Message);
            }
        }

        /// Belirli bir bildirimi okundu olarak iþaretler.
        public async Task<ServiceResult<bool>> MarkAsReadAsync(string notificationId)
        {
            try
            {
                var notification = await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notificationId)
                    .OnceSingleAsync<Notification>();

                if (notification == null)
                {
                    return ServiceResult<bool>.FailureResult("Bildirim bulunamadý.");
                }

                if (notification.IsRead)
                {
                    return ServiceResult<bool>.SuccessResult(true, "Bildirim zaten okunmuþ.");
                }

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notificationId)
                    .PutAsync(notification);

                await CheckAndBroadcastUnreadStatus(notification.UserId);

                return ServiceResult<bool>.SuccessResult(true, "Bildirim okundu olarak iþaretlendi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ýþlem sýrasýnda hata oluþtu.", ex.Message);
            }
        }
    }
}