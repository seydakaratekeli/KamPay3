using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ✅ YENİ MAUI API GARSONU
/// Firebase'e doğrudan bağlanmak yerine, arka planda çalışan kendi API'mize istek atar.
/// SOLID Prensiplerine sadık kalarak IProductService arayüzünü uygular.
/// </summary>
public class ProductApiService : IProductService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;
    private readonly string _baseUrl;
    private readonly IProductImageCoordinator _imageCoordinator;
    private readonly IProductCacheService _cacheService;

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public ProductApiService(HttpClient httpClient, IAuthenticationService authService, IProductImageCoordinator imageCoordinator, IProductCacheService cacheService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _imageCoordinator = imageCoordinator ?? throw new ArgumentNullException(nameof(imageCoordinator));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Platform bazlı API adresi belirleme:
        // - Android Emülatör: localhost yerine 10.0.2.2 kullanılmalı
        // - Android Gerçek Cihaz (USB): Bilgisayarın LAN IP adresi kullanılmalı
        // - Windows (MAUI): localhost doğrudan çalışır
        // ⚠️ Android'de HTTP kullanıyoruz (SSL sertifika sorunu olmasın diye)
        //    AndroidManifest.xml'de usesCleartextTraffic="true" zaten açık
#if ANDROID
        // 📱 GERÇEK CİHAZ TESTİ: Bilgisayarınızın IP adresini buraya yazın
        // CMD'de "ipconfig" komutu ile öğrenebilirsiniz
        var baseHost = "http://192.168.226.219:5011";

        // 🖥️ EMÜLATÖR TESTİ İÇİN: Yukarıdaki satırı yorum yapıp bunu açın
        // var baseHost = "http://10.0.2.2:5011";
#elif IOS
        var baseHost = "http://192.168.226.219:5011";
#else
        var baseHost = "http://localhost:5011";
#endif
        _baseUrl = $"{baseHost}/api/v1/products";

        System.Diagnostics.Debug.WriteLine($"✅ ProductApiService oluşturuldu (API Garsonu devrede) → {_baseUrl}");
    }

    // --- GİZLİ SİLAHIMIZ: İSTEKLERE TOKEN EKLEYEN METOT ---
    private async Task SetAuthHeaderAsync()
    {
        // 🌟 YENİ: Custom API JWT'mizi SecureStorage'dan alıyoruz
        var token = await Microsoft.Maui.Storage.SecureStorage.GetAsync("KAMPAY_API_JWT");

        if (!string.IsNullOrEmpty(token))
        {
            // API'nin kapısındaki [Authorize] duvarını geçmek için Token'ı Header'a ekliyoruz
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            System.Diagnostics.Debug.WriteLine($"🔑 Auth token ayarlandı: {token.Substring(0, Math.Min(20, token.Length))}...");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("⚠️ KAMPAY_API_JWT bulunamadı! Yetki gerektiren endpointler 401 hatası verebilir.");
        }
    }

    #region IProductQueryService Methods

    public async Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Token'a gerek yok, herkes ürünleri görebilir. Direkt API'mizden çekiyoruz.
            var response = await _httpClient.GetAsync(_baseUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(_jsonOptions, cancellationToken);

                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>(), "Ürünler API'den çekildi");
            }

            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ GetAllProductsAsync hata: {ex.Message}");
            return ServiceResult<List<Product>>.FailureResult("API bağlantı hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default) 
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/{productId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<Product>(_jsonOptions, cancellationToken);
                if (product != null)
                {
                    return ServiceResult<Product>.SuccessResult(product);
                }
            }
            return ServiceResult<Product>.FailureResult($"API Hatası ({response.StatusCode})", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API bağlantı hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId, CancellationToken cancellationToken = default) 
    {
        try
        {
            // API'de /user/{userId} rotasının olduğunu varsayıyoruz
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/{userId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(_jsonOptions, cancellationToken);
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetProductsAsync(string? categoryId = null, string? searchText = null, CancellationToken cancellationToken = default) 
    {
        try
        {
            var query = $"?categoryId={categoryId}&searchText={searchText}";
            var response = await _httpClient.GetAsync($"{_baseUrl}{query}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(_jsonOptions, cancellationToken);
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetProductsPagedAsync(int pageSize = 20, string? lastKey = null, ProductFilter? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"?pageSize={pageSize}&lastKey={lastKey}";
            var response = await _httpClient.GetAsync($"{_baseUrl}/paged{query}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(_jsonOptions, cancellationToken);
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public Task<ServiceResult<List<Category>>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // Kategoriler de API'den gelecek şekilde güncellenecek
        return Task.FromResult(ServiceResult<List<Category>>.SuccessResult(Category.GetDefaultCategories(), "Varsayılan Kategoriler"));
    }

    #endregion

    #region IProductCommandService Methods

    public async Task<ServiceResult<Product>> AddProductAsync(ProductRequest request, User currentUser)
    {
        try
        {
            var validation = ValidateProduct(request);
            if (!validation.IsValid)
            {
                return ServiceResult<Product>.FailureResult("Geçersiz ürün", validation.Errors.ToArray());
            }

            // 🛑 1. Resimleri İstemciden (MAUI) Doğrudan Firebase Storage'a Yükle
            var tempProductId = Guid.NewGuid().ToString(); // Storage'da klasör adı veya ileride API'nin kabul edeceği ID
            var uploadedImageUrls = new List<string>();

            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var uploadResult = await _imageCoordinator.UploadProductImagesParallelAsync(request.ImagePaths, tempProductId);
                if (!uploadResult.Success)
                {
                    return ServiceResult<Product>.FailureResult("Resimler yüklenemedi", uploadResult.Message);
                }

                uploadedImageUrls = uploadResult.Data;
            }

            // Örnek eşleme (API 'Product' modelini kabul ediyor)
            var newProduct = new Product
            {
                ProductId = tempProductId,
                Title = request.Title,
                Description = request.Description,
                Price = request.Price,
                CategoryId = request.CategoryId ?? string.Empty,
                CategoryName = request.CategoryName ?? string.Empty,
                Condition = request.Condition,
                Type = request.Type,
                Location = request.Location ?? string.Empty,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UserId = currentUser.UserId,
                UserName = currentUser.FullName,
                UserPhotoUrl = currentUser.ProfileImageUrl,
                ImageUrls = uploadedImageUrls,
                ThumbnailUrl = uploadedImageUrls.FirstOrDefault() ?? string.Empty
            };

            // Güvenlik headerı
            await SetAuthHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync(_baseUrl, newProduct);

            if (response.IsSuccessStatusCode)
            {
                // API dönüşünde belki farklı ID veya Product gelebilir, duruma göre:
                var apiProduct = await response.Content.ReadFromJsonAsync<Product>();
                var finalProduct = apiProduct ?? newProduct;

                // 🛑 Cache'e ekle
                if (_cacheService != null)
                {
                    await _cacheService.UpdateProductInCacheAsync(finalProduct);
                }

                return ServiceResult<Product>.SuccessResult(finalProduct, "Ürün API üzerinden eklendi");
            }
            var authHeaders = response.Headers.WwwAuthenticate.ToString();
            return ServiceResult<Product>.FailureResult("Ürün eklenemedi", $"{response.StatusCode} - {authHeaders} - {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> UpdateProductAsync(string productId, ProductRequest request) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}/{productId}", request);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<Product>.SuccessResult(new Product(), "Ürün başarıyla güncellendi");
            }
            return ServiceResult<Product>.FailureResult("Ürün güncellenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeleteProductAsync(string productId) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/{productId}");
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Ürün başarıyla silindi");
            }
            return ServiceResult<bool>.FailureResult("Ürün silinemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> UpdateProductOwnerAsync(string productId, string newOwnerId, bool markAsSold = true) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var body = new { newOwnerId, markAsSold };
            var response = await _httpClient.PatchAsJsonAsync($"{_baseUrl}/{productId}/owner", body);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Ürün sahibi güncellendi");
            }
            return ServiceResult<bool>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> MarkAsSoldAsync(string productId) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsJsonAsync($"{_baseUrl}/{productId}/marksold", new { });
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Ürün satıldı olarak işaretlendi");
            }
            return ServiceResult<bool>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> MarkAsExchangedAsync(string productId) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsJsonAsync($"{_baseUrl}/{productId}/markexchanged", new { });
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Ürün takas edildi olarak işaretlendi");
            }
            return ServiceResult<bool>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> MarkAsReservedAsync(string productId, bool isReserved) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var body = new { isReserved };
            var response = await _httpClient.PatchAsJsonAsync($"{_baseUrl}/{productId}/markreserved", body);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Ürün rezervasyon durumu güncellendi");
            }
            return ServiceResult<bool>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> SaveProductDirectlyAsync(Product product) 
    {
        try
        {
            await SetAuthHeaderAsync();
            
            System.Diagnostics.Debug.WriteLine($"📤 SaveProductDirectly → {_baseUrl}");
            System.Diagnostics.Debug.WriteLine($"📤 Product: {product.Title}, UserId: {product.UserId}");
            
            var response = await _httpClient.PostAsJsonAsync(_baseUrl, product);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"📥 API Yanıt: {response.StatusCode} - {responseContent}");
            
            if (response.IsSuccessStatusCode)
            {
                // API { Message, ProductId } formatında dönüyor, Product değil.
                // Bu yüzden gönderdiğimiz product objesini doğrudan kullanıyoruz.
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (apiResponse.TryGetProperty("ProductId", out var pidProp) ||
                        apiResponse.TryGetProperty("productId", out pidProp))
                    {
                        product.ProductId = pidProp.GetString() ?? product.ProductId;
                    }
                }
                catch { /* API yanıtı parse edilemezse orijinal ProductId'yi kullan */ }

                // Önbelleğe de ekleyelim
                if (_cacheService != null)
                {
                    await _cacheService.UpdateProductInCacheAsync(product);
                }
                return ServiceResult<Product>.SuccessResult(product, "Ürün doğrudan kaydedildi");
            }

            var authHeaders = response.Headers.WwwAuthenticate.ToString();
            return ServiceResult<Product>.FailureResult("İşlem başarısız", $"{response.StatusCode} - {authHeaders} - {responseContent}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ SaveProductDirectly hata: {ex.Message}");
            return ServiceResult<Product>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> UpdateUserInfoInProductsAsync(string userId, string? newName, string? newPhotoUrl) 
    {
        try
        {
            await SetAuthHeaderAsync();
            var body = new { userId, newName, newPhotoUrl };
            var response = await _httpClient.PatchAsJsonAsync($"{_baseUrl}/updateuserinfo", body);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true, "Kullanıcı bilgileri güncellendi");
            }
            return ServiceResult<bool>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    #endregion

    #region IProductValidationService Methods

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

        if (string.IsNullOrWhiteSpace(request.CategoryId))
        {
            result.AddError("Kategori seçilmelidir");
        }

        return result;
    }

    public async Task<ServiceResult<bool>> IncrementViewCountAsync(string productId) 
    {
        try
        {
            // View count genelde herkes tarafından artırılabildiği için token gerekmeyebilir ancak endpoint dizaynına bağlıdır.
            // await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsync($"{_baseUrl}/{productId}/incrementview", null);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true);
            }
            return ServiceResult<bool>.FailureResult("Görüntülenme artırılamadı", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatası", ex.Message);
        }
    }

    #endregion
}
