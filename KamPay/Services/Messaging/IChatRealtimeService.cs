using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public interface IChatRealtimeService : IDisposable
    {
        Task<IReadOnlyList<Message>> LoadAndListenAsync(
            string conversationId,
            string currentUserId,
            Action<Message> onNewMessage,
            Action<Message> onUpdatedMessage,
            Action<string> onDeletedMessage,
            int initialLimit = 50);

        Task<IReadOnlyList<Message>> LoadOlderMessagesAsync(
            string conversationId,
            string beforeMessageId,
            string currentUserId,
            int limit = 50);

        void StopListening();
    }
}
