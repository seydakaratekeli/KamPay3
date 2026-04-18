using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;

namespace KamPay.Security
{
    /// <summary>
    /// ?? Firebase tabanlı Güvenlik Denetim Servisi
    /// OWASP Best Practices: Logging, Monitoring, Account Lockout
    /// </summary>
    public class FirebaseSecurityAuditService : ISecurityAuditService
    {
        private readonly FirebaseClient _firebaseClient;
        
        // ?? Güvenlik Politikaları (Production ortamı için optimize edilmiş)
        private const int MAX_FAILED_ATTEMPTS_BEFORE_LOCK = 3;  // ? Daha sıkı: 3 başarısız deneme
        private const int FAILED_ATTEMPTS_WINDOW_MINUTES = 30;  // ? 30 dakikalık pencere
        private const int ACCOUNT_LOCK_DURATION_MINUTES = 60;   // ? 1 saat kilitleme
        private const int CRITICAL_FAILED_ATTEMPTS = 5;         // ? 5 başarısız deneme = kritik
        private const int SUSPICIOUS_IP_THRESHOLD = 10;         // ? IP başına 10 başarısız = şüpheli

        public FirebaseSecurityAuditService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        }

        #region Logging Methods

        public async Task LogFailedLoginAttemptAsync(string email, string? ipAddress, string? deviceInfo)
        {
            try
            {
                var log = new SecurityAuditLog
                {
                    Email = email?.ToLower(),
                    IpAddress = ipAddress,
                    DeviceInfo = deviceInfo,
                    EventType = SecurityEventType.FailedLogin,
                    EventDetails = $"Başarısız giriş denemesi: {email}",
                    SuspicionLevel = SuspicionLevel.Low,
                    Timestamp = DateTime.UtcNow
                };

                await _firebaseClient
                    .Child("security_audit_logs")
                    .PostAsync(log);

                // ?? Otomatik şüpheli aktivite kontrolü
                var recentFails = await GetRecentFailedAttemptsAsync(email, hours: 1);
                if (recentFails.Count >= CRITICAL_FAILED_ATTEMPTS)
                {
                    await LogSuspiciousActivityAsync(
                        null, 
                        "Repeated Failed Logins", 
                        $"{recentFails.Count} başarısız giriş denemesi son 1 saatte",
                        SuspicionLevel.Critical
                    );
                }

                KamPay.Helpers.AppLogger.DebugLog($"?? Başarısız giriş kaydedildi: {email} (IP: {ipAddress})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? LogFailedLoginAttempt hatası: {ex.Message}");
            }
        }

        public async Task LogSuccessfulLoginAsync(string userId, string email, string? ipAddress, string? deviceInfo)
        {
            try
            {
                var log = new SecurityAuditLog
                {
                    UserId = userId,
                    Email = email?.ToLower(),
                    IpAddress = ipAddress,
                    DeviceInfo = deviceInfo,
                    EventType = SecurityEventType.SuccessfulLogin,
                    EventDetails = $"Başarılı giriş: {email}",
                    SuspicionLevel = SuspicionLevel.Low,
                    Timestamp = DateTime.UtcNow
                };

                await _firebaseClient
                    .Child("security_audit_logs")
                    .PostAsync(log);

                // ?? Başarılı girişten sonra başarısız deneme sayacını sıfırla
                await ResetFailedAttemptsAsync(email);

                KamPay.Helpers.AppLogger.DebugLog($"? Başarılı giriş kaydedildi: {email}");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? LogSuccessfulLogin hatası: {ex.Message}");
            }
        }

        public async Task LogSuspiciousActivityAsync(string? userId, string activityType, string details, SuspicionLevel level)
        {
            try
            {
                var log = new SecurityAuditLog
                {
                    UserId = userId,
                    EventType = SecurityEventType.SuspiciousActivity,
                    EventDetails = $"{activityType}: {details}",
                    SuspicionLevel = level,
                    Timestamp = DateTime.UtcNow
                };

                await _firebaseClient
                    .Child("security_audit_logs")
                    .PostAsync(log);

                KamPay.Helpers.AppLogger.DebugLog($"?? Şüpheli aktivite kaydedildi: {activityType} (Seviye: {level})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? LogSuspiciousActivity hatası: {ex.Message}");
            }
        }

        #endregion

        #region Query Methods

