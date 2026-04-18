using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface INotificationService
    {
        Task<ServiceResult<List<Notification>>> GetUserNotificationsAsync(string userId);
        Task<ServiceResult<bool>> MarkAsReadAsync(string notificationId);
        Task<ServiceResult<bool>> MarkAllAsReadAsync(string userId);
        Task<ServiceResult<bool>> CreateNotificationAsync(Notification notification);
        Task<ServiceResult<bool>> DeleteNotificationAsync(string notificationId);
        Task<ServiceResult<bool>> DeleteAllNotificationsAsync(string userId);
    }
}

