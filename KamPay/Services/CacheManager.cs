using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace KamPay.Services
{
    public class CacheManager<T>
    {
        private readonly ConcurrentDictionary<string, CacheEntry<T>> _cache = new();
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

        public void Set(string key, T value, TimeSpan? expiration = null)
        {
            var entry = new CacheEntry<T>
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expiration ?? _defaultExpiration)
            };
            _cache[key] = entry;
        }

        public bool TryGet(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    value = entry.Value;
                    return true;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                }
            }

            value = default;
            return false;
        }

        public void Clear() => _cache.Clear();

        private class CacheEntry<TValue>
        {
            public TValue Value { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}