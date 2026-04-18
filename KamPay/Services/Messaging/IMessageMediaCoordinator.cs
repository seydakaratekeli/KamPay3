using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Mesajlaþma medya koordinatörü
/// Single Responsibility: Sadece mesaj görselleri/medyasýnýn yönetimi
/// </summary>
public interface IMessageMediaCoordinator
{
    /// <summary>
    /// Mesaj için görsel yükler
    /// </summary>
    /// <param name="imagePath">Yerel görsel yolu</param>
    /// <param name="conversationId">Konuþma ID</param>
    /// <param name="messageId">Mesaj ID</param>
    /// <returns>Yüklenen görselin URL'si</returns>
    Task<ServiceResult<string>> UploadMessageImageAsync(string imagePath, string conversationId, string messageId);

    /// <summary>
    /// Mesaj için video yükler
    /// </summary>
    Task<ServiceResult<string>> UploadMessageVideoAsync(string videoPath, string conversationId, string messageId);

    /// <summary>
    /// Mesaj için dosya yükler
    /// </summary>
    Task<ServiceResult<string>> UploadMessageFileAsync(string filePath, string conversationId, string messageId);

    /// <summary>
    /// Mesaj medyasýný siler
    /// </summary>
    Task<ServiceResult<bool>> DeleteMessageMediaAsync(string mediaUrl);

    /// <summary>
    /// Birden fazla medyayý toplu siler
    /// </summary>
    Task<ServiceResult<bool>> DeleteMessageMediaBatchAsync(List<string> mediaUrls);

    /// <summary>
    /// Görsel thumbnail'i oluþturur (optimize edilmiþ küçük görsel)
    /// </summary>
    Task<ServiceResult<string>> CreateImageThumbnailAsync(string imagePath);
}
