using Firebase.Database;
using KamPay.Helpers;
using KamPay.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.ViewModels;
using Firebase.Database.Query;

namespace KamPay.Services.Notifications
{
    /// <summary>
    /// ✅ OCP: Firebase Database bildirim kanalı
    /// Mevcut FirebaseNotificationService implementasyonu
    /// </summary>
    public class DatabaseNotificationChannel : INotificationChannel
    {
        private readonly FirebaseClient _firebaseClient;
        
        public string ChannelName => "Database";
        public bool IsEnabled => true;
        
        public DatabaseNotificationChannel(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new System.ArgumentNullException(nameof(firebaseClient));
        }
        
        public async Task<ServiceResult<bool>> SendNotificationAsync(Notification notification)
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

                return ServiceResult<bool>.SuccessResult(true, "Bildirim kaydedildi.");
            }
            catch (System.Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Bildirim kaydedilemedi.", ex.Message);
            }
        }
        
        public async Task<ServiceResult<bool>> SendBatchNotificationsAsync(List<Notification> notifications)
        {
            try
            {
                // ✅ Paralel kaydetme - performans optimizasyonu
                var tasks = notifications.Select(n => SendNotificationAsync(n));
                var results = await Task.WhenAll(tasks);
                
                var failedCount = results.Count(r => !r.Success);
                
                if (failedCount > 0)
                {
                    return ServiceResult<bool>.FailureResult(
                        $"{failedCount}/{notifications.Count} bildirim kaydedilemedi");
                }
                
                return ServiceResult<bool>.SuccessResult(true, $"{notifications.Count} bildirim kaydedildi");
            }
            catch (System.Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Toplu bildirim hatası", ex.Message);
            }
        }
    }
    
    /// <summary>
    /// ✅ OCP: In-App bildirim kanalı (WeakReferenceMessenger)
    /// Real-time UI güncellemesi için
    /// </summary>
    public class InAppNotificationChannel : INotificationChannel
    {
        public string ChannelName => "InApp";
        public bool IsEnabled => true;
        
        public async Task<ServiceResult<bool>> SendNotificationAsync(Notification notification)
        {
            try
            {
                // ✅ UI'a real-time bildirim gönder
                WeakReferenceMessenger.Default.Send(
                    new UnreadGeneralNotificationStatusMessage(true));
                
                return await Task.FromResult(ServiceResult<bool>.SuccessResult(true));
            }
            catch (System.Exception ex)
            {
                return ServiceResult<bool>.FailureResult("In-app bildirim hatası", ex.Message);
            }
        }
        
        public async Task<ServiceResult<bool>> SendBatchNotificationsAsync(List<Notification> notifications)
        {
            // Toplu in-app bildirimi (sadece bir kere UI'ı güncelle)
            return await SendNotificationAsync(notifications.First());
        }
    }
}
