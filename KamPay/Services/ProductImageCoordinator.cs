using KamPay.Models;
using KamPay.Helpers;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// Ürün görsellerinin yüklenmesini koordine eder.
/// ? Single Responsibility: Sadece görsel yükleme iþlemleri
/// ? Dependency Inversion: IStorageService arayüzüne baðýmlý
/// </summary>
public class ProductImageCoordinator : IProductImageCoordinator
{
    private readonly IStorageService _storageService;

    public ProductImageCoordinator(IStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    /// <summary>
    /// Birden fazla ürün görselini sýrayla yükler.
    /// </summary>
    public async Task<ServiceResult<List<string>>> UploadProductImagesAsync(
        List<string> imagePaths, 
        string productId)
    {
        try
        {
            if (imagePaths == null || !imagePaths.Any())
            {
                return ServiceResult<List<string>>.FailureResult("Yüklenecek görsel bulunamadý");
            }

            if (string.IsNullOrWhiteSpace(productId))
            {
                return ServiceResult<List<string>>.FailureResult("Geçersiz ürün ID'si");
            }

            var imageUrls = new List<string>();
            var maxImages = Math.Min(imagePaths.Count, Constants.MaxProductImages);

            Debug.WriteLine($"?? {maxImages} görsel yüklenecek (ProductId: {productId})");

            for (int i = 0; i < maxImages; i++)
            {
                var imagePath = imagePaths[i];
                
                // Dosya kontrolü
                if (!File.Exists(imagePath))
                {
                    Debug.WriteLine($"?? Görsel bulunamadý: {imagePath}");
                    continue;
                }

                var uploadResult = await _storageService.UploadProductImageAsync(
                    imagePath, 
                    productId, 
                    i);

                if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Data))
                {
                    imageUrls.Add(uploadResult.Data);
                    Debug.WriteLine($"? Görsel {i + 1}/{maxImages} yüklendi");
                }
                else
                {
                    Debug.WriteLine($"?? Görsel {i + 1} yüklenemedi: {uploadResult.Message}");
                }
            }

            if (!imageUrls.Any())
            {
                return ServiceResult<List<string>>.FailureResult(
                    "Hiçbir görsel yüklenemedi", 
                    "Tüm görsel yükleme denemeleri baþarýsýz oldu");
            }

            Debug.WriteLine($"? Toplam {imageUrls.Count} görsel baþarýyla yüklendi");
            return ServiceResult<List<string>>.SuccessResult(
                imageUrls, 
                $"{imageUrls.Count} görsel baþarýyla yüklendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadProductImages hatasý: {ex.Message}");
            return ServiceResult<List<string>>.FailureResult(
                "Görsel yükleme baþarýsýz", 
                ex.Message);
        }
    }

    /// <summary>
    /// Tek bir ürün görselini yükler.
    /// </summary>
    public async Task<ServiceResult<string>> UploadSingleProductImageAsync(
        string imagePath,
        string productId,
        int imageIndex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return ServiceResult<string>.FailureResult("Görsel yolu boþ");
            }

            if (!File.Exists(imagePath))
            {
                return ServiceResult<string>.FailureResult("Görsel dosyasý bulunamadý");
            }

            var result = await _storageService.UploadProductImageAsync(
                imagePath, 
                productId, 
                imageIndex);

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadSingleProductImage hatasý: {ex.Message}");
            return ServiceResult<string>.FailureResult("Görsel yüklenemedi", ex.Message);
        }
    }

    /// <summary>
    /// ?? PERFORMANS: Görselleri paralel olarak yükler.
    /// Not: Firebase Storage concurrent upload limitini aþmamak için maxParallelism kullanýlýr.
    /// </summary>
    public async Task<ServiceResult<List<string>>> UploadProductImagesParallelAsync(
        List<string> imagePaths,
        string productId,
        int maxParallelism = 3)
    {
        try
        {
            if (imagePaths == null || !imagePaths.Any())
            {
                return ServiceResult<List<string>>.FailureResult("Yüklenecek görsel bulunamadý");
            }

            var maxImages = Math.Min(imagePaths.Count, Constants.MaxProductImages);
            var validPaths = imagePaths
                .Take(maxImages)
                .Where(File.Exists)
                .ToList();

            if (!validPaths.Any())
            {
                return ServiceResult<List<string>>.FailureResult("Geçerli görsel dosyasý bulunamadý");
            }

            Debug.WriteLine($"?? {validPaths.Count} görsel paralel yükleniyor (max {maxParallelism} eþ zamanlý)");

            // SemaphoreSlim ile concurrent upload sayýsýný sýnýrla
            using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            var uploadTasks = new List<Task<(int index, ServiceResult<string> result)>>();

            for (int i = 0; i < validPaths.Count; i++)
            {
                var index = i;
                var imagePath = validPaths[i];

                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var result = await _storageService.UploadProductImageAsync(
                            imagePath, 
                            productId, 
                            index);
                        return (index, result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                uploadTasks.Add(task);
            }

            var results = await Task.WhenAll(uploadTasks);

            // Baþarýlý yüklemeleri topla (sýralý)
            var imageUrls = results
                .Where(r => r.result.Success && !string.IsNullOrEmpty(r.result.Data))
                .OrderBy(r => r.index)
                .Select(r => r.result.Data!)
                .ToList();

            if (!imageUrls.Any())
            {
                return ServiceResult<List<string>>.FailureResult("Hiçbir görsel yüklenemedi");
            }

            Debug.WriteLine($"? {imageUrls.Count}/{validPaths.Count} görsel paralel yükleme tamamlandý");
            return ServiceResult<List<string>>.SuccessResult(
                imageUrls,
                $"{imageUrls.Count} görsel baþarýyla yüklendi");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadProductImagesParallel hatasý: {ex.Message}");
            return ServiceResult<List<string>>.FailureResult("Paralel yükleme baþarýsýz", ex.Message);
        }
    }

    /// <summary>
    /// ? YENÝ: Birden fazla ürün görselini Firebase Storage'dan siler.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteProductImagesAsync(List<string> imageUrls)
    {
        try
        {
            if (imageUrls == null || !imageUrls.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek görsel yok");
            }

            Debug.WriteLine($"??? {imageUrls.Count} görsel silinecek");

            var deleteTasks = imageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => _storageService.DeleteImageAsync(url))
                .ToList();

            if (!deleteTasks.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek geçerli görsel yok");
            }

            var results = await Task.WhenAll(deleteTasks);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Length - successCount;

            if (failCount == 0)
            {
                Debug.WriteLine($"? {successCount} görsel baþarýyla silindi");
                return ServiceResult<bool>.SuccessResult(true, $"{successCount} görsel silindi");
            }
            else
            {
                Debug.WriteLine($"?? {successCount} baþarýlý, {failCount} baþarýsýz silme");
                return ServiceResult<bool>.SuccessResult(
                    true, 
                    $"{successCount} görsel silindi, {failCount} baþarýsýz");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? DeleteProductImages hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Görseller silinemedi", ex.Message);
        }
    }

    /// <summary>
    /// ? YENÝ: Tek bir ürün görselini Firebase Storage'dan siler.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteSingleProductImageAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek görsel yok");
            }

            var result = await _storageService.DeleteImageAsync(imageUrl);
            
            if (result.Success)
            {
                Debug.WriteLine($"? Görsel silindi: {imageUrl}");
            }
            else
            {
                Debug.WriteLine($"?? Görsel silinemedi: {result.Message}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? DeleteSingleProductImage hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Görsel silinemedi", ex.Message);
        }
    }
}
