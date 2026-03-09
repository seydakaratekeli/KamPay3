using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System.Linq;

namespace KamPay.Services;

/// <summary>
/// ✅ SOLID PRENSİPLERE UYGUN: Firebase Product Service
/// - SRP: Tek sorumluluk - ürün CRUD işlemleri
/// - OCP: Genişletilebilir - yeni metod eklenebilir, mevcut kod değişmez
/// - LSP: IProductService'in tüm davranışlarını doğru implement eder
/// - ISP: Interface'ler küçük ve odaklanmış (Query, Command, Validation)
/// - DIP: Concrete class'lara değil interface'lere bağımlı
/// </summary>
public class FirebaseProductService : IProductService
{
    private readonly FirebaseClient _firebaseClient;
    private readonly IProductImageCoordinator _imageCoordinator;
    private readonly IProductCacheService _cacheService;

    // ✅ SOLID: Constructor Injection - tüm bağımlılıklar DI'den geliyor
    public FirebaseProductService(
        FirebaseClient firebaseClient,
        IProductImageCoordinator imageCoordinator,
        IProductCacheService cacheService)
    {
        _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        _imageCoordinator = imageCoordinator ?? throw new ArgumentNullException(nameof(imageCoordinator));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        
        System.Diagnostics.Debug.WriteLine("✅ FirebaseProductService oluşturuldu (DI ile - SOLID uyumlu)");
    }

