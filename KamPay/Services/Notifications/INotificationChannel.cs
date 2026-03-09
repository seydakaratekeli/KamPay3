using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services.Notifications
{
    /// <summary>
    /// ? OCP (Open/Closed Principle): Bildirim kanallarý için base interface
    /// Yeni bildirim kanalý eklemek için (SMS, Push, Email vb.) mevcut kodu deðiþtirmeye gerek yok
    /// Strategy Pattern kullanýmý
    /// </summary>
    public interface INotificationChannel
    {
        /// <summary>
        /// Bu kanalýn adý (Database, Email, Push vb.)
        /// </summary>
        string ChannelName { get; }
        
        /// <summary>
        /// Bu kanal aktif mi?
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Bildirimi bu kanal üzerinden gönder
        /// </summary>
        Task<ServiceResult<bool>> SendNotificationAsync(Notification notification);
        
        /// <summary>
        /// Birden fazla bildirimi toplu gönder (performans optimizasyonu)
        /// </summary>
        Task<ServiceResult<bool>> SendBatchNotificationsAsync(System.Collections.Generic.List<Notification> notifications);
    }
}
