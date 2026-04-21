using Firebase.Database;
using Firebase.Database.Query;
using KamPay.API.Models;

namespace KamPay.API.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly FirebaseClient _firebaseClient;

        public ProductRepository(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        public async Task<List<Product>> GetAllAsync(int limit = 50)
        {
            var urunler = await _firebaseClient
                .Child("products")
                .OrderByKey()
                .LimitToLast(limit)
                .OnceAsync<Product>();

            return urunler.Select(x =>
            {
                var urun = x.Object;
                urun.ProductId = x.Key;
                return urun;
            }).ToList();
        }

        /// <summary>
        /// Cursor-based sayfalama: cursor varsa o key'den sonraki kayıtları getirir (exclusive).
        /// Filtreler (kategori, tip, arama) Firebase'in tek-field sorgu sınırı nedeniyle
        /// alınan batch üzerinde client-side uygulanır; yine de tüm koleksiyonu yüklemez.
        /// </summary>
        public async Task<PagedResult<Product>> GetPagedAsync(int pageSize, string? cursor, ProductQueryOptions? options = null)
        {
            // Filtreli sorgu için fazladan veri çekiyoruz (overfetch), sonra filtreleriz.
            // Maksimum ne kadar çekeceğimizi belirle.
            int fetchLimit = string.IsNullOrEmpty(options?.CategoryId) &&
                             options?.Type == null &&
                             string.IsNullOrEmpty(options?.Search)
                ? pageSize + 1
                : pageSize * 4;

            IFirebaseQuery query;

            if (!string.IsNullOrEmpty(cursor))
                query = _firebaseClient.Child("products").OrderByKey().StartAt(cursor + "\0").LimitToFirst(fetchLimit);

            else
                query = _firebaseClient.Child("products").OrderByKey().LimitToFirst(fetchLimit);

            var raw = await query.OnceAsync<Product>();

            var products = raw
                .Select(x => { x.Object.ProductId = x.Key; return x.Object; })
                .Where(p => p.IsActive && !p.IsSold)
                .ToList();

            

            // Client-side filtreler
            if (options != null)
            {
                if (!string.IsNullOrEmpty(options.CategoryId))
                    products = products.Where(p => p.CategoryId == options.CategoryId).ToList();

                if (options.Type.HasValue)
                    products = products.Where(p => p.Type == options.Type.Value).ToList();

                if (!string.IsNullOrEmpty(options.Search))
                    products = products
                        .Where(p => p.Title.Contains(options.Search, StringComparison.OrdinalIgnoreCase)
                                 || p.Description.Contains(options.Search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
            }

            var page = products.Take(pageSize).ToList();
            string? nextCursor = page.Count == pageSize ? page.Last().ProductId : null;

            return new PagedResult<Product>
            {
                Items = page,
                NextCursor = nextCursor,
                HasMore = nextCursor != null
            };
        }

        /// <summary>
        /// Firebase OrderBy("UserId") ile doğrudan filtreli sorgu — 1000 yükleme anti-pattern kaldırıldı.
        /// Not: Firebase RTDB'de UserId alanına index tanımlanmalı (rules.json).
        /// </summary>
        public async Task<List<Product>> GetByUserIdAsync(string userId)
        {
            var items = await _firebaseClient
                .Child("products")
                .OrderBy("UserId")
                .EqualTo(userId)
                .OnceAsync<Product>();

            return items
                .Select(x => { x.Object.ProductId = x.Key; return x.Object; })
                .ToList();
        }

        public async Task<Product?> GetByIdAsync(string id)
        {
            var product = await _firebaseClient.Child("products").Child(id).OnceSingleAsync<Product>();
            if (product != null)
                product.ProductId = id;
            return product;
        }

        public async Task<string> AddAsync(Product product)
        {
            var response = await _firebaseClient.Child("products").PostAsync(product);
            return response.Key;
        }

        public async Task UpdateAsync(string id, Product product)
        {
            await _firebaseClient.Child("products").Child(id).PutAsync(product);
        }

        public async Task DeleteAsync(string id)
        {
            await _firebaseClient.Child("products").Child(id).DeleteAsync();
        }
    }
}
