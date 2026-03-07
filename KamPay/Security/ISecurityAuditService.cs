using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Security
{
    /// <summary>
    /// ?? Güvenlik Denetim Servisi - Þüpheli aktiviteleri izler ve loglama yapar
    /// OWASP Top 10 uyumlu: A09:2021 – Security Logging and Monitoring Failures
    /// </summary>
    public interface ISecurityAuditService
    {
        /// <summary>
        /// Baþarýsýz giriþ denemesini kaydeder
        /// </summary>
        Task LogFailedLoginAttemptAsync(string email, string? ipAddress, string? deviceInfo);
        
        /// <summary>
        /// Baþarýlý giriþ iþlemini kaydeder
        /// </summary>
        Task LogSuccessfulLoginAsync(string userId, string email, string? ipAddress, string? deviceInfo);
        
        /// <summary>
        /// Þüpheli aktiviteyi kaydeder (çoklu baþarýsýz denemeler, hýzlý istekler vb.)
        /// </summary>
        Task LogSuspiciousActivityAsync(string userId, string activityType, string details, SuspicionLevel level);
        
        /// <summary>
        /// Belirli bir kullanýcý için son baþarýsýz giriþ denemelerini getirir
        /// </summary>
        Task<List<SecurityAuditLog>> GetRecentFailedAttemptsAsync(string email, int hours = 24);
        
        /// <summary>
        /// IP adresi bazýnda þüpheli aktivite kontrolü yapar
        /// </summary>
        Task<bool> IsIpAddressSuspiciousAsync(string ipAddress);
        
        /// <summary>
        /// Kullanýcý hesabýnýn geçici olarak kilitlenmesi gerekip gerekmediðini kontrol eder
        /// </summary>
        Task<(bool ShouldLock, TimeSpan LockDuration, string Reason)> ShouldLockAccountAsync(string email);
        
        /// <summary>
        /// Hesabý geçici olarak kilitler
        /// </summary>
        Task LockAccountTemporarilyAsync(string email, TimeSpan duration, string reason);
        
        /// <summary>
        /// Hesap kilidi kaldýrýr
        /// </summary>
        Task UnlockAccountAsync(string email);
        
        /// <summary>
        /// Hesabýn kilitli olup olmadýðýný kontrol eder
        /// </summary>
        Task<(bool IsLocked, DateTime? UnlockTime, string? Reason)> IsAccountLockedAsync(string email);
    }

    /// <summary>
    /// Güvenlik denetim logu modeli
    /// </summary>
    public class SecurityAuditLog
    {
        public string LogId { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
        public SecurityEventType EventType { get; set; }
        public string EventDetails { get; set; } = string.Empty;
        public SuspicionLevel SuspicionLevel { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; }
    }

    /// <summary>
    /// Güvenlik olayý tipleri
    /// </summary>
    public enum SecurityEventType
    {
        FailedLogin = 0,
        SuccessfulLogin = 1,
        PasswordResetRequest = 2,
        AccountLocked = 3,
        AccountUnlocked = 4,
        RateLimitExceeded = 5,
        SuspiciousActivity = 6,
        InvalidToken = 7,
        UnauthorizedAccess = 8
    }

    /// <summary>
    /// Þüphe seviyesi
    /// </summary>
    public enum SuspicionLevel
    {
        Low = 0,      // Normal aktivite
        Medium = 1,   // Ýzlenmesi gereken aktivite
        High = 2,     // Þüpheli aktivite
        Critical = 3  // Acil müdahale gereken aktivite
    }

    /// <summary>
    /// Hesap kilidi bilgisi
    /// </summary>
    public class AccountLockInfo
    {
        public string Email { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? UnlockAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int FailedAttempts { get; set; }
        public DateTime? LastFailedAttempt { get; set; }
    }
}
