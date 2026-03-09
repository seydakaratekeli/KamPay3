using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    //bu sayfanın amacı Firebase Realtime Database'den kategori verilerini almak ve yönetmektir. nasıl alınacağı, önbelleğe alma ve başlangıç verilerinin eklenmesi gibi işlevleri kapsar.
    public class FirebaseCategoryService : ICategoryService
    {
        private readonly FirebaseClient _firebaseClient;
        private List<Category>? _categoriesCache;

        // ✅ SOLID FIX: FirebaseClient'ı DI'den alıyoruz (LSP ve DIP prensiplerine uygun)
        public FirebaseCategoryService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            System.Diagnostics.Debug.WriteLine("✅ FirebaseCategoryService oluşturuldu (DI ile)");
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync()
        {
            if (_categoriesCache != null && _categoriesCache.Any())
            {
                return _categoriesCache;
            }

            var categories = await _firebaseClient
                .Child("categories")
                .OnceAsync<Category>();

            if (categories == null || !categories.Any())
            {
                await SeedCategoriesAsync();
                categories = await _firebaseClient.Child("categories").OnceAsync<Category>();
            }

            _categoriesCache = categories.Select(item => 
            new Category
            {
                CategoryId = item.Key, // Firebase'in anahtarını ID olarak kullanıyoruz
                Name = item.Object.Name,
                IconName = item.Object.IconName, 
                Description = item.Object.Description
            }).ToList();

            return _categoriesCache;
        }

        private async Task SeedCategoriesAsync()
        {
            var initialCategories = Category.GetDefaultCategories();

            foreach (var category in initialCategories)
            {
                // CategoryId'yi burada oluşturmuyoruz, Firebase'in oluşturmasına izin veriyoruz.
                // Bu yüzden PostAsync kullanmak en doğrusu.
                await _firebaseClient.Child("categories").PostAsync(new
                {
                    category.Name,
                    category.IconName,
                    category.Description
                });
            }
        }
    }
}