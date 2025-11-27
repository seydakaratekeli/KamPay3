// KamPay/Services/ProductCacheService.cs
using System.Collections.Concurrent;
using KamPay.Models;

namespace KamPay.Services
{
    public interface IProductCacheService
    {
        Task<List<Product>> GetCachedProductsAsync(bool forceRefresh = false);
        Task InvalidateCacheAsync();
        Task UpdateProductInCacheAsync(Product product);
        Task RemoveProductFromCacheAsync(string productId);
        bool IsCacheValid { get; }
    }

    public class ProductCacheService : IProductCacheService
    {
        private readonly ConcurrentDictionary<string, Product> _cache = new();
        private DateTime? _lastCacheTime;
        private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromMinutes(5);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        public bool IsCacheValid =>
            _lastCacheTime.HasValue &&
            DateTime.UtcNow - _lastCacheTime.Value < _cacheValidityPeriod;

        public async Task<List<Product>> GetCachedProductsAsync(bool forceRefresh = false)
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (forceRefresh || !IsCacheValid)
                {
                    return null; // Firebase'den çekilmeli
                }

                return _cache.Values.ToList();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task SetCacheAsync(List<Product> products)
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cache.Clear();
                foreach (var product in products)
                {
                    _cache[product.ProductId] = product;
                }
                _lastCacheTime = DateTime.UtcNow;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task UpdateProductInCacheAsync(Product product)
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cache[product.ProductId] = product;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task RemoveProductFromCacheAsync(string productId)
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cache.TryRemove(productId, out _);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task InvalidateCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cache.Clear();
                _lastCacheTime = null;
            }
            finally
            {
                _cacheLock.Release();
            }
        }
    }
}