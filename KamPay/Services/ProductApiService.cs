using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
    // Android Emulator için localhost "10.0.2.2" olarak yazılmalıdır. Cihaz / iOS için sunucu IP adresi kullanılmalıdır.
    private readonly string _baseUrl = "https://localhost:7147/api/v1/products"; 
    private readonly IProductImageCoordinator _imageCoordinator;

    public ProductApiService(HttpClient httpClient, IAuthenticationService authService, IProductImageCoordinator imageCoordinator)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _imageCoordinator = imageCoordinator ?? throw new ArgumentNullException(nameof(imageCoordinator));

        System.Diagnostics.Debug.WriteLine("✅ ProductApiService oluşturuldu (API Garsonu devrede)");
    }

    // --- GİZLİ SİLAHIMIZ: İSTEKLERE TOKEN EKLEYEN METOT ---
    private async Task SetAuthHeaderAsync()
    {
        var token = await _authService.GetValidTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            // API'nin kapısındaki [Authorize] duvarını geçmek için Token'ı Header'a ekliyoruz
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    #region IProductQueryService Methods

    public async Task<ServiceResult<List<Product>>> GetAllProductsAsync(ProductFilter? filter = null)
    {
        try
        {
            // Token'a gerek yok, herkes ürünleri görebilir. Direkt API'mizden çekiyoruz.
            var response = await _httpClient.GetAsync(_baseUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                // NOT: API henüz tam Product dönmüyorsa burada mapping gerekebilir.
                // İleride API tamamlandığında doğrudan:
                // var products = await _httpClient.GetFromJsonAsync<List<Product>>(_baseUrl);

                return ServiceResult<List<Product>>.SuccessResult(new List<Product>(), "Ürünler API'den çekildi");
            }

            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API bağlantı hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> GetProductByIdAsync(string productId) 
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/{productId}");
            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<Product>();
                if (product != null)
                {
                    return ServiceResult<Product>.SuccessResult(product);
                }
            }
            return ServiceResult<Product>.FailureResult("Ürün bulunamadı", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<Product>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetUserProductsAsync(string userId) 
    {
        try
        {
            // API'de /user/{userId} rotasının olduğunu varsayıyoruz
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>();
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetProductsAsync(string? categoryId = null, string? searchText = null) 
    {
        try
        {
            var query = $"?categoryId={categoryId}&searchText={searchText}";
            var response = await _httpClient.GetAsync($"{_baseUrl}{query}");
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>();
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<List<Product>>> GetProductsPagedAsync(int pageSize = 20, string? lastKey = null, ProductFilter? filter = null)
    {
        try
        {
            var query = $"?pageSize={pageSize}&lastKey={lastKey}";
            var response = await _httpClient.GetAsync($"{_baseUrl}/paged{query}");
            if (response.IsSuccessStatusCode)
            {
                var products = await response.Content.ReadFromJsonAsync<List<Product>>();
                return ServiceResult<List<Product>>.SuccessResult(products ?? new List<Product>());
            }
            return ServiceResult<List<Product>>.FailureResult("Ürünler yüklenemedi", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<Product>>.FailureResult("API hatası", ex.Message);
        }
    }

    public Task<ServiceResult<List<Category>>> GetCategoriesAsync()
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

            // Örnek eşleme (API 'Product' modelini kabul ediyor)
            var newProduct = new Product
            {
                Title = request.Title,
                Description = request.Description,
                Price = request.Price,
                CategoryId = request.CategoryId ?? string.Empty,
                Condition = request.Condition,
                Type = request.Type,
                Location = request.Location ?? string.Empty,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Güvenlik headerı
            await SetAuthHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync(_baseUrl, newProduct);
            
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<Product>.SuccessResult(newProduct, "Ürün API üzerinden eklendi");
            }
            return ServiceResult<Product>.FailureResult("Ürün eklenemedi", response.ReasonPhrase);
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
            var response = await _httpClient.PatchAsync($"{_baseUrl}/{productId}/marksold", null);
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
            var response = await _httpClient.PatchAsync($"{_baseUrl}/{productId}/markexchanged", null);
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
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/save", product);
            if (response.IsSuccessStatusCode)
            {
                return ServiceResult<Product>.SuccessResult(product, "Ürün doğrudan kaydedildi");
            }
            return ServiceResult<Product>.FailureResult("İşlem başarısız", response.ReasonPhrase);
        }
        catch (Exception ex)
        {
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
