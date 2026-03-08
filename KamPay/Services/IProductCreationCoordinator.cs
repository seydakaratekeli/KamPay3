using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Ürün oluþturma sürecini koordine eder
/// Single Responsibility: Sadece yeni ürün oluþturma iþ akýþý
/// </summary>
public interface IProductCreationCoordinator
{
    /// <summary>
    /// Validasyon + Görsel Yükleme + Firebase Kayýt sürecini koordine eder
    /// </summary>
    /// <param name="request">Ürün talep bilgileri</param>
    /// <param name="currentUser">Mevcut kullanýcý</param>
    /// <returns>Oluþturulan ürün</returns>
    Task<ServiceResult<Product>> CreateProductAsync(ProductRequest request, User currentUser);

    /// <summary>
    /// Ürün güncelleme sürecini koordine eder
    /// </summary>
    Task<ServiceResult<Product>> UpdateProductAsync(string productId, ProductRequest request);

    /// <summary>
    /// Ürün taslaðý oluþturur (görseller henüz yüklenmemiþ)
    /// </summary>
    Task<ServiceResult<Product>> CreateProductDraftAsync(ProductRequest request, User currentUser);

    /// <summary>
    /// Taslak ürüne görselleri ekler ve yayýnlar
    /// </summary>
    Task<ServiceResult<Product>> PublishProductDraftAsync(string productId, List<string> imagePaths);
}
