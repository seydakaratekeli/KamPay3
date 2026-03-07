using System;
using System.Collections.Concurrent;
using System.Linq;

namespace KamPay.Helpers
{
    /// <summary>
    /// ?? Geliþmiþ Hýz Sýnýrlayýcý - IP ve Kullanýcý Bazlý Koruma
    /// OWASP Best Practices: Rate Limiting, Brute Force Protection
    /// </summary>
    public class AdvancedRateLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestLog;
        private readonly ConcurrentDictionary<string, int> _violationCount;
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;
        private readonly int _maxViolationsBeforeBan;
        private readonly TimeSpan _banDuration;

        /// <summary>
        /// Geliþmiþ Rate Limiter Constructor
        /// </summary>
        /// <param name="maxRequests">Zaman penceresinde izin verilen maksimum istek</param>
        /// <param name="timeWindow">Zaman penceresi (örn: 15 dakika)</param>
        /// <param name="maxViolationsBeforeBan">Ban öncesi maksimum ihlal sayýsý</param>
        /// <param name="banDuration">Ban süresi</param>
        public AdvancedRateLimiter(
            int maxRequests, 
            TimeSpan timeWindow, 
            int maxViolationsBeforeBan = 3,
            TimeSpan? banDuration = null)
        {
            _maxRequests = maxRequests;
            _timeWindow = timeWindow;
            _maxViolationsBeforeBan = maxViolationsBeforeBan;
            _banDuration = banDuration ?? TimeSpan.FromHours(24);
            _requestLog = new ConcurrentDictionary<string, Queue<DateTime>>();
            _violationCount = new ConcurrentDictionary<string, int>();
        }

        /// <summary>
        /// Ýsteðin izin verilip verilmediðini kontrol eder ve ihlal durumunda ban uygular
        /// </summary>
        public RateLimitResult CheckRequest(string identifier, string? ipAddress = null)
        {
            // ?? IP tabanlý ek kontrol
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                var ipResult = CheckIdentifier(ipAddress);
                if (!ipResult.IsAllowed)
                {
                    return ipResult;
                }
            }

