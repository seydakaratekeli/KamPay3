using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Önbellekleme stratejilerini yönetir
/// Single Responsibility: Sadece cache orkestrasyon ve strateji yönetimi
/// </summary>
public interface ICacheCoordinator
{
    /// <summary>
    /// Cache'den al veya factory ile oluþtur ve cache'e kaydet
    /// </summary>
    Task<ServiceResult<T>> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);

    /// <summary>
    /// Belirli bir anahtarý cache'den sil
    /// </summary>
    Task<ServiceResult<bool>> InvalidateAsync(string key);

    /// <summary>
    /// Pattern'e uyan tüm anahtarlarý cache'den sil
    /// </summary>
    Task<ServiceResult<bool>> InvalidatePatternAsync(string pattern);

    /// <summary>
    /// Cache istatistiklerini getir
    /// </summary>
    Task<ServiceResult<CacheStats>> GetCacheStatsAsync();

    /// <summary>
    /// Tüm cache'i temizle
    /// </summary>
    Task<ServiceResult<bool>> ClearAllCacheAsync();

    /// <summary>
    /// Cache boyutunu optimize et (eski verileri temizle)
    /// </summary>
    Task<ServiceResult<int>> OptimizeCacheAsync();
}

/// <summary>
/// Cache istatistikleri modeli
/// </summary>
public class CacheStats
{
    public int TotalKeys { get; set; }
    public long TotalSizeBytes { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests * 100 : 0;
    public int TotalRequests => HitCount + MissCount;
    public DateTime LastClearTime { get; set; }
}
