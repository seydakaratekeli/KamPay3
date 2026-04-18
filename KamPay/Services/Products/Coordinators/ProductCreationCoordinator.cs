using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Products;
using KamPay.Services.Products.Coordinators;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? Ürün oluþturma koordinatörü
/// ? Single Responsibility: Sadece ürün oluþturma iþ akýþýný yönetir
/// ? Orchestrates: IProductService, IProductImageCoordinator, Validation
/// </summary>
public class ProductCreationCoordinator : IProductCreationCoordinator
{
    private readonly IProductService _productService;
    private readonly IProductImageCoordinator _imageCoordinator;

    public ProductCreationCoordinator(
        IProductService productService,
        IProductImageCoordinator imageCoordinator)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _imageCoordinator = imageCoordinator ?? throw new ArgumentNullException(nameof(imageCoordinator));
    }

    public async Task<ServiceResult<Product>> CreateProductAsync(ProductRequest request, User currentUser)
    {
        try
        {
            // 1. Validasyon
            var validation = _productService.ValidateProduct(request);
            if (!validation.IsValid)
            {
                Debug.WriteLine($"? Validasyon hatasý: {string.Join(", ", validation.Errors)}");
                return ServiceResult<Product>.FailureResult("Ürün bilgileri geçersiz", validation.Errors.ToArray());
            }

            // 2. Kategori bilgisi al
            var categories = await _productService.GetCategoriesAsync();
            var categoryName = categories.Data?.FirstOrDefault(c => c.CategoryId == request.CategoryId)?.Name ?? "Bilinmeyen";

            // 3. Product nesnesi oluþtur
            var product = new Product
            {
                ProductId = Guid.NewGuid().ToString(),
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

            // 4. Görselleri yükle
            if (request.ImagePaths != null && request.ImagePaths.Any())
            {
                Debug.WriteLine($"?? {request.ImagePaths.Count} görsel yüklenecek");
                
                var uploadResult = await _imageCoordinator.UploadProductImagesAsync(
                    request.ImagePaths,
                    product.ProductId);

                if (uploadResult.Success && uploadResult.Data != null && uploadResult.Data.Any())
                {
                    product.ImageUrls = uploadResult.Data;
                    product.ThumbnailUrl = uploadResult.Data.First();
                    Debug.WriteLine($"? {uploadResult.Data.Count} görsel yüklendi");
                }
                else
                {
                    Debug.WriteLine($"? Görsel yükleme hatasý: {uploadResult.Message}");
                    return ServiceResult<Product>.FailureResult("Görseller yüklenemedi", uploadResult.Message);
                }
            }

            // 5. Firebase'e kaydet
            var saveResult = await _productService.SaveProductDirectlyAsync(product);
            
            if (saveResult.Success)
            {
                Debug.WriteLine($"? Ürün baþarýyla oluþturuldu: {product.ProductId}");
                return ServiceResult<Product>.SuccessResult(product, "Ürün baþarýyla eklendi!");
            }
            else
            {
                Debug.WriteLine($"? Ürün kaydedilemedi: {saveResult.Message}");
                return ServiceResult<Product>.FailureResult("Ürün kaydedilemedi", saveResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateProductAsync hatasý: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Ürün oluþturulurken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> UpdateProductAsync(string productId, ProductRequest request)
    {
        try
        {
            // Validasyon
            var validation = _productService.ValidateProduct(request);
            if (!validation.IsValid)
            {
                return ServiceResult<Product>.FailureResult("Ürün bilgileri geçersiz", validation.Errors.ToArray());
            }

            // Koordinatör pattern: Güncellemeleri sýrasýyla yönet
            var updateResult = await _productService.UpdateProductAsync(productId, request);

            if (updateResult.Success)
            {
                Debug.WriteLine($"? Ürün güncellendi: {productId}");
            }
            else
            {
                Debug.WriteLine($"? Ürün güncellenemedi: {updateResult.Message}");
            }

            return updateResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UpdateProductAsync hatasý: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Ürün güncellenirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> CreateProductDraftAsync(ProductRequest request, User currentUser)
    {
        try
        {
            // Taslak oluþturma: Görseller olmadan ürün kaydý
            var validation = _productService.ValidateProduct(request);
            if (!validation.IsValid)
            {
                return ServiceResult<Product>.FailureResult("Ürün bilgileri geçersiz", validation.Errors.ToArray());
            }

            var categories = await _productService.GetCategoriesAsync();
            var categoryName = categories.Data?.FirstOrDefault(c => c.CategoryId == request.CategoryId)?.Name ?? "Bilinmeyen";

            var product = new Product
            {
                ProductId = Guid.NewGuid().ToString(),
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
                IsActive = false, // ?? Taslak olduðu için pasif
                IsSold = false,
                IsReserved = false,
                CreatedAt = DateTime.UtcNow
            };

            var saveResult = await _productService.SaveProductDirectlyAsync(product);

            if (saveResult.Success)
            {
                Debug.WriteLine($"? Taslak ürün oluþturuldu: {product.ProductId}");
                return ServiceResult<Product>.SuccessResult(product, "Taslak kaydedildi");
            }
            else
            {
                Debug.WriteLine($"? Taslak kaydedilemedi: {saveResult.Message}");
                return ServiceResult<Product>.FailureResult("Taslak kaydedilemedi", saveResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateProductDraftAsync hatasý: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Taslak oluþturulamadý", ex.Message);
        }
    }

    public async Task<ServiceResult<Product>> PublishProductDraftAsync(string productId, List<string> imagePaths)
    {
        try
        {
            // Taslaðý getir
            var productResult = await _productService.GetProductByIdAsync(productId);
            if (!productResult.Success || productResult.Data == null)
            {
                return ServiceResult<Product>.FailureResult("Taslak bulunamadý");
            }

            var product = productResult.Data;

            // Görselleri yükle
            if (imagePaths != null && imagePaths.Any())
            {
                Debug.WriteLine($"?? {imagePaths.Count} görsel yüklenecek (taslak için)");

                var uploadResult = await _imageCoordinator.UploadProductImagesAsync(imagePaths, productId);

                if (uploadResult.Success && uploadResult.Data != null && uploadResult.Data.Any())
                {
                    product.ImageUrls = uploadResult.Data;
                    product.ThumbnailUrl = uploadResult.Data.First();
                    Debug.WriteLine($"? {uploadResult.Data.Count} görsel yüklendi");
                }
                else
                {
                    return ServiceResult<Product>.FailureResult("Görseller yüklenemedi", uploadResult.Message);
                }
            }

            // Aktif hale getir ve kaydet
            product.IsActive = true;
            product.UpdatedAt = DateTime.UtcNow;

            var saveResult = await _productService.SaveProductDirectlyAsync(product);

            if (saveResult.Success)
            {
                Debug.WriteLine($"? Taslak yayýnlandý: {productId}");
                return ServiceResult<Product>.SuccessResult(product, "Ürün yayýnlandý!");
            }
            else
            {
                return ServiceResult<Product>.FailureResult("Yayýnlanamadý", saveResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? PublishProductDraftAsync hatasý: {ex.Message}");
            return ServiceResult<Product>.FailureResult("Yayýnlanamadý", ex.Message);
        }
    }
}
