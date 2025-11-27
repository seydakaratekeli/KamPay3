using KamPay.Models;

namespace KamPay.Services
{

    public interface IMessagingService
    {
        Task<ServiceResult<Message>> SendMessageAsync(SendMessageRequest request, User sender);
        Task<ServiceResult<List<Message>>> GetConversationMessagesAsync(string conversationId, int limit = 50);
        Task<ServiceResult<List<Conversation>>> GetUserConversationsAsync(string userId);
        Task<ServiceResult<Conversation>> GetOrCreateConversationAsync(string user1Id, string user2Id, string productId = null);
       Task<ServiceResult<bool>> DeleteConversationAsync(string conversationId, string userId);
        Task<ServiceResult<bool>> MarkMessagesAsReadAsync(string conversationId, string readerUserId);
        Task<ServiceResult<int>> GetTotalUnreadMessageCountAsync(string userId);
        IDisposable SubscribeToConversations(string userId, Action<List<Conversation>> onConversationsChanged);
        IDisposable SubscribeToMessages(string conversationId, Action<List<Message>> onMessagesChanged);


    }
}