            // Kullanýcý tabanlý kontrol
            return CheckIdentifier(identifier);
        }

        private RateLimitResult CheckIdentifier(string identifier)
        {
            var now = DateTime.UtcNow;
            var requests = _requestLog.GetOrAdd(identifier, _ => new Queue<DateTime>());

            lock (requests)
            {
                // Zaman penceresinin dýþýndaki istekleri temizle
                while (requests.Count > 0 && (now - requests.Peek()) > _timeWindow)
                {
                    requests.Dequeue();
                }

                // Ýhlal sayýsýný kontrol et (ban durumu)
                var violations = _violationCount.GetOrAdd(identifier, 0);
                if (violations >= _maxViolationsBeforeBan)
                {
                    var message = $"?? Hesabýnýz geçici olarak engellenmiþtir. " +
                                  $"{_banDuration.TotalHours} saat sonra tekrar deneyin.";
                    
                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        ResetTime = now.Add(_banDuration),
                        Message = message,
                        IsBanned = true,
                        ViolationCount = violations
                    };
                }

                // Limiti aþmýþ mý?
                if (requests.Count >= _maxRequests)
                {
                    // Ýhlal sayýsýný artýr
                    _violationCount.AddOrUpdate(identifier, 1, (key, oldValue) => oldValue + 1);
                    violations = _violationCount[identifier];

                    var timeUntilReset = _timeWindow - (now - requests.Peek());
                    var message = violations >= _maxViolationsBeforeBan - 1
                        ? $"?? SON UYARI: Bir daha denemeniz halinde hesabýnýz {_banDuration.TotalHours} saat engellenecektir!"
                        : GetDeniedMessage(timeUntilReset);

                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        ResetTime = requests.Peek().Add(_timeWindow),
                        Message = message,
                        ViolationCount = violations
                    };
                }

                // Ýstek izin verildi
                requests.Enqueue(now);
                
                // Baþarýlý istek sonrasý ihlal sayacýný sýfýrla
                if (violations > 0)
                {
                    _violationCount.TryUpdate(identifier, 0, violations);
                }

                return new RateLimitResult
                {
                    IsAllowed = true,
                    RemainingRequests = Math.Max(0, _maxRequests - requests.Count),
                    ResetTime = requests.Peek().Add(_timeWindow)
                };
            }
        }

        /// <summary>
        /// Ban durumunu manuel olarak temizler (admin kullanýmý için)
        /// </summary>
        public void RemoveBan(string identifier)
        {
            _violationCount.TryRemove(identifier, out _);
            _requestLog.TryRemove(identifier, out _);
        }

        /// <summary>
        /// Tüm kayýtlarý temizler
        /// </summary>
        public void ResetAll()
        {
            _requestLog.Clear();
            _violationCount.Clear();
        }

        private string GetDeniedMessage(TimeSpan timeUntilReset)
        {
            if (timeUntilReset.TotalMinutes >= 1)
            {
                return $"? Çok fazla deneme yaptýnýz. " +
                       $"{Math.Ceiling(timeUntilReset.TotalMinutes)} dakika sonra tekrar deneyin.";
            }
            else
            {
                return $"? Çok fazla deneme yaptýnýz. " +
                       $"{Math.Ceiling(timeUntilReset.TotalSeconds)} saniye sonra tekrar deneyin.";
            }
        }
    }

    /// <summary>
    /// ?? Önceden Yapýlandýrýlmýþ Geliþmiþ Rate Limiter'lar
    /// PRODUCTION ortamý için sýkýlaþtýrýlmýþ güvenlik politikalarý
    /// </summary>
    public static class SecureRateLimiters
    {
        // ?? GÝRÝÞ: 3 deneme / 15 dakika (ban: 3 ihlal sonrasý 24 saat)
        private static readonly Lazy<AdvancedRateLimiter> _loginLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 3,                           // ? 5'ten 3'e düþürüldü
                timeWindow: TimeSpan.FromMinutes(15),
                maxViolationsBeforeBan: 3,                // 3 ihlal sonrasý ban
                banDuration: TimeSpan.FromHours(24)       // 24 saat ban
            ));

        // ?? ÞÝFRE SIFIRLAMA: 2 deneme / saat (ban: 3 ihlal sonrasý 12 saat)
        private static readonly Lazy<AdvancedRateLimiter> _passwordResetLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 2,                           // ? 3'ten 2'ye düþürüldü
                timeWindow: TimeSpan.FromHours(1),
                maxViolationsBeforeBan: 3,
                banDuration: TimeSpan.FromHours(12)
            ));

        // ?? E-POSTA DOÐRULAMA: 3 deneme / 10 dakika
        private static readonly Lazy<AdvancedRateLimiter> _emailVerificationLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 3,
                timeWindow: TimeSpan.FromMinutes(10),
                maxViolationsBeforeBan: 5,
                banDuration: TimeSpan.FromHours(6)
            ));

        // ?? ÜRÜN OLUÞTURMA: 5 ürün / saat (spam önleme)
        private static readonly Lazy<AdvancedRateLimiter> _productCreationLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 5,                           // ? 10'dan 5'e düþürüldü
                timeWindow: TimeSpan.FromHours(1),
                maxViolationsBeforeBan: 2,
                banDuration: TimeSpan.FromHours(24)
            ));

        // ?? MESAJLAÞMA: 20 mesaj / dakika (spam önleme)
        private static readonly Lazy<AdvancedRateLimiter> _messageLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 20,                          // ? 30'dan 20'ye düþürüldü
                timeWindow: TimeSpan.FromMinutes(1),
                maxViolationsBeforeBan: 3,
                banDuration: TimeSpan.FromHours(1)
            ));

        // ?? RESÝM YÜKLEME: 10 resim / 10 dakika
        private static readonly Lazy<AdvancedRateLimiter> _imageUploadLimiter = 
            new Lazy<AdvancedRateLimiter>(() => new AdvancedRateLimiter(
                maxRequests: 10,                          // ? 20'den 10'a düþürüldü
                timeWindow: TimeSpan.FromMinutes(10),
                maxViolationsBeforeBan: 3,
                banDuration: TimeSpan.FromHours(6)
            ));

        public static AdvancedRateLimiter Login => _loginLimiter.Value;
        public static AdvancedRateLimiter PasswordReset => _passwordResetLimiter.Value;
        public static AdvancedRateLimiter EmailVerification => _emailVerificationLimiter.Value;
        public static AdvancedRateLimiter ProductCreation => _productCreationLimiter.Value;
        public static AdvancedRateLimiter Message => _messageLimiter.Value;
        public static AdvancedRateLimiter ImageUpload => _imageUploadLimiter.Value;
    }

    /// <summary>
    /// ?? Geliþmiþ Rate Limit Sonucu (ban desteði ile)
    /// </summary>
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime? ResetTime { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsBanned { get; set; }
        public int ViolationCount { get; set; }

        /// <summary>
        /// ? Factory method: Ýsteðe izin verildiðinde
        /// </summary>
        public static RateLimitResult Allowed(int remaining, DateTime? resetTime)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = remaining,
                ResetTime = resetTime,
                Message = string.Empty,
                IsBanned = false,
                ViolationCount = 0
            };
        }

        /// <summary>
        /// ?? Factory method: Ýstek reddedildiðinde
        /// </summary>
        public static RateLimitResult Denied(DateTime? resetTime)
        {
            var timeUntilReset = resetTime.HasValue 
                ? resetTime.Value - DateTime.UtcNow 
                : TimeSpan.Zero;

            var message = timeUntilReset.TotalMinutes > 1
                ? $"? Çok fazla deneme yaptýnýz. {Math.Ceiling(timeUntilReset.TotalMinutes)} dakika sonra tekrar deneyin."
                : $"? Çok fazla deneme yaptýnýz. {Math.Ceiling(timeUntilReset.TotalSeconds)} saniye sonra tekrar deneyin.";

            return new RateLimitResult
            {
                IsAllowed = false,
                RemainingRequests = 0,
                ResetTime = resetTime,
                Message = message,
                IsBanned = false,
                ViolationCount = 0
            };
        }
    }
}