        public async Task<List<SecurityAuditLog>> GetRecentFailedAttemptsAsync(string email, int hours = 24)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);
                
                var logs = await _firebaseClient
                    .Child("security_audit_logs")
                    .OrderBy("Email")
                    .EqualTo(email.ToLower())
                    .OnceAsync<SecurityAuditLog>();

                return logs
                    .Select(x => x.Object)
                    .Where(log => 
                        log.EventType == SecurityEventType.FailedLogin && 
                        log.Timestamp >= cutoffTime)
                    .OrderByDescending(log => log.Timestamp)
                    .ToList();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? GetRecentFailedAttempts hatası: {ex.Message}");
                return new List<SecurityAuditLog>();
            }
        }

        public async Task<bool> IsIpAddressSuspiciousAsync(string ipAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                    return false;

                var cutoffTime = DateTime.UtcNow.AddHours(-24);
                
                var logs = await _firebaseClient
                    .Child("security_audit_logs")
                    .OrderBy("IpAddress")
                    .EqualTo(ipAddress)
                    .OnceAsync<SecurityAuditLog>();

                var recentFailures = logs
                    .Select(x => x.Object)
                    .Where(log => 
                        log.EventType == SecurityEventType.FailedLogin && 
                        log.Timestamp >= cutoffTime)
                    .Count();

                return recentFailures >= SUSPICIOUS_IP_THRESHOLD;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? IsIpAddressSuspicious hatası: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Account Lockout Management

        public async Task<(bool ShouldLock, TimeSpan LockDuration, string Reason)> ShouldLockAccountAsync(string email)
        {
            try
            {
                // Önceden kilitli mi kontrol et
                var lockInfo = await GetAccountLockInfoAsync(email);
                if (lockInfo.IsLocked)
                {
                    var remaining = lockInfo.UnlockAt.HasValue 
                        ? lockInfo.UnlockAt.Value - DateTime.UtcNow 
                        : TimeSpan.Zero;
                    
                    return (true, remaining, lockInfo.Reason);
                }

                // Son X dakikadaki başarısız denemeleri kontrol et
                var cutoffTime = DateTime.UtcNow.AddMinutes(-FAILED_ATTEMPTS_WINDOW_MINUTES);
                var recentFails = await GetRecentFailedAttemptsAsync(email, hours: 1);
                var failsInWindow = recentFails.Count(f => f.Timestamp >= cutoffTime);

                if (failsInWindow >= MAX_FAILED_ATTEMPTS_BEFORE_LOCK)
                {
                    var lockDuration = TimeSpan.FromMinutes(ACCOUNT_LOCK_DURATION_MINUTES);
                    var reason = $"{failsInWindow} başarısız giriş denemesi ({FAILED_ATTEMPTS_WINDOW_MINUTES} dakikada)";
                    return (true, lockDuration, reason);
                }

                return (false, TimeSpan.Zero, string.Empty);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? ShouldLockAccount hatası: {ex.Message}");
                return (false, TimeSpan.Zero, string.Empty);
            }
        }

        public async Task LockAccountTemporarilyAsync(string email, TimeSpan duration, string reason)
        {
            try
            {
                var lockInfo = new AccountLockInfo
                {
                    Email = email.ToLower(),
                    IsLocked = true,
                    LockedAt = DateTime.UtcNow,
                    UnlockAt = DateTime.UtcNow.Add(duration),
                    Reason = reason,
                    FailedAttempts = (await GetRecentFailedAttemptsAsync(email, hours: 1)).Count
                };

                // Firebase'e kaydet
                await _firebaseClient
                    .Child("account_locks")
                    .Child(GetSafeKey(email))
                    .PutAsync(lockInfo);

                // Log olarak kaydet
                await LogSuspiciousActivityAsync(
                    null,
                    "Account Locked",
                    $"Hesap kilitlendi: {email} ({duration.TotalMinutes} dakika) - {reason}",
                    SuspicionLevel.High
                );

                KamPay.Helpers.AppLogger.DebugLog($"?? Hesap kilitlendi: {email} ({duration.TotalMinutes} dakika)");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? LockAccountTemporarily hatası: {ex.Message}");
            }
        }

        public async Task UnlockAccountAsync(string email)
        {
            try
            {
                await _firebaseClient
                    .Child("account_locks")
                    .Child(GetSafeKey(email))
                    .DeleteAsync();

                KamPay.Helpers.AppLogger.DebugLog($"? Hesap kilidi kaldırıldı: {email}");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? UnlockAccount hatası: {ex.Message}");
            }
        }

        public async Task<(bool IsLocked, DateTime? UnlockTime, string? Reason)> IsAccountLockedAsync(string email)
        {
            try
            {
                var lockInfo = await GetAccountLockInfoAsync(email);

                if (!lockInfo.IsLocked)
                    return (false, null, null);

                // Kilitleme süresi dolmuş mu kontrol et
                if (lockInfo.UnlockAt.HasValue && DateTime.UtcNow >= lockInfo.UnlockAt.Value)
                {
                    // Otomatik kilidi kaldır
                    await UnlockAccountAsync(email);
                    return (false, null, null);
                }

                return (true, lockInfo.UnlockAt, lockInfo.Reason);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? IsAccountLocked hatası: {ex.Message}");
                return (false, null, null);
            }
        }

        private async Task<AccountLockInfo> GetAccountLockInfoAsync(string email)
        {
            try
            {
                var lockInfo = await _firebaseClient
                    .Child("account_locks")
                    .Child(GetSafeKey(email))
                    .OnceSingleAsync<AccountLockInfo>();

                return lockInfo ?? new AccountLockInfo { Email = email.ToLower(), IsLocked = false };
            }
            catch
            {
                return new AccountLockInfo { Email = email.ToLower(), IsLocked = false };
            }
        }

        private async Task ResetFailedAttemptsAsync(string email)
        {
            try
            {
                // Eski başarısız deneme kayıtlarını "resolved" olarak işaretle
                var recentFails = await GetRecentFailedAttemptsAsync(email, hours: 24);
                
                foreach (var log in recentFails)
                {
                    log.IsResolved = true;
                }

                KamPay.Helpers.AppLogger.DebugLog($"? Başarısız deneme sayacı sıfırlandı: {email}");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"? ResetFailedAttempts hatası: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// E-posta adresini Firebase key olarak kullanmak için güvenli hale getirir
        /// Firebase keys cannot contain: . $ # [ ] /
        /// </summary>
        private string GetSafeKey(string email)
        {
            return email
                .Replace(".", "_dot_")
                .Replace("$", "_dollar_")
                .Replace("#", "_hash_")
                .Replace("[", "_lb_")
                .Replace("]", "_rb_")
                .Replace("/", "_slash_");
        }

        #endregion
    }
}

