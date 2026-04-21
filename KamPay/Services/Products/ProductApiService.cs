using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Auth;
using KamPay.Services.Products.Coordinators;

namespace KamPay.Services.Products;

/// <summary>
/// âœ… YENÄ° MAUI API GARSONU
/// Firebase'e doÄŸrudan baÄŸlanmak yerine, arka planda Ã§alÄ±ÅŸan kendi API'mize istek atar.
/// SOLID Prensiplerine sadÄ±k kalarak IProductService arayÃ¼zÃ¼nÃ¼ uygular.
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

    public ProductApiService(HttpClient httpClient, IAuthenticationService authService, IProductImageCoordinator imageCoordinator, IProductCacheService cacheService, ApiSettings apiSettings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _imageCoordinator = imageCoordinator ?? throw new ArgumentNullException(nameof(imageCoordinator));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Platform bazlÄ± API adresi belirleme:
        // - Android EmÃ¼latÃ¶r: localhost yerine 10.0.2.2 kullanÄ±lmalÄ±
        // - Android GerÃ§ek Cihaz (USB): BilgisayarÄ±n LAN IP adresi kullanÄ±lmalÄ±
        // - Windows (MAUI): localhost doÄŸrudan Ã§alÄ±ÅŸÄ±r
        // âš ï¸ Android'de HTTP kullanÄ±yoruz (SSL sertifika sorunu olmasÄ±n diye)
        //    AndroidManifest.xml'de usesCleartextTraffic="true" zaten aÃ§Ä±k

        // ğŸ”„ ArtÄ±k IP konfigÃ¼rasyonu JSON dosyasÄ±ndan okunuyor!
        var baseHost = apiSettings.LocalApiBaseUrl;

        _baseUrl = $"{baseHost}/api/v1/products";

        KamPay.Helpers.AppLogger.DebugLog($"âœ… ProductApiService oluÅŸturuldu (API Garsonu devrede) â†’ {_baseUrl}");
    }

    // --- GÄ°ZLÄ° SÄ°LAHIMIZ: Ä°STEKLERE TOKEN EKLEYEN METOT ---
    private async Task SetAuthHeaderAsync()
    {
        // ğŸŒŸ YENÄ°: Custom API JWT'mizi SecureStorage'dan alÄ±yoruz
        var token = await Microsoft.Maui.Storage.SecureStorage.GetAsync("KAMPAY_API_JWT");

        if (!string.IsNullOrEmpty(token))
        {
            // API'nin kapÄ±sÄ±ndaki [Authorize] duvarÄ±nÄ± geÃ§mek iÃ§in Token'Ä± Header'a ekliyoruz
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ”‘ Auth token ayarlandÄ±: {token.Substring(0, Math.Min(20, token.Length))}...");
        }
        else
        {
            KamPay.Helpers.AppLogger.DebugLog("âš ï¸ KAMPAY_API_JWT bulunamadÄ±! Yetki gerektiren endpointler 401 hatasÄ± verebilir.");
        }
    }

    #region IProductQueryService Methods

    public async Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null, CancellationToken cancellationToken = default)
    {
        // GetAllProductsAsync artÄ±k GetProductsPagedAsync'e delege ediyor (ilk sayfa)
        var paged = await GetProductsPagedAsync(20, null, filter, cancellationToken);
        if (paged.Success && paged.Data != null)
            return ServiceResult<List<Product>>.SuccessResult(paged.Data.Items, paged.Message);
        return ServiceResult<List<Product>>.FailureResult(paged.Message, paged.Errors?.ToArray());
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
            return ServiceResult<Product>.FailureResult($"API HatasÄ± ({response.StatusCode})", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API baÄŸlantÄ± hatasÄ±", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId, CancellationToken cancellationToken = default) 
    {
        try
        {
            // API'de /user/{userId} rotasÄ±nÄ±n olduÄŸunu varsayÄ±yoruz
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/{userId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>(_jsonOptions, cancellationToken);
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("ÃœrÃ¼nler yÃ¼klenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatasÄ±", ex.Message);
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
            return ServiceResult<List<Product>>.FailureResult("ÃœrÃ¼nler yÃ¼klenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatasÄ±", ex.Message);
        }
    }

    public async Task<ServiceResult<ProductPagedResponse>> GetProductsPagedAsync(int pageSize = 20, string? cursor = null, ProductFilter? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string> { $"pageSize={pageSize}" };

            if (!string.IsNullOrEmpty(cursor))
                queryParams.Add($"cursor={Uri.EscapeDataString(cursor)}");

            if (!string.IsNullOrEmpty(filter?.CategoryId))
                queryParams.Add($"categoryId={Uri.EscapeDataString(filter.CategoryId)}");

            if (filter?.Type.HasValue == true)
                queryParams.Add($"type={(int)filter.Type.Value}");

            if (!string.IsNullOrEmpty(filter?.SearchText))
                queryParams.Add($"search={Uri.EscapeDataString(filter.SearchText)}");

            var url = $"{_baseUrl}?{string.Join("&", queryParams)}";
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ“„ GetProductsPagedAsync â†’ {url}");

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ProductPagedResponse>(_jsonOptions, cancellationToken);
                return ServiceResult<ProductPagedResponse>.SuccessResult(result ?? new ProductPagedResponse(), "ÃœrÃ¼nler yÃ¼klendi");
            }

            return ServiceResult<ProductPagedResponse>.FailureResult("ÃœrÃ¼nler yÃ¼klenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"âŒ GetProductsPagedAsync hata: {ex.Message}");
            return ServiceResult<ProductPagedResponse>.FailureResult("API baÄŸlantÄ± hatasÄ±", ex.Message);
        }
    }

    public Task<ServiceResult<List<Category>>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // Kategoriler de API'den gelecek ÅŸekilde gÃ¼ncellenecek
        return Task.FromResult(ServiceResult<List<Category>>.SuccessResult(Category.GetDefaultCategories(), "VarsayÄ±lan Kategoriler"));
    }

    /// <summary>
    /// Cache-first yÃ¼kleme iÃ§in: Ã–nbellekteki Ã¼rÃ¼nleri dÃ¶ndÃ¼rÃ¼r. Cache yoksa null.
    /// </summary>
    public async Task<List<Product>?> GetCachedProductsAsync()
    {
        try
        {
            var cached = await _cacheService.GetCachedProductsAsync();
            return cached?.Count > 0 ? cached : null;
        }
        catch
        {
            return null;
        }
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
                return ServiceResult<Product>.FailureResult("GeÃ§ersiz Ã¼rÃ¼n", validation.Errors.ToArray());
            }

            // ğŸ›‘ 1. Resimleri Ä°stemciden (MAUI) DoÄŸrudan Firebase Storage'a YÃ¼kle
            var tempProductId = Guid.NewGuid().ToString(); // Storage'da klasÃ¶r adÄ± veya ileride API'nin kabul edeceÄŸi ID
            var uploadedImageUrls = new List<string>();

            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                var uploadResult = await _imageCoordinator.UploadProductImagesParallelAsync(request.ImagePaths, tempProductId);
                if (!uploadResult.Success)
                {
                    return ServiceResult<Product>.FailureResult("Resimler yÃ¼klenemedi", uploadResult.Message);
                }

                uploadedImageUrls = uploadResult.Data;
            }

            // Ã–rnek eÅŸleme (API 'Product' modelini kabul ediyor)
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

            // GÃ¼venlik headerÄ±
            await SetAuthHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync(_baseUrl, newProduct);

            if (response.IsSuccessStatusCode)
            {
                // API dÃ¶nÃ¼ÅŸÃ¼nde belki farklÄ± ID veya Product gelebilir, duruma gÃ¶re:
                var apiProduct = await response.Content.ReadFromJsonAsync<Product>();
                var finalProduct = apiProduct ?? newProduct;

                // ğŸ›‘ Cache'e ekle
                if (_cacheService != null)
                {
                    await _cacheService.UpdateProductInCacheAsync(finalProduct);
                }

                return ServiceResult<Product>.SuccessResult(finalProduct, "ÃœrÃ¼n API Ã¼zerinden eklendi");
            }
            var authHeaders = response.Headers.WwwAuthenticate.ToString();
            return ServiceResult<Product>.FailureResult("ÃœrÃ¼n eklenemedi", $"{response.StatusCode} - {authHeaders} - {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<Product>.SuccessResult(new Product(), "ÃœrÃ¼n baÅŸarÄ±yla gÃ¼ncellendi");
            }
            return ServiceResult<Product>.FailureResult("ÃœrÃ¼n gÃ¼ncellenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "ÃœrÃ¼n baÅŸarÄ±yla silindi");
            }
            return ServiceResult<bool>.FailureResult("ÃœrÃ¼n silinemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "ÃœrÃ¼n sahibi gÃ¼ncellendi");
            }
            return ServiceResult<bool>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "ÃœrÃ¼n satÄ±ldÄ± olarak iÅŸaretlendi");
            }
            return ServiceResult<bool>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "ÃœrÃ¼n takas edildi olarak iÅŸaretlendi");
            }
            return ServiceResult<bool>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "ÃœrÃ¼n rezervasyon durumu gÃ¼ncellendi");
            }
            return ServiceResult<bool>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> SaveProductDirectlyAsync(Product product) 
    {
        try
        {
            await SetAuthHeaderAsync();
            
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ“¤ SaveProductDirectly â†’ {_baseUrl}");
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ“¤ Product: {product.Title}, UserId: {product.UserId}");
            
            var response = await _httpClient.PostAsJsonAsync(_baseUrl, product);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            KamPay.Helpers.AppLogger.DebugLog($"ğŸ“¥ API YanÄ±t: {response.StatusCode} - {responseContent}");
            
            if (response.IsSuccessStatusCode)
            {
                // API { Message, ProductId } formatÄ±nda dÃ¶nÃ¼yor, Product deÄŸil.
                // Bu yÃ¼zden gÃ¶nderdiÄŸimiz product objesini doÄŸrudan kullanÄ±yoruz.
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (apiResponse.TryGetProperty("ProductId", out var pidProp) ||
                        apiResponse.TryGetProperty("productId", out pidProp))
                    {
                        product.ProductId = pidProp.GetString() ?? product.ProductId;
                    }
                }
                catch { /* API yanÄ±tÄ± parse edilemezse orijinal ProductId'yi kullan */ }

                // Ã–nbelleÄŸe de ekleyelim
                if (_cacheService != null)
                {
                    await _cacheService.UpdateProductInCacheAsync(product);
                }
                return ServiceResult<Product>.SuccessResult(product, "ÃœrÃ¼n doÄŸrudan kaydedildi");
            }

            var authHeaders = response.Headers.WwwAuthenticate.ToString();
            return ServiceResult<Product>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", $"{response.StatusCode} - {authHeaders} - {responseContent}");
        }
        catch (Exception ex)
        {
            KamPay.Helpers.AppLogger.DebugLog($"âŒ SaveProductDirectly hata: {ex.Message}");
            return ServiceResult<Product>.FailureResult("API hatasÄ±", ex.Message);
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
                return ServiceResult<bool>.SuccessResult(true, "KullanÄ±cÄ± bilgileri gÃ¼ncellendi");
            }
            return ServiceResult<bool>.FailureResult("Ä°ÅŸlem baÅŸarÄ±sÄ±z", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
        }
    }

    #endregion

    #region IProductValidationService Methods

    public ValidationResult ValidateProduct(ProductRequest request)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            result.AddError("ÃœrÃ¼n baÅŸlÄ±ÄŸÄ± boÅŸ olamaz");
        }
        else if (request.Title.Length > Constants.MaxProductTitleLength)
        {
            result.AddError($"BaÅŸlÄ±k en fazla {Constants.MaxProductTitleLength} karakter olabilir");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            result.AddError("ÃœrÃ¼n aÃ§Ä±klamasÄ± boÅŸ olamaz");
        }

        if (string.IsNullOrWhiteSpace(request.CategoryId))
        {
            result.AddError("Kategori seÃ§ilmelidir");
        }

        return result;
    }

    public async Task<ServiceResult<bool>> IncrementViewCountAsync(string productId) 
    {
        try
        {
            // View count genelde herkes tarafÄ±ndan artÄ±rÄ±labildiÄŸi iÃ§in token gerekmeyebilir ancak endpoint dizaynÄ±na baÄŸlÄ±dÄ±r.
            // await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsync($"{_baseUrl}/{productId}/incrementview", null);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<bool>.SuccessResult(true);
            }
            return ServiceResult<bool>.FailureResult("GÃ¶rÃ¼ntÃ¼lenme artÄ±rÄ±lamadÄ±", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("API hatasÄ±", ex.Message);
        }
    }

    #endregion
}

