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

        public async Task<Product?> GetByIdAsync(string id)
        {
            var product = await _firebaseClient.Child("products").Child(id).OnceSingleAsync<Product>();
            if (product != null)
            {
                product.ProductId = id;
            }
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
