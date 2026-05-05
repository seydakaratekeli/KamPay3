using KamPay.Helpers;

namespace KamPay.Services.Messaging
{
    public sealed class ChatCacheService : IChatCacheService
    {
        private const int MaxCachedConversations = 10;
        private readonly Dictionary<string, ChatConversationState> _cache = new();
        private readonly object _gate = new();

        public bool TryGet(string conversationId, out ChatConversationState state)
        {
            lock (_gate)
            {
                if (_cache.TryGetValue(conversationId, out var cached))
                {
                    cached.LastAccessedAt = DateTime.UtcNow;
                    state = cached;
                    return true;
                }
            }

            state = null!;
            return false;
        }

        public void Set(string conversationId, ChatConversationState state)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            lock (_gate)
            {
                if (_cache.Count >= MaxCachedConversations && !_cache.ContainsKey(conversationId))
                {
                    var oldestKey = _cache
                        .OrderBy(kvp => kvp.Value.LastAccessedAt)
                        .First().Key;

                    _cache.Remove(oldestKey);
                }

                _cache[conversationId] = state;
            }

            AppLogger.DebugLog($"Chat cache kaydedildi: {conversationId} ({state.Messages.Count})");
        }

        public void Remove(string conversationId)
        {
            lock (_gate)
            {
                _cache.Remove(conversationId);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _cache.Clear();
            }
        }

        public void ClearOld(int maxAgeMinutes)
        {
            var now = DateTime.UtcNow;

            lock (_gate)
            {
                var oldKeys = _cache
                    .Where(kvp => (now - kvp.Value.CachedAt).TotalMinutes > maxAgeMinutes)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldKeys)
                {
                    _cache.Remove(key);
                }

                if (_cache.Count > MaxCachedConversations)
                {
                    var overflowKeys = _cache
                        .OrderBy(kvp => kvp.Value.LastAccessedAt)
                        .Take(_cache.Count - MaxCachedConversations)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in overflowKeys)
                    {
                        _cache.Remove(key);
                    }
                }
            }
        }
    }
}
