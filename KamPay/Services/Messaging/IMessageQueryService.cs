using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? ISP: Sadece mesaj okuma iþlemleri
    /// </summary>
    public interface IMessageQueryService
    {
        Task<ServiceResult<List<Message>>> GetConversationMessagesAsync(string conversationId, int limit = 50);
        Task<ServiceResult<List<Conversation>>> GetUserConversationsAsync(string userId);
        Task<ServiceResult<int>> GetTotalUnreadMessageCountAsync(string userId);
    }
}
