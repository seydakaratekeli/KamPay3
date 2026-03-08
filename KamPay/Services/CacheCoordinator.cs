using KamPay.Models;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? Cache koordinatörü implementasyonu
/// ? Single Responsibility: Sadece cache strateji orkestrasyon
/// ? Delegates to: IProductCacheService
/// </summary>
public class CacheCoordinator : ICacheCoordinator
{
    private readonly IProductCacheService _cacheService;
    
    // In-memory cache statistics
    private static int _hitCount = 0;
    private static int _missCount = 0;
    private static DateTime _lastClearTime = DateTime.UtcNow;

    // Cache expiry defaults
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ShortExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongExpiry = TimeSpan.FromHours(1);

    public CacheCoordinator(IProductCacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<ServiceResult<T>> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        try
        {
            Debug.WriteLine($"?? Cache lookup: {key}");

            // 1. Önce cache'den kontrol et
            // Not: Generic cache için IProductCacheService'i geniþletmek gerekir
            // Þimdilik sadece Product cache'i desteklendiði için type kontrolü yapalým
            
            if (typeof(T) == typeof(List<Product>))
            {
                if (_cacheService.IsCacheValid)
                {
                    var cachedProducts = await _cacheService.GetCachedProductsAsync();
                    if (cachedProducts != null && cachedProducts.Any())
                    {
                        _hitCount++;
                        Debug.WriteLine($"? Cache HIT: {key}");
                        return ServiceResult<T>.SuccessResult((T)(object)cachedProducts, "Cache'den alýndý");
                    }
                }
            }

            // 2. Cache'de yok, factory ile oluþtur
            _missCount++;
            Debug.WriteLine($"?? Cache MISS: {key}, factory çalýþtýrýlýyor");
            
            var data = await factory();
            
            // 3. Yeni veriyi cache'e kaydet
            if (typeof(T) == typeof(List<Product>) && data is List<Product> products)
            {
                await _cacheService.SetCacheAsync(products);
                Debug.WriteLine($"? Cache'e kaydedildi: {key} ({products.Count} ürün)");
            }

            return ServiceResult<T>.SuccessResult(data, "Veri oluþturuldu ve cache'e alýndý");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetOrSetAsync hatasý: {ex.Message}");
            return ServiceResult<T>.FailureResult("Cache iþlemi baþarýsýz", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> InvalidateAsync(string key)
    {
        try
        {
            Debug.WriteLine($"??? Cache invalidate: {key}");

            // Product cache'ini temizle
            await _cacheService.InvalidateCacheAsync();
            
            Debug.WriteLine($"? Cache temizlendi: {key}");
            return ServiceResult<bool>.SuccessResult(true, "Cache temizlendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? InvalidateAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Cache temizlenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> InvalidatePatternAsync(string pattern)
    {
        try
        {
            Debug.WriteLine($"??? Cache pattern invalidate: {pattern}");

            // Pattern'e göre cache temizleme (geniþletilebilir)
            if (pattern.Contains("product", StringComparison.OrdinalIgnoreCase))
            {
                await _cacheService.InvalidateCacheAsync();
                Debug.WriteLine($"? Product cache temizlendi");
            }

            return ServiceResult<bool>.SuccessResult(true, "Pattern bazlý cache temizlendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? InvalidatePatternAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Pattern cache temizlenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<CacheStats>> GetCacheStatsAsync()
    {
        try
        {
            Debug.WriteLine($"?? Cache istatistikleri alýnýyor");

            var stats = new CacheStats
            {
                TotalKeys = 1, // Sadece product cache var þimdilik
                TotalSizeBytes = 0, // TODO: Hesaplanabilir
                HitCount = _hitCount,
                MissCount = _missCount,
                LastClearTime = _lastClearTime
            };

            Debug.WriteLine($"?? Hit Rate: {stats.HitRate:F2}% ({stats.HitCount}/{stats.TotalRequests})");
            
            await Task.CompletedTask;
            return ServiceResult<CacheStats>.SuccessResult(stats, "Ýstatistikler alýndý");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? GetCacheStatsAsync hatasý: {ex.Message}");
            return ServiceResult<CacheStats>.FailureResult("Ýstatistikler alýnamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> ClearAllCacheAsync()
    {
        try
        {
            Debug.WriteLine($"??? Tüm cache temizleniyor");

            await _cacheService.InvalidateCacheAsync();
            
            // Ýstatistikleri sýfýrla
            _hitCount = 0;
            _missCount = 0;
            _lastClearTime = DateTime.UtcNow;

            Debug.WriteLine($"? Tüm cache temizlendi");
            return ServiceResult<bool>.SuccessResult(true, "Tüm cache temizlendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? ClearAllCacheAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Cache temizlenemedi", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> OptimizeCacheAsync()
    {
        try
        {
            Debug.WriteLine($"?? Cache optimize ediliyor");

            // 1. Eski cache'i temizle
            if (!_cacheService.IsCacheValid)
            {
                await _cacheService.InvalidateCacheAsync();
                Debug.WriteLine($"? Eski cache temizlendi");
                return ServiceResult<int>.SuccessResult(1, "Eski cache temizlendi");
            }

            // 2. TODO: Boyut kontrolü yapýlabilir
            // 3. TODO: Sýk kullanýlmayan veriler temizlenebilir

            Debug.WriteLine($"? Cache optimizasyonu tamamlandý");
            return ServiceResult<int>.SuccessResult(0, "Optimizasyon gerekmedi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? OptimizeCacheAsync hatasý: {ex.Message}");
            return ServiceResult<int>.FailureResult("Cache optimize edilemedi", ex.Message);
        }
    }
}
