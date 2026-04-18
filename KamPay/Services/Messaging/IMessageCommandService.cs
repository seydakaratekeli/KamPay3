using KamPay.Models;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? ISP: Sadece mesaj yazma/güncelleme iþlemleri
    /// </summary>
    public interface IMessageCommandService
    {
        Task<ServiceResult<Message>> SendMessageAsync(SendMessageRequest request, User sender);
        Task<ServiceResult<Conversation>> GetOrCreateConversationAsync(string user1Id, string user2Id, string? productId = null);
        Task<ServiceResult<bool>> DeleteConversationAsync(string conversationId, string userId);
        Task<ServiceResult<bool>> MarkMessagesAsReadAsync(string conversationId, string readerUserId);
        Task<ServiceResult<bool>> UpdateUserInfoInMessagesAsync(string userId, string? newName, string? newPhotoUrl);
        Task<ServiceResult<bool>> UpdateUserInfoInConversationsAsync(string userId, string? newName, string? newPhotoUrl);
        
        /// <summary>
        /// Real-time dinleme - Reactive Extensions pattern
        /// </summary>
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanýn")]
        IDisposable SubscribeToConversations(string userId, Action<List<Conversation>> onConversationsChanged);
        
        [Obsolete("Direkt ViewModel'de Firebase Observable kullanýn")]
        IDisposable SubscribeToMessages(string conversationId, Action<List<Message>> onMessagesChanged);
    }
}
