using KamPay.Helpers;
using KamPay.Models;
using System.Diagnostics;

namespace KamPay.Services;

/// <summary>
/// ?? Mesaj medya koordinatörü implementasyonu
/// ? Single Responsibility: Sadece mesaj medyasýnýn yönetimi
/// ? Delegates to: IStorageService
/// </summary>
public class MessageMediaCoordinator : IMessageMediaCoordinator
{
    private readonly IStorageService _storageService;

    // Dosya boyutu limitleri
    private const long MaxImageSize = 10 * 1024 * 1024; // 10 MB
    private const long MaxVideoSize = 50 * 1024 * 1024; // 50 MB
    private const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

    public MessageMediaCoordinator(IStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task<ServiceResult<string>> UploadMessageImageAsync(string imagePath, string conversationId, string messageId)
    {
        try
        {
            // Dosya kontrolü
            if (!File.Exists(imagePath))
            {
                return ServiceResult<string>.FailureResult("Görsel dosyasý bulunamadý");
            }

            // Boyut kontrolü
            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Length > MaxImageSize)
            {
                return ServiceResult<string>.FailureResult($"Görsel boyutu {MaxImageSize / 1024 / 1024} MB'dan büyük olamaz");
            }

            // Dosya türü kontrolü
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            
            if (!allowedExtensions.Contains(extension))
            {
                return ServiceResult<string>.FailureResult("Sadece resim dosyalarý yüklenebilir");
            }

            Debug.WriteLine($"?? Mesaj görseli yükleniyor: {conversationId}/{messageId}");

            // IStorageService zaten UploadMessageImageAsync metoduna sahip
            var uploadResult = await _storageService.UploadMessageImageAsync(imagePath, conversationId);

            if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Data))
            {
                Debug.WriteLine($"? Mesaj görseli yüklendi: {uploadResult.Data}");
                return ServiceResult<string>.SuccessResult(uploadResult.Data, "Görsel yüklendi");
            }
            else
            {
                Debug.WriteLine($"? Mesaj görseli yüklenemedi: {uploadResult.Message}");
                return ServiceResult<string>.FailureResult("Görsel yüklenemedi", uploadResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadMessageImageAsync hatasý: {ex.Message}");
            return ServiceResult<string>.FailureResult("Görsel yüklenirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<string>> UploadMessageVideoAsync(string videoPath, string conversationId, string messageId)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                return ServiceResult<string>.FailureResult("Video dosyasý bulunamadý");
            }

            var fileInfo = new FileInfo(videoPath);
            if (fileInfo.Length > MaxVideoSize)
            {
                return ServiceResult<string>.FailureResult($"Video boyutu {MaxVideoSize / 1024 / 1024} MB'dan büyük olamaz");
            }

            var extension = Path.GetExtension(videoPath).ToLowerInvariant();
            var allowedExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

            if (!allowedExtensions.Contains(extension))
            {
                return ServiceResult<string>.FailureResult("Sadece video dosyalarý yüklenebilir");
            }

            Debug.WriteLine($"?? Mesaj videosu yükleniyor: {conversationId}/{messageId}");

            // ?? Video için UploadMessageImageAsync kullanýyoruz (IStorageService'de video metodu yok)
            var uploadResult = await _storageService.UploadMessageImageAsync(videoPath, conversationId);

            if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Data))
            {
                Debug.WriteLine($"? Mesaj videosu yüklendi: {uploadResult.Data}");
                return ServiceResult<string>.SuccessResult(uploadResult.Data, "Video yüklendi");
            }
            else
            {
                Debug.WriteLine($"? Mesaj videosu yüklenemedi: {uploadResult.Message}");
                return ServiceResult<string>.FailureResult("Video yüklenemedi", uploadResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadMessageVideoAsync hatasý: {ex.Message}");
            return ServiceResult<string>.FailureResult("Video yüklenirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<string>> UploadMessageFileAsync(string filePath, string conversationId, string messageId)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return ServiceResult<string>.FailureResult("Dosya bulunamadý");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                return ServiceResult<string>.FailureResult($"Dosya boyutu {MaxFileSize / 1024 / 1024} MB'dan büyük olamaz");
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar" };

            if (!allowedExtensions.Contains(extension))
            {
                return ServiceResult<string>.FailureResult("Bu dosya türü desteklenmiyor");
            }

            Debug.WriteLine($"?? Mesaj dosyasý yükleniyor: {conversationId}/{messageId}");

            // ?? Dosya için de UploadMessageImageAsync kullanýyoruz (IStorageService'de file metodu yok)
            var uploadResult = await _storageService.UploadMessageImageAsync(filePath, conversationId);

            if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Data))
            {
                Debug.WriteLine($"? Mesaj dosyasý yüklendi: {uploadResult.Data}");
                return ServiceResult<string>.SuccessResult(uploadResult.Data, "Dosya yüklendi");
            }
            else
            {
                Debug.WriteLine($"? Mesaj dosyasý yüklenemedi: {uploadResult.Message}");
                return ServiceResult<string>.FailureResult("Dosya yüklenemedi", uploadResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? UploadMessageFileAsync hatasý: {ex.Message}");
            return ServiceResult<string>.FailureResult("Dosya yüklenirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeleteMessageMediaAsync(string mediaUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek medya yok");
            }

            Debug.WriteLine($"??? Mesaj medyasý siliniyor: {mediaUrl}");

            var deleteResult = await _storageService.DeleteImageAsync(mediaUrl);

            if (deleteResult.Success)
            {
                Debug.WriteLine($"? Mesaj medyasý silindi");
                return ServiceResult<bool>.SuccessResult(true, "Medya silindi");
            }
            else
            {
                Debug.WriteLine($"?? Mesaj medyasý silinemedi: {deleteResult.Message}");
                return ServiceResult<bool>.FailureResult("Medya silinemedi", deleteResult.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? DeleteMessageMediaAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Medya silinirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeleteMessageMediaBatchAsync(List<string> mediaUrls)
    {
        try
        {
            if (mediaUrls == null || !mediaUrls.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek medya yok");
            }

            Debug.WriteLine($"??? {mediaUrls.Count} mesaj medyasý toplu siliniyor");

            var deleteTasks = mediaUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => _storageService.DeleteImageAsync(url))
                .ToList();

            if (!deleteTasks.Any())
            {
                return ServiceResult<bool>.SuccessResult(true, "Silinecek geçerli medya yok");
            }

            var results = await Task.WhenAll(deleteTasks);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Length - successCount;

            if (failCount == 0)
            {
                Debug.WriteLine($"? {successCount} medya baþarýyla silindi");
                return ServiceResult<bool>.SuccessResult(true, $"{successCount} medya silindi");
            }
            else
            {
                Debug.WriteLine($"?? {successCount} baþarýlý, {failCount} baþarýsýz silme");
                return ServiceResult<bool>.SuccessResult(
                    true,
                    $"{successCount} medya silindi, {failCount} baþarýsýz");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? DeleteMessageMediaBatchAsync hatasý: {ex.Message}");
            return ServiceResult<bool>.FailureResult("Medya silinirken hata oluþtu", ex.Message);
        }
    }

    public async Task<ServiceResult<string>> CreateImageThumbnailAsync(string imagePath)
    {
        try
        {
            // Bu metot opsiyonel - gelecekte görsel thumbnail oluþturma için kullanýlabilir
            // Þimdilik orijinal görseli döndür
            await Task.CompletedTask; // Async pattern için
            
            Debug.WriteLine($"?? Thumbnail oluþturma henüz implemente edilmedi");
            return ServiceResult<string>.SuccessResult(imagePath, "Orijinal görsel kullanýlýyor");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? CreateImageThumbnailAsync hatasý: {ex.Message}");
            return ServiceResult<string>.FailureResult("Thumbnail oluþturulamadý", ex.Message);
        }
    }
}
