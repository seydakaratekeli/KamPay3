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

        // Yeni bir bildirim oluşturur ve Firebase'e kaydeder.
        public async Task<ServiceResult<bool>> CreateNotificationAsync(Notification notification)
        {
            try
            {
                if (notification == null || string.IsNullOrEmpty(notification.UserId))
                {
                    return ServiceResult<bool>.FailureResult("Bildirim veya kullanıcı ID'si geçersiz.");
                }

                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notification.NotificationId)
                    .PutAsync(notification);

              //  WeakReferenceMessenger.Default.Send(new UnreadGeneralNotificationStatusMessage(true));


                return ServiceResult<bool>.SuccessResult(true, "Bildirim oluşturuldu.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Bildirim oluşturulurken hata oluştu.", ex.Message);
            }
        }

        /// Belirli bir kullanıcının tüm bildirimlerini getirir.
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
                return ServiceResult<List<Notification>>.FailureResult("Bildirimler alınamadı.", ex.Message);
            }
        }

        /// Belirli bir bildirimi okundu olarak işaretler.
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
                    return ServiceResult<bool>.FailureResult("Bildirim bulunamadı.");
                }

                if (notification.IsRead)
                {
                    return ServiceResult<bool>.SuccessResult(true, "Bildirim zaten okunmuş.");
                }

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notificationId)
                    .PutAsync(notification);

                await CheckAndBroadcastUnreadStatus(notification.UserId);

                return ServiceResult<bool>.SuccessResult(true, "Bildirim okundu olarak işaretlendi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }

        // 🔥 YENİ EKLENEN METOTLAR

        public async Task<ServiceResult<bool>> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var allNotifications = await GetUserNotificationsAsync(userId);
                if (allNotifications.Success && allNotifications.Data != null)
                {
                    var unreadNotifications = allNotifications.Data.Where(n => !n.IsRead).ToList();

                    // Paralel güncelleme yerine döngüyle güncelleme (Firebase Realtime DB için daha güvenli)
                    foreach (var notification in unreadNotifications)
                    {
                        notification.IsRead = true;
                        notification.ReadAt = DateTime.UtcNow;

                        await _firebaseClient
                            .Child(Constants.NotificationsCollection)
                            .Child(notification.NotificationId)
                            .PutAsync(notification);
                    }

                    await CheckAndBroadcastUnreadStatus(userId);
                }
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Tümünü okundu işaretlerken hata.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> DeleteNotificationAsync(string notificationId)
        {
            try
            {
                await _firebaseClient
                    .Child(Constants.NotificationsCollection)
                    .Child(notificationId)
                    .DeleteAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Silme hatası.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> DeleteAllNotificationsAsync(string userId)
        {
            try
            {
                var allNotifications = await GetUserNotificationsAsync(userId);
                if (allNotifications.Success && allNotifications.Data != null)
                {
                    foreach (var notification in allNotifications.Data)
                    {
                        await _firebaseClient
                            .Child(Constants.NotificationsCollection)
                            .Child(notification.NotificationId)
                            .DeleteAsync();
                    }
                }
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Toplu silme hatası.", ex.Message);
            }
        }
    }
}
    
    