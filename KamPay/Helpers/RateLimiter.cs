using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace KamPay.Helpers
{
    /// <summary>
    /// ⚠️ DEPRECATED: Bu class artık AdvancedRateLimiter ile değiştirilmiştir.
    /// Geriye dönük uyumluluk için tutulmuştur.
    /// Yeni kod için lütfen SecureRateLimiters kullanın.
    /// </summary>
    public class RateLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestLog;
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;

        public RateLimiter(int maxRequests, TimeSpan timeWindow)
        {
            _maxRequests = maxRequests;
            _timeWindow = timeWindow;
            _requestLog = new ConcurrentDictionary<string, Queue<DateTime>>();
        }

        public bool IsRequestAllowed(string identifier)
        {
            var now = DateTime.UtcNow;
            var requests = _requestLog.GetOrAdd(identifier, _ => new Queue<DateTime>());

            lock (requests)
            {
                // Remove old requests outside the time window
                while (requests.Count > 0 && (now - requests.Peek()) > _timeWindow)
                {
                    requests.Dequeue();
                }

                // Check if we've exceeded the limit
                if (requests.Count >= _maxRequests)
                {
                    return false;
                }

                // Add this request to the log
                requests.Enqueue(now);
                return true;
            }
        }

        public int GetRemainingRequests(string identifier)
        {
            if (!_requestLog.TryGetValue(identifier, out var requests))
            {
                return _maxRequests;
            }

            var now = DateTime.UtcNow;
            lock (requests)
            {
                // Remove old requests
                while (requests.Count > 0 && (now - requests.Peek()) > _timeWindow)
                {
                    requests.Dequeue();
                }

                return Math.Max(0, _maxRequests - requests.Count);
            }
        }

        public DateTime? GetResetTime(string identifier)
        {
            if (!_requestLog.TryGetValue(identifier, out var requests))
            {
                return null;
            }

            lock (requests)
            {
                if (requests.Count == 0)
                    return null;

                return requests.Peek().Add(_timeWindow);
            }
        }

        public void Reset(string identifier)
        {
            _requestLog.TryRemove(identifier, out _);
        }

        public void ResetAll()
        {
            _requestLog.Clear();
        }
    }

    /// <summary>
    /// ⚠️ DEPRECATED: Eski rate limiter'lar. Yeni kod için SecureRateLimiters kullanın.
    /// Geriye dönük uyumluluk için tutulmuştur.
    /// </summary>
    public static class RateLimiters
    {
        // Login attempts: 5 attempts per 15 minutes
        private static readonly Lazy<RateLimiter> _loginLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(5, TimeSpan.FromMinutes(15)));

        // Message sending: 30 messages per minute
        private static readonly Lazy<RateLimiter> _messageLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(30, TimeSpan.FromMinutes(1)));

        // Product creation: 10 products per hour
        private static readonly Lazy<RateLimiter> _productCreationLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(10, TimeSpan.FromHours(1)));

        // API calls: 100 requests per minute
        private static readonly Lazy<RateLimiter> _apiCallLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(100, TimeSpan.FromMinutes(1)));

        // Image uploads: 20 uploads per 10 minutes
        private static readonly Lazy<RateLimiter> _imageUploadLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(20, TimeSpan.FromMinutes(10)));

        // Password reset: 3 attempts per hour
        private static readonly Lazy<RateLimiter> _passwordResetLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(3, TimeSpan.FromHours(1)));

        // Search queries: 60 searches per minute
        private static readonly Lazy<RateLimiter> _searchLimiter = 
            new Lazy<RateLimiter>(() => new RateLimiter(60, TimeSpan.FromMinutes(1)));

        public static RateLimiter Login => _loginLimiter.Value;
        public static RateLimiter Message => _messageLimiter.Value;
        public static RateLimiter ProductCreation => _productCreationLimiter.Value;
        public static RateLimiter ApiCall => _apiCallLimiter.Value;
        public static RateLimiter ImageUpload => _imageUploadLimiter.Value;
        public static RateLimiter PasswordReset => _passwordResetLimiter.Value;
        public static RateLimiter Search => _searchLimiter.Value;
    }

    /// <summary>
    /// ⚠️ DEPRECATED: Eski extension method. CheckRequest() kullanın.
    /// Geriye dönük uyumluluk için tutulmuştur.
    /// </summary>
    public static class RateLimiterExtensions
    {
        public static RateLimitResult CheckLimit(this RateLimiter limiter, string identifier)
        {
            var isAllowed = limiter.IsRequestAllowed(identifier);
            var remaining = limiter.GetRemainingRequests(identifier);
            var resetTime = limiter.GetResetTime(identifier);

            return isAllowed 
                ? RateLimitResult.Allowed(remaining, resetTime)
                : RateLimitResult.Denied(resetTime);
        }
    }

    /// <summary>
    /// ⚠️ DEPRECATED: RateLimitException artık kullanılmıyor.
    /// Geriye dönük uyumluluk için tutulmuştur.
    /// </summary>
    public class RateLimitException : Exception
    {
        public override string Message { get; }
        public int RetryAfterSeconds { get; set; }

        public RateLimitException(string message) : base(message)
        {
            Message = message;
        }

        public RateLimitException(string message, int retryAfterSeconds) : base(message)
        {
            Message = message;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }
}
