using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// Ürün görsellerinin yüklenmesini koordine eder.
/// Single Responsibility: Sadece ürün görseli yükleme iþlemleri.
/// </summary>
public interface IProductImageCoordinator
{
    /// <summary>
    /// Birden fazla ürün görselini Firebase Storage'a yükler.
    /// </summary>
    /// <param name="imagePaths">Yerel görsel dosya yollarý</param>
    /// <param name="productId">Ürün ID'si (Storage path için)</param>
    /// <returns>Yüklenen görsellerin URL'leri</returns>
    Task<ServiceResult<List<string>>> UploadProductImagesAsync(
        List<string> imagePaths, 
        string productId);

    /// <summary>
    /// Tek bir ürün görselini yükler.
    /// </summary>
    Task<ServiceResult<string>> UploadSingleProductImageAsync(
        string imagePath,
        string productId,
        int imageIndex);

    /// <summary>
    /// Ürün görsellerini paralel olarak yükler (performans optimizasyonu).
    /// </summary>
    Task<ServiceResult<List<string>>> UploadProductImagesParallelAsync(
        List<string> imagePaths,
        string productId,
        int maxParallelism = 3);

    /// <summary>
    /// ? YENÝ: Birden fazla ürün görselini Firebase Storage'dan siler.
    /// </summary>
    /// <param name="imageUrls">Silinecek görsellerin URL'leri</param>
    /// <returns>Silme iþlemi sonucu</returns>
    Task<ServiceResult<bool>> DeleteProductImagesAsync(List<string> imageUrls);

    /// <summary>
    /// ? YENÝ: Tek bir ürün görselini Firebase Storage'dan siler.
    /// </summary>
    /// <param name="imageUrl">Silinecek görselin URL'si</param>
    /// <returns>Silme iþlemi sonucu</returns>
    Task<ServiceResult<bool>> DeleteSingleProductImageAsync(string imageUrl);
}
