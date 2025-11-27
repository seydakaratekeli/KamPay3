using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System.Linq;

namespace KamPay.Services;
public class FirebaseProductService : IProductService
{
    private readonly FirebaseClient _firebaseClient;
    private readonly IStorageService _storageService;

    public FirebaseProductService(IStorageService storageService)
    {
        _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        _storageService = storageService;
    }

    public async Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter filter = null)
    {
        try
        {
            var allProducts = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .OnceAsync<Product>();

            var productsQuery = allProducts.Select(p =>
            {
                var product = p.Object;
                product.ProductId = p.Key;
                return product;
            }).AsQueryable(); // Sorgulanabilir hale getiriyoruz

            // Filtreleme
            // Filtreleme
            if (filter != null)
            {
                // Sadece aktif ürünler
                if (filter.OnlyActive)
                {
                    productsQuery = productsQuery.Where(p => p.IsActive);
                }

                // 🔹 YENİ: Satılmış ürünleri göstermeyi AÇIK bırakıyoruz
                // Anasayfada "TAKAS YAPILDI" etiketiyle görünsünler
                // ❌ KALDIRILDI: ExcludeSold filtresi (zaten yorumlanmış)

                // SATILMIŞ ÜRÜNLER FİLTRESİNİ DEVRE DIŞI BIRAKTIK
                /*
                if (filter.ExcludeSold)
                {
                    productsQuery = productsQuery.Where(p => !p.IsSold);
                }
                */

                // Arama metni
                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    var searchLower = filter.SearchText.ToLower();
                    productsQuery = productsQuery.Where(p =>
                        p.Title.ToLower().Contains(searchLower) ||
                        p.Description.ToLower().Contains(searchLower)
                    );
                }

                // Kategori
                if (!string.IsNullOrWhiteSpace(filter.CategoryId))
                {
                    productsQuery = productsQuery.Where(p => p.CategoryId == filter.CategoryId);
                }

                // Tip
                if (filter.Type.HasValue)
                {
                    productsQuery = productsQuery.Where(p => p.Type == filter.Type.Value);
                }

                // Durum
                if (filter.Condition.HasValue)
                {
                    productsQuery = productsQuery.Where(p => p.Condition == filter.Condition.Value);
                }

                // Fiyat aralığı
                if (filter.MinPrice.HasValue)
                {
                    productsQuery = productsQuery.Where(p => p.Price >= filter.MinPrice.Value);
                }
                if (filter.MaxPrice.HasValue)
                {
                    productsQuery = productsQuery.Where(p => p.Price <= filter.MaxPrice.Value);
                }

                // Konum
                if (!string.IsNullOrWhiteSpace(filter.Location))
                {
                    var locationLower = filter.Location.ToLower();
                    productsQuery = productsQuery.Where(p =>
                        p.Location != null && p.Location.ToLower().Contains(locationLower)
                    );
                }

                // Sıralama
                productsQuery = filter.SortBy switch
                {
                    ProductSortOption.Newest => productsQuery.OrderByDescending(p => p.CreatedAt),
                    ProductSortOption.Oldest => productsQuery.OrderBy(p => p.CreatedAt),
                    ProductSortOption.PriceAsc => productsQuery.OrderBy(p => p.Price),
                    ProductSortOption.PriceDesc => productsQuery.OrderByDescending(p => p.Price),
                    ProductSortOption.MostViewed => productsQuery.OrderByDescending(p => p.ViewCount),
                    ProductSortOption.MostFavorited => productsQuery.OrderByDescending(p => p.FavoriteCount),
                    _ => productsQuery.OrderByDescending(p => p.CreatedAt)
                };
            }
            else
            {
                // Filtre yoksa varsayılan sıralama
                productsQuery = productsQuery.OrderByDescending(p => p.CreatedAt);
            }

            var products = productsQuery.ToList();
            return ServiceResult<List<Product>>.SuccessResult(products);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", ex.Message);
        }
    }

    // ... BU DOSYADAKİ DİĞER TÜM METOTLARINIZ (AddProductAsync, UpdateProductAsync vb.) AYNI KALACAK ...
    #region Diğer Metotlar
    public async Task<ServiceResult<Product>> AddProductAsync(ProductRequest request, User currentUser)
    {
        try
        {
            var validation = ValidateProduct(request);
            if (!validation.IsValid)
            {
                return ServiceResult<Product>.FailureResult("Ürün bilgileri geçersiz", validation.Errors.ToArray());
            }
            // Kategori adını önceden alalım
            var categories = await GetCategoriesAsync();
            var categoryName = categories.Data?.FirstOrDefault(c => c.CategoryId == request.CategoryId)?.Name ?? "Bilinmeyen";

            var product = new Product
            {
                ProductId = Guid.NewGuid().ToString(), // ID'yi burada oluşturmak daha güvenli
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                CategoryId = request.CategoryId,
                CategoryName = categoryName,
                Condition = request.Condition,
                Type = request.Type,
                Price = request.Price,
                Location = request.Location?.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                UserId = currentUser.UserId,
                UserName = currentUser.FullName,
                UserEmail = currentUser.Email,
                UserPhotoUrl = currentUser.ProfileImageUrl,
                ExchangePreference = request.ExchangePreference?.Trim(),
                // --- EKSİK SATIRI BURAYA EKLEYİN ---
                IsForSurpriseBox = request.IsForSurpriseBox,

                // Durum bilgilerini ayarlıyoruz
                IsActive = true,
                IsSold = false,
                IsReserved = false,
                CreatedAt = DateTime.UtcNow
            };



            // --- RESİM YÜKLEME KODU ---
            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var imageUrls = new List<string>();
                for (int i = 0; i < Math.Min(request.ImagePaths.Count, Constants.MaxProductImages); i++)
                {
                    var uploadResult = await _storageService.UploadProductImageAsync(request.ImagePaths[i], product.ProductId, i);
                    if (uploadResult.Success)
                    {
                        imageUrls.Add(uploadResult.Data);
                    }
                }
                product.ImageUrls = imageUrls;
                if (imageUrls.Any())
                {
                    product.ThumbnailUrl = imageUrls.First();
                }
            }

            // Ürünü veritabanına kaydet
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(product.ProductId)
                .PutAsync(product);


            // 🔹 Kullanıcının toplam ürün sayısını artır
            var userStatsRef = _firebaseClient
                .Child("user_stats")
                .Child(currentUser.UserId);

            var stats = await userStatsRef.OnceSingleAsync<UserStats>() ?? new UserStats { UserId = currentUser.UserId };

            stats.TotalProducts++;
            await userStatsRef.PutAsync(stats);


            return ServiceResult<Product>.SuccessResult(product, "Ürün başarıyla eklendi!");
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("Ürün eklenirken hata oluştu", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> UpdateProductAsync(string productId, ProductRequest request)
    {
        try
        {
            var validation = ValidateProduct(request);
            if (!validation.IsValid)
            {
                return ServiceResult<Product>.FailureResult("Ürün bilgileri geçersiz", validation.Errors.ToArray());
            }

            var existingProduct = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (existingProduct == null)
            {
                return ServiceResult<Product>.FailureResult("Ürün bulunamadı");
            }

            existingProduct.Title = request.Title.Trim();
            existingProduct.Description = request.Description.Trim();
            existingProduct.CategoryId = request.CategoryId;
            existingProduct.Condition = request.Condition;
            existingProduct.Type = request.Type;
            existingProduct.Price = request.Price;
            existingProduct.Location = request.Location?.Trim();
            existingProduct.Latitude = request.Latitude;
            existingProduct.Longitude = request.Longitude;
            existingProduct.ExchangePreference = request.ExchangePreference?.Trim();
            existingProduct.UpdatedAt = DateTime.UtcNow;

            var categories = await GetCategoriesAsync();
            var category = categories.Data?.FirstOrDefault(c => c.CategoryId == request.CategoryId);
            if (category != null)
            {
                existingProduct.CategoryName = category.Name;
            }

            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var newImageUrls = new List<string>();
                for (int i = 0; i < Math.Min(request.ImagePaths.Count, Constants.MaxProductImages); i++)
                {
                    var uploadResult = await _storageService.UploadProductImageAsync(request.ImagePaths[i], productId, i);
                    if (uploadResult.Success)
                    {
                        newImageUrls.Add(uploadResult.Data);
                    }
                }
                existingProduct.ImageUrls = newImageUrls;
                if (newImageUrls.Any())
                {
                    existingProduct.ThumbnailUrl = newImageUrls.First();
                }
            }

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(existingProduct);

            return ServiceResult<Product>.SuccessResult(existingProduct, "Ürün güncellendi");
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("Güncelleme hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeleteProductAsync(string productId)
    {
        try
        {
            var product = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<bool>.FailureResult("Ürün bulunamadı");
            }

            if (product.ImageUrls != null && product.ImageUrls.Any())
            {
                foreach (var imageUrl in product.ImageUrls)
                {
                    await _storageService.DeleteImageAsync(imageUrl);
                }
            }

            var allFavorites = await _firebaseClient
                .Child(Constants.FavoritesCollection)
                .OrderBy("ProductId")
                .EqualTo(productId)
                .OnceAsync<Favorite>();

            if (allFavorites.Any())
            {
                foreach (var favoriteEntry in allFavorites)
                {
                    await _firebaseClient
                        .Child(Constants.FavoritesCollection)
                        .Child(favoriteEntry.Key)
                        .DeleteAsync();
                }
            }

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .DeleteAsync();

            // 🔹 Kullanıcının toplam ürün sayısını azalt
            var userStatsRef = _firebaseClient
                .Child("user_stats")
                .Child(product.UserId);

            var stats = await userStatsRef.OnceSingleAsync<UserStats>();
            if (stats != null && stats.TotalProducts > 0)
            {
                stats.TotalProducts--;
                await userStatsRef.PutAsync(stats);
            }

            return ServiceResult<bool>.SuccessResult(true, "Ürün ve ilişkili favoriler silindi");
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Silme hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> GetProductByIdAsync(string productId)
    {
        try
        {
            var product = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<Product>.FailureResult("Ürün bulunamadı");
            }

            return ServiceResult<Product>.SuccessResult(product);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("Hata oluştu", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId)
    {
        try
        {
            var allProducts = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .OrderBy("UserId")
                .EqualTo(userId)
                .OnceAsync<Product>();

            foreach (var product in allProducts)
            {
                product.Object.ProductId = product.Key;
            }

            var products = allProducts
                .Select(p => p.Object)
                .Where(p => p.IsActive && !p.IsSold && !p.IsReserved)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return ServiceResult<List<Product>>.SuccessResult(products);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("Kullanıcının ürünleri alınamadı.", ex.Message);
        }
    }
    /// <summary>
    /// TAKAS işlemlerinde kullanılır - Ürün anasayfada kalır, "TAKAS YAPILDI" etiketi ile görünür
    /// </summary>
    public async Task<ServiceResult<bool>> MarkAsExchangedAsync(string productId)
    {
        try
        {
            var product = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<bool>.FailureResult("Ürün bulunamadı");
            }

            // 🔹 Takas için: Görünür kalır
            product.IsSold = true;
            product.IsReserved = false;
            product.SoldAt = DateTime.UtcNow;
            // IsActive = true (değişmez)

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(product);

            return ServiceResult<bool>.SuccessResult(true, "Ürün takas yapıldı olarak işaretlendi");
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("İşlem başarısız", ex.Message);
        }
    }

    /// <summary>
    /// SATIŞ işlemlerinde kullanılır - Ürün anasayfadan kaldırılır
    /// </summary>
    // KamPay/Services/FirebaseProductService.cs

    public async Task<ServiceResult<bool>> MarkAsSoldAsync(string productId)
    {
        try
        {
            var productNode = _firebaseClient.Child(Constants.ProductsCollection).Child(productId);
            var product = await productNode.OnceSingleAsync<Product>();
            if (product != null)
            {
                // Güncellemeler:
                product.IsActive = false;   // Ana listeden kaldırır
                product.IsReserved = false; // Artık rezerve değil
                product.IsSold = true;      // SATILDI olarak işaretler
                product.SoldAt = DateTime.UtcNow; // Satılma zamanını kaydeder

                await productNode.PutAsync(product);
                return ServiceResult<bool>.SuccessResult(true, "Ürün satıldı olarak işaretlendi.");
            }
            return ServiceResult<bool>.FailureResult("Ürün bulunamadı.");
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Ürün işaretlenirken hata oluştu.", ex.Message);
        }
    }


    public async Task<ServiceResult<bool>> MarkAsReservedAsync(string productId, bool isReserved)
    {
        try
        {
            var product = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<bool>.FailureResult("Ürün bulunamadı");
            }

            product.IsReserved = isReserved;

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(product);

            var message = isReserved ? "Ürün rezerve edildi" : "Rezervasyon kaldırıldı";
            return ServiceResult<bool>.SuccessResult(true, message);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("İşlem başarısız", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> IncrementViewCountAsync(string productId)
    {
        try
        {
            var product = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<bool>.FailureResult("Ürün bulunamadı");
            }

            product.ViewCount++;

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(product);

            return ServiceResult<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Hata", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Category>>> GetCategoriesAsync()
    {
        try
        {
            var firebaseCategories = await _firebaseClient
                .Child(Constants.CategoriesCollection)
                .OnceAsync<Category>();

            if (firebaseCategories.Any())
            {
                // DÜZELTME: Firebase'den gelen 'Key'i, nesnenin 'CategoryId' özelliğine atıyoruz.
                var categories = firebaseCategories.Select(c =>
                {
                    var category = c.Object;
                    category.CategoryId = c.Key; // En önemli satır!
                    return category;
                }).ToList();

                return ServiceResult<List<Category>>.SuccessResult(categories);
            }

            // --- Tohumlama (Seeding) Mantığını da İyileştirelim ---
            var defaultCategories = Category.GetDefaultCategories();
            foreach (var category in defaultCategories)
            {
                // PutAsync yerine PostAsync kullanarak Firebase'in benzersiz ID oluşturmasını sağlayalım.
                // Bu, yukarıdaki veri çekme mantığıyla %100 uyumlu çalışır.
                await _firebaseClient
                    .Child(Constants.CategoriesCollection)
                    .PostAsync(category);
            }

            // Veritabanı boşsa ve yeni doldurulduysa, tekrar okuyarak doğru ID'lerle dönelim
            return await GetCategoriesAsync();
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Category>>.SuccessResult(
                Category.GetDefaultCategories(),
                "Kategoriler yerel olarak yüklendi"
            );
        }
    }

    public ValidationResult ValidateProduct(ProductRequest request)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            result.AddError("Ürün başlığı boş olamaz");
        }
        else if (request.Title.Length > Constants.MaxProductTitleLength)
        {
            result.AddError($"Başlık en fazla {Constants.MaxProductTitleLength} karakter olabilir");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            result.AddError("Ürün açıklaması boş olamaz");
        }
        else if (request.Description.Length > Constants.MaxProductDescriptionLength)
        {
            result.AddError($"Açıklama en fazla {Constants.MaxProductDescriptionLength} karakter olabilir");
        }

        if (string.IsNullOrWhiteSpace(request.CategoryId))
        {
            result.AddError("Kategori seçilmelidir");
        }

        if (request.Type == ProductType.Satis)
        {
            if (request.Price <= 0)
            {
                result.AddError("Satış fiyatı 0'dan büyük olmalıdır");
            }
            else if (request.Price > 999999)
            {
                result.AddError("Fiyat çok yüksek");
            }
        }

        if (request.ImagePaths != null && request.ImagePaths.Count > Constants.MaxProductImages)
        {
            result.AddError($"En fazla {Constants.MaxProductImages} görsel eklenebilir");
        }
        /*
        if (request.Type == ProductType.Takas && string.IsNullOrWhiteSpace(request.ExchangePreference))
        {
            result.AddError("Takas için tercih belirtilmelidir");
        }
        */
        return result;
    }
    #endregion
    // ... (Mevcut metotlarınızın sonu) ...

    // --- YENİ EKLENEN METOT 1 ---
    public async Task<ServiceResult<List<Product>>> GetProductsAsync(string categoryId = null, string searchText = null)
    {
        try
        {
            var query = _firebaseClient.Child(Constants.ProductsCollection).OrderBy("CreatedAt");
            var productItems = await query.OnceAsync<Product>();

            var products = productItems.Select(item =>
            {
                var product = item.Object;
                product.ProductId = item.Key;
                return product;
            })
            .Where(p => !p.IsSold) // Sadece satılmamış olanları getir
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

            // Kategoriye göre filtrele
            if (!string.IsNullOrEmpty(categoryId))
            {
                products = products.Where(p => p.CategoryId == categoryId).ToList();
            }

            // Arama metnine göre filtrele
            if (!string.IsNullOrEmpty(searchText))
            {
                products = products.Where(p => p.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return ServiceResult<List<Product>>.SuccessResult(products);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("Hata", ex.Message);
        }
    }
    // FirebaseProductService.cs içine ekleyin:

    // 🔥 YENİ METOD: Direkt kaydetme (resimler zaten yüklenmiş)
    public async Task<ServiceResult<Product>> SaveProductDirectlyAsync(Product product)
    {
        try
        {
            // Firebase'e kaydet
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(product.ProductId)
                .PutAsync(product);

            // 🔥 Kullanıcı istatistiklerini güncelle
            var userStatsRef = _firebaseClient
                .Child(Constants.UserStatsCollection)
                .Child(product.UserId);

            var stats = await userStatsRef.OnceSingleAsync<UserStats>()
                ?? new UserStats { UserId = product.UserId };

            stats.TotalProducts++;
            await userStatsRef.PutAsync(stats);

            Console.WriteLine($"✅ Ürün kaydedildi: {product.Title}");
            return ServiceResult<Product>.SuccessResult(product, "Ürün başarıyla eklendi!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SaveProductDirectly hatası: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Ürün kaydedilemedi", ex.Message);
        }
    }

    /// <summary>
    /// Sayfalama desteği ile ürünleri getirir (Performans Optimizasyonu)
    /// </summary>
    public async Task<ServiceResult<List<Product>>> GetProductsPagedAsync(
        int pageSize = 20,
        string lastKey = null,
        ProductFilter filter = null)
    {
        try
        {
            // 🔥 Firebase query'yi doğru şekilde oluştur
            var productsRef = _firebaseClient.Child(Constants.ProductsCollection);

            // Önce OrderBy uygula
            var orderedQuery = productsRef.OrderBy("CreatedAt");

            // Sayfalama için StartAt veya LimitToFirst
            IEnumerable<FirebaseObject<Product>> items;

            if (!string.IsNullOrEmpty(lastKey))
            {
                // Önceki sayfadan devam et
                items = await orderedQuery
                    .StartAt(lastKey)
                    .LimitToFirst(pageSize + 1) // +1 ile bir sonraki sayfa var mı kontrol et
                    .OnceAsync<Product>();
            }
            else
            {
                // İlk sayfa
                items = await orderedQuery
                    .LimitToFirst(pageSize)
                    .OnceAsync<Product>();
            }

            // 🔥 Product listesine dönüştür
            var products = items.Select(p =>
            {
                var product = p.Object;
                product.ProductId = p.Key;
                return product;
            }).ToList();

            // lastKey'i atla (eğer pagination yapılıyorsa)
            if (!string.IsNullOrEmpty(lastKey) && products.Any() && products.First().ProductId == lastKey)
            {
                products.RemoveAt(0);
            }

            // 🔥 Hafif filtreleme (ağır işlemler UI thread'de yapılmayacak)
            if (filter != null)
            {
                if (filter.OnlyActive)
                {
                    products = products.Where(p => p.IsActive).ToList();
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    var searchLower = filter.SearchText.ToLowerInvariant();
                    products = products.Where(p =>
                        p.Title.ToLowerInvariant().Contains(searchLower) ||
                        p.Description.ToLowerInvariant().Contains(searchLower)
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(filter.CategoryId))
                {
                    products = products.Where(p => p.CategoryId == filter.CategoryId).ToList();
                }

                if (filter.Type.HasValue)
                {
                    products = products.Where(p => p.Type == filter.Type.Value).ToList();
                }
            }

            return ServiceResult<List<Product>>.SuccessResult(products);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", ex.Message);
        }
    }


    // --- YENİ EKLENEN METOT 2 ---
    public async Task<ServiceResult<bool>> UpdateProductOwnerAsync(string productId, string newOwnerId, bool markAsSold = true)
    {
        try
        {
            var productNode = _firebaseClient.Child(Constants.ProductsCollection).Child(productId);
            var product = await productNode.OnceSingleAsync<Product>();

            if (product == null)
            {
                return ServiceResult<bool>.FailureResult("Ürün bulunamadı.");
            }

            product.UserId = newOwnerId; // Yeni sahibi ata
            if (markAsSold)
            {
                // Ürünü hem satıldı hem de rezerve değil olarak işaretle
                product.IsSold = true;
                product.IsReserved = false;
            }

            await productNode.PutAsync(product);
            return ServiceResult<bool>.SuccessResult(true, "Ürün sahibi güncellendi.");
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Hata", ex.Message);
        }
    }




}