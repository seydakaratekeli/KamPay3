using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using KamPay.Helpers;
using KamPay.Models;
using System.Reactive.Linq;

namespace KamPay.Services.Messaging
{
    public sealed class ChatRealtimeService : IChatRealtimeService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly HashSet<string> _knownMessageIds = new();
        private IDisposable? _subscription;
        private string _conversationId = string.Empty;

        public ChatRealtimeService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        public async Task<IReadOnlyList<Message>> LoadAndListenAsync(
            string conversationId,
            string currentUserId,
            Action<Message> onNewMessage,
            Action<Message> onUpdatedMessage,
            Action<string> onDeletedMessage,
            int initialLimit = 50)
        {
            StopListening();
            _conversationId = conversationId;

            var messages = await LoadLatestMessagesAsync(conversationId, currentUserId, initialLimit);
            foreach (var message in messages)
            {
                Track(message.MessageId);
            }

            _subscription = _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(conversationId)
                .AsObservable<Message>()
                .Where(e => e.Object != null || e.EventType == FirebaseEventType.Delete)
                .Buffer(TimeSpan.FromMilliseconds(150))
                .Where(batch => batch.Any())
                .Subscribe(
                    events => MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var e in events)
                        {
                            if (e.EventType == FirebaseEventType.Delete)
                            {
                                _knownMessageIds.Remove(e.Key);
                                onDeletedMessage(e.Key);
                                continue;
                            }

                            var message = e.Object;
                            if (message == null)
                                continue;

                            message.MessageId = e.Key;
                            message.IsSentByMe = message.SenderId == currentUserId;

                            if (message.IsDeleted)
                            {
                                _knownMessageIds.Remove(message.MessageId);
                                onDeletedMessage(message.MessageId);
                                continue;
                            }

                            if (_knownMessageIds.Contains(message.MessageId))
                            {
                                onUpdatedMessage(message);
                            }
                            else
                            {
                                Track(message.MessageId);
                                onNewMessage(message);
                            }
                        }
                    }),
                    error => AppLogger.DebugLog($"Chat realtime listener hatasi: {error.Message}"));

            return messages;
        }

        public async Task<IReadOnlyList<Message>> LoadOlderMessagesAsync(
            string conversationId,
            string beforeMessageId,
            string currentUserId,
            int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(beforeMessageId))
                return Array.Empty<Message>();

            var snapshot = await _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(conversationId)
                .OrderByKey()
                .EndAt(beforeMessageId)
                .LimitToLast(limit + 1)
                .OnceAsync<Message>();

            return snapshot
                .Where(item => item.Object != null && !item.Object.IsDeleted && item.Key != beforeMessageId)
                .Select(item => MapMessage(item.Key, item.Object, currentUserId))
                .OrderBy(message => message.SentAt)
                .ToList();
        }

        public void StopListening()
        {
            _subscription?.Dispose();
            _subscription = null;
            _knownMessageIds.Clear();
            _conversationId = string.Empty;
        }

        public void Dispose() => StopListening();

        private async Task<List<Message>> LoadLatestMessagesAsync(
            string conversationId,
            string currentUserId,
            int limit)
        {
            var snapshot = await _firebaseClient
                .Child(Constants.MessagesCollection)
                .Child(conversationId)
                .OrderByKey()
                .LimitToLast(limit)
                .OnceAsync<Message>();

            return snapshot
                .Where(item => item.Object != null && !item.Object.IsDeleted)
                .Select(item => MapMessage(item.Key, item.Object, currentUserId))
                .OrderBy(message => message.SentAt)
                .ToList();
        }

        private static Message MapMessage(string key, Message message, string currentUserId)
        {
            message.MessageId = key;
            message.IsSentByMe = message.SenderId == currentUserId;
            return message;
        }

        private void Track(string messageId)
        {
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                _knownMessageIds.Add(messageId);
            }
        }
    }
}
