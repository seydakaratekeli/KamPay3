using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public sealed class ChatConversationState
    {
        public required List<Message> Messages { get; set; }
        public Conversation? Conversation { get; set; }
        public required string OtherUserName { get; set; }
        public required string OtherUserPhoto { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
    }

    public interface IChatCacheService
    {
        bool TryGet(string conversationId, out ChatConversationState state);
        void Set(string conversationId, ChatConversationState state);
        void Remove(string conversationId);
        void Clear();
        void ClearOld(int maxAgeMinutes);
    }
}