    public async Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null)
    {
        try
        {
            List<Product> products;

            // ?? PERFORMANS OPTİMİZASYONU: Sunucu tarafı filtreleme
            if (filter != null)
            {
                // Firebase Query ile sunucu tarafında filtreleme
                var query = _firebaseClient.Child(Constants.ProductsCollection);

                // Öncelik sırasına göre en etkili filtreyi uygula
                if (!string.IsNullOrWhiteSpace(filter.CategoryId))
                {
                    // Kategori filtresi - Sunucu tarafı
                    var categoryProducts = await query
                        .OrderBy("CategoryId")
                        .EqualTo(filter.CategoryId)
                        .LimitToFirst(100)
                        .OnceAsync<Product>();

                    products = categoryProducts.Select(p =>
                    {
                        var product = p.Object;
                        product.ProductId = p.Key;
                        return product;
                    }).ToList();
                }
                else if (filter.Type.HasValue)
                {
                    // Tip filtresi - Sunucu tarafı
                    var typeProducts = await query
                        .OrderBy("Type")
                        .EqualTo((int)filter.Type.Value)
                        .LimitToFirst(100)
                        .OnceAsync<Product>();

                    products = typeProducts.Select(p =>
                    {
                        var product = p.Object;
                        product.ProductId = p.Key;
                        return product;
                    }).ToList();
                }
                else if (filter.OnlyActive)
                {
                    // Aktif ürünler - Sunucu tarafı
                    var activeProducts = await query
                        .OrderBy("IsActive")
                        .EqualTo(true)
                        .LimitToFirst(100)
                        .OnceAsync<Product>();

                    products = activeProducts.Select(p =>
                    {
                        var product = p.Object;
                        product.ProductId = p.Key;
                        return product;
                    }).ToList();
                }
                else
                {
                    // Varsayılan: Son 100 ürün
                    var defaultProducts = await query
                        .OrderByKey()
                        .LimitToLast(100)
                        .OnceAsync<Product>();

                    products = defaultProducts.Select(p =>
                    {
                        var product = p.Object;
                        product.ProductId = p.Key;
                        return product;
                    }).ToList();
                }

                // İstemci tarafı ince ayar filtreleri
                var productsQuery = products.AsQueryable();

                // Aktif ve satılmamış kontrolü
                if (filter.OnlyActive)
                {
                    productsQuery = productsQuery.Where(p => p.IsActive && !p.IsSold);
                }

                // Arama metni (Firebase'de full-text search yok)
                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    var searchLower = filter.SearchText.ToLowerInvariant();
                    productsQuery = productsQuery.Where(p =>
                        p.Title.ToLowerInvariant().Contains(searchLower) ||
                        p.Description.ToLowerInvariant().Contains(searchLower)
                    );
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
                    var locationLower = filter.Location.ToLowerInvariant();
                    productsQuery = productsQuery.Where(p =>
                        p.Location != null && p.Location.ToLowerInvariant().Contains(locationLower)
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

                products = productsQuery.ToList();
            }
            else
            {
                // Filtre yoksa önbellekten kontrol et
                if (_cacheService.IsCacheValid)
                {
                    products = await _cacheService.GetCachedProductsAsync();
                }
                else
                {
                    // Son 100 ürünü getir (Tüm ürünler yerine)
                    var recentProducts = await _firebaseClient
                        .Child(Constants.ProductsCollection)
                        .OrderByKey()
                        .LimitToLast(100)
                        .OnceAsync<Product>();

                    products = recentProducts.Select(p =>
                    {
                        var product = p.Object;
                        product.ProductId = p.Key;
                        return product;
                    })
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                    // Önbelleğe kaydet
                    await _cacheService.SetCacheAsync(products);
                }
            }

            return ServiceResult<List<Product>>.SuccessResult(products);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", ex.Message);
        }
    }
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
                ProductId = Guid.NewGuid().ToString(),

                // GÜVENLİK: XSS ve zararlı içeriklere karşı Girdi Temizleme (Sanitization)
                Title = InputSanitizer.SanitizeText(request.Title.Trim()),
                Description = InputSanitizer.SanitizeText(request.Description.Trim()),
                CategoryId = request.CategoryId ?? string.Empty,
                CategoryName = categoryName,
                Condition = request.Condition,
                Type = request.Type,
                Price = request.Price,
                Location = InputSanitizer.SanitizeText(request.Location?.Trim() ?? string.Empty),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                UserId = currentUser.UserId,
                UserName = currentUser.FullName ?? string.Empty,
                UserEmail = currentUser.Email,
                UserPhotoUrl = currentUser.ProfileImageUrl ?? string.Empty,
                ExchangePreference = InputSanitizer.SanitizeText(request.ExchangePreference?.Trim() ?? string.Empty),

                IsForSurpriseBox = request.IsForSurpriseBox,
                IsActive = true,
                IsSold = false,
                IsReserved = false,
                CreatedAt = DateTime.UtcNow
            };

            // ✅ REFACTOR: Görsel yükleme işi koordinatöre devredildi
            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var uploadResult = await _imageCoordinator.UploadProductImagesAsync(
                    request.ImagePaths, 
                    product.ProductId);

                if (uploadResult.Success && uploadResult.Data != null && uploadResult.Data.Any())
                {
                    product.ImageUrls = uploadResult.Data;
                    product.ThumbnailUrl = uploadResult.Data.First();
                }
                else
                {
                    return ServiceResult<Product>.FailureResult(
                        "Görseller yüklenemedi", 
                        uploadResult.Message);
                }
            }

            // Ürünü veritabanına kaydet
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(product.ProductId)
                .PutAsync(product);

            // PERFORMANS: Yeni ürünü anında önbelleğe (cache) ekleyerek liste yenileme hızını artır
            await _cacheService.UpdateProductInCacheAsync(product);

            // Kullanıcının toplam ürün sayısını artır
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
            existingProduct.CategoryId = request.CategoryId ?? string.Empty;
            existingProduct.Condition = request.Condition;
            existingProduct.Type = request.Type;
            existingProduct.Price = request.Price;
            existingProduct.Location = request.Location?.Trim() ?? string.Empty;
            existingProduct.Latitude = request.Latitude;
            existingProduct.Longitude = request.Longitude;
            existingProduct.ExchangePreference = request.ExchangePreference?.Trim() ?? string.Empty;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            var categories = await GetCategoriesAsync();
            var category = categories.Data?.FirstOrDefault(c => c.CategoryId == request.CategoryId);
            if (category != null)
            {
                existingProduct.CategoryName = category.Name;
            }

            // ✅ REFACTOR: Görsel yükleme işi koordinatöre devredildi
            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var uploadResult = await _imageCoordinator.UploadProductImagesAsync(
                    request.ImagePaths,
                    productId);

                if (uploadResult.Success && uploadResult.Data != null && uploadResult.Data.Any())
                {
                    existingProduct.ImageUrls = uploadResult.Data;
                    existingProduct.ThumbnailUrl = uploadResult.Data.First();
                }
            }

            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(existingProduct);

            await _cacheService.UpdateProductInCacheAsync(existingProduct);
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

            // ✅ REFACTOR: Görsel silme işi koordinatöre devredildi
            if (product.ImageUrls != null && product.ImageUrls.Any())
            {
                var deleteResult = await _imageCoordinator.DeleteProductImagesAsync(product.ImageUrls);
                
                if (!deleteResult.Success)
                {
                    // Görseller silinemese bile işleme devam et (warning log)
                    System.Diagnostics.Debug.WriteLine($"⚠️ Görseller silinemedi: {deleteResult.Message}");
                }
            }

            // Favorileri sil
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

            // Ürünü sil
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .DeleteAsync();

            //  Kullanıcının toplam ürün sayısını azalt
            var userStatsRef = _firebaseClient
                .Child("user_stats")
                .Child(product.UserId);

            var stats = await userStatsRef.OnceSingleAsync<UserStats>();
            if (stats != null && stats.TotalProducts > 0)
            {
                stats.TotalProducts--;
                await userStatsRef.PutAsync(stats);
            }

            await _cacheService.RemoveProductFromCacheAsync(productId);
            return ServiceResult<bool>.SuccessResult(true, "Ürün silindi");
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

    // TAKAS işlemlerinde kullanılır - Ürün anasayfada kalır, "TAKAS YAPILDI" etiketi ile görünür

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

            //  Takas için: Görünür kalır
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


    // SATIŞ işlemlerinde kullanılır - Ürün anasayfadan kaldırılır

    public async Task<ServiceResult<bool>> MarkAsSoldAsync(string productId)
    {
        try
        {
            var productNode = _firebaseClient.Child(Constants.ProductsCollection).Child(productId);
            var product = await productNode.OnceSingleAsync<Product>();

            if (product != null)
            {
                // Veri bütünlüğü ve cache için ID'yi atıyoruz
                product.ProductId = productId;

                // Güncellemeler:
                product.IsActive = false;   // Ana listeden kaldırır
                product.IsReserved = false; // Artık rezerve değil
                product.IsSold = true;      // SATILDI olarak işaretler
                product.SoldAt = DateTime.UtcNow; // Satılma zamanını kaydeder

                // Firebase'e kaydet
                await productNode.PutAsync(product);

                // PERFORMANS: Değişikliği anında önbelleğe (cache) yansıt
                await _cacheService.UpdateProductInCacheAsync(product);

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

            // Cache güncellenirken doğru anahtarı kullanması için ID'yi set ediyoruz
            product.ProductId = productId;
            product.IsReserved = isReserved;

            // Firebase'e kaydet
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(productId)
                .PutAsync(product);

            // PERFORMANS: Rezervasyon durumunu önbellekte güncelle
            await _cacheService.UpdateProductInCacheAsync(product);

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
                var categories = firebaseCategories.Select(c =>
                {
                    var category = c.Object;
                    category.CategoryId = c.Key;
                    return category;
                }).ToList();

                return ServiceResult<List<Category>>.SuccessResult(categories);
            }

            // ? FIX: Tohumlama sonrası DOĞRUDAN oku, recursive çağrı yapma
            var defaultCategories = Category.GetDefaultCategories();
            foreach (var category in defaultCategories)
            {
                await _firebaseClient
                    .Child(Constants.CategoriesCollection)
                    .PostAsync(category);
            }

            // ? Tohumlama sonrası tekrar oku (recursive değil)
            var seededCategories = await _firebaseClient
                .Child(Constants.CategoriesCollection)
                .OnceAsync<Category>();

            if (seededCategories.Any())
            {
                var categories = seededCategories.Select(c =>
                {
                    var category = c.Object;
                    category.CategoryId = c.Key;
                    return category;
                }).ToList();

                return ServiceResult<List<Category>>.SuccessResult(categories);
            }

            // Son çare: Varsayılan kategorileri döndür
            return ServiceResult<List<Category>>.SuccessResult(
                defaultCategories,
                "Kategoriler yerel olarak yüklendi"
            );
        }
        catch
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
        else
        {
            // Check for dangerous content in title
            if (InputSanitizer.ContainsDangerousContent(request.Title))
            {
                result.AddError("Ürün başlığı geçersiz karakterler içeriyor");
            }
            else if (request.Title.Length > Constants.MaxProductTitleLength)
            {
                result.AddError($"Başlık en fazla {Constants.MaxProductTitleLength} karakter olabilir");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            result.AddError("Ürün açıklaması boş olamaz");
        }
        else
        {
            // Check for dangerous content in description
            if (InputSanitizer.ContainsDangerousContent(request.Description))
            {
                result.AddError("Ürün açıklaması geçersiz karakterler içeriyor");
            }
            else if (request.Description.Length > Constants.MaxProductDescriptionLength)
            {
                result.AddError($"Açıklama en fazla {Constants.MaxProductDescriptionLength} karakter olabilir");
            }
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



    public async Task<ServiceResult<List<Product>>> GetProductsAsync(string? categoryId = null, string? searchText = null)
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


    //  Direkt kaydetme (resimler zaten yüklenmiş)
    public async Task<ServiceResult<Product>> SaveProductDirectlyAsync(Product product)
    {
        try
        {
            // Firebase'e kaydet
            await _firebaseClient
                .Child(Constants.ProductsCollection)
                .Child(product.ProductId)
                .PutAsync(product);

            //  Kullanıcı istatistiklerini güncelle
            var userStatsRef = _firebaseClient
                .Child(Constants.UserStatsCollection)
                .Child(product.UserId);

            var stats = await userStatsRef.OnceSingleAsync<UserStats>()
                ?? new UserStats { UserId = product.UserId };

            stats.TotalProducts++;
            await userStatsRef.PutAsync(stats);

            Console.WriteLine($"? Ürün kaydedildi: {product.Title}");
            return ServiceResult<Product>.SuccessResult(product, "Ürün başarıyla eklendi!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? SaveProductDirectly hatası: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Ürün kaydedilemedi", ex.Message);
        }
    }


    /// Sayfalama desteği ile ürünleri getirir (Performans Optimizasyonu)

    public async Task<ServiceResult<List<Product>>> GetProductsPagedAsync(
        int pageSize = 20,
        string? lastKey = null,
        ProductFilter? filter = null)
    {
        try
        {
            //  Firebase query'yi doğru şekilde oluştur
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

            //  Product listesine dönüştür
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

            //  Hafif filtreleme (ağır işlemler UI thread'de yapılmayacak)
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


    /// Kullanıcının tüm ürünlerindeki isim ve profil fotoğrafı bilgilerini günceller
    /// ? OPTIMIZE: Firebase PatchAsync ile atomic güncelleme
    /// </summary>
    public async Task<ServiceResult<bool>> UpdateUserInfoInProductsAsync(string userId, string? newName, string? newPhotoUrl)
    {
        try
        {
            // 1?? Kullanıcının ürünlerini bul
            var allProducts = await _firebaseClient
                .Child(Constants.ProductsCollection)
                .OrderBy("UserId")
                .EqualTo(userId)
                .OnceAsync<Product>();

            if (!allProducts.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Güncellenecek ürün yok");
            }

            // 2?? ? FIX: Her ürün için ayrı PatchAsync çağrısı (paralel)
            var updateTasks = new List<Task>();

            foreach (var productEntry in allProducts)
            {
                var updates = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    updates["UserName"] = newName;
                }

                if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                {
                    updates["UserPhotoUrl"] = newPhotoUrl;
                }

                if (updates.Any())
                {
                    // ? Her ürün için PatchAsync (güvenli atomic update)
                    var task = _firebaseClient
                        .Child(Constants.ProductsCollection)
                        .Child(productEntry.Key)
                        .PatchAsync(updates);

                    updateTasks.Add(task);
                }
            }

            // 3?? ? TÜM GÜNCELLEMELERI PARALEL BEK  LE
            await Task.WhenAll(updateTasks);

            Console.WriteLine($"? {allProducts.Count()} ürün PatchAsync ile güncellendi");

            return ServiceResult<bool>.SuccessResult(true, $"{allProducts.Count()} ürün güncellendi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? UpdateUserInfoInProducts hatası: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Ürünler güncellenemedi", ex.Message);
        }
    }


}