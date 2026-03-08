using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Bildirim gönderim stratejilerini yönetir
/// Single Responsibility: Sadece bildirim orkestrasyon ve batch iþlemler
/// </summary>
public interface INotificationCoordinator
{
    /// <summary>
    /// Birden fazla kullanýcýya toplu bildirim gönder
    /// </summary>
    Task<ServiceResult<int>> SendBatchNotificationsAsync(List<string> userIds, string title, string message, NotificationType type);

    /// <summary>
    /// Transaction bazlý bildirimleri otomatik oluþtur ve gönder
    /// </summary>
    Task<ServiceResult<bool>> SendTransactionNotificationsAsync(Transaction transaction, string eventType);

    /// <summary>
    /// Zamanlý bildirim gönder (geciktirilmiþ)
    /// </summary>
    Task<ServiceResult<bool>> ScheduleNotificationAsync(string userId, string title, string message, DateTime sendAt);

    /// <summary>
    /// Öncelikli bildirim gönder (push notification + in-app)
    /// </summary>
    Task<ServiceResult<bool>> SendPriorityNotificationAsync(string userId, string title, string message, NotificationType type);

    /// <summary>
    /// Kullanýcý tercihlerine göre bildirim gönder (opt-in/opt-out kontrolü)
    /// </summary>
    Task<ServiceResult<bool>> SendUserPreferenceNotificationAsync(string userId, string title, string message, NotificationType type);

    /// <summary>
    /// Bildirim istatistiklerini getir
    /// </summary>
    Task<ServiceResult<NotificationStats>> GetNotificationStatsAsync(string userId);
}

public class NotificationStats
{
    public int TotalSent { get; set; }
    public int TotalRead { get; set; }
    public int UnreadCount { get; set; }
    public Dictionary<NotificationType, int> ByType { get; set; } = new();
}
