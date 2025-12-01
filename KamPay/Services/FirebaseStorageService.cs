using Firebase.Storage;
using KamPay.Helpers;
using KamPay.Models;
using SkiaSharp;

namespace KamPay.Services;

public class FirebaseStorageService : IStorageService
{
    private readonly FirebaseStorage _storage;

    public FirebaseStorageService()
    {
        // Firebase Storage bucket URL
        _storage = new FirebaseStorage("kampay-b006d.firebasestorage.app");
    }

    public async Task<ServiceResult<string>> UploadProductImageAsync(string localPath, string productId, int imageIndex)
    {
        try
        {
            // Dosya var mı kontrol et
            if (!File.Exists(localPath))
            {
                return ServiceResult<string>.FailureResult(
                    "Dosya bulunamadı",
                    "Seçilen görsel dosyası bulunamadı"
                );
            }

            // Dosya boyutu kontrolü
            var fileSize = await GetFileSizeAsync(localPath);
            if (fileSize > Constants.MaxImageSizeBytes)
            {
                return ServiceResult<string>.FailureResult(
                    "Dosya çok büyük",
                    $"Maksimum dosya boyutu {Constants.MaxImageSizeBytes / (1024 * 1024)} MB olabilir"
                );
            }

            // Dosya uzantısını al
            var extension = Path.GetExtension(localPath);
            var fileName = $"{productId}_{imageIndex}_{Guid.NewGuid()}{extension}";

            // Firebase Storage'a yükle
            await using var stream = File.OpenRead(localPath);
            var downloadUrl = await _storage
            .Child(Constants.ProductImagesFolder)
            .Child(productId)
            .Child(fileName)
            .PutAsync(stream);

            return ServiceResult<string>.SuccessResult(downloadUrl, "Görsel başarıyla yüklendi");
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.FailureResult(
                "Görsel yüklenemedi",
                ex.Message
            );
        }
    }

    public async Task<ServiceResult<string>> UploadProfileImageAsync(string localPath, string userId)
    {
        try
        {
            if (!File.Exists(localPath))
            {
                return ServiceResult<string>.FailureResult("Dosya bulunamadı");
            }

            var fileSize = await GetFileSizeAsync(localPath);
            if (fileSize > Constants.MaxImageSizeBytes)
            {
                return ServiceResult<string>.FailureResult(
                    "Dosya çok büyük",
                    $"Maksimum {Constants.MaxImageSizeBytes / (1024 * 1024)} MB"
                );
            }

            var extension = Path.GetExtension(localPath);
            var fileName = $"{userId}_profile{Path.GetExtension(localPath)}";

            await using var stream = File.OpenRead(localPath);

            var downloadUrl = await _storage
                .Child(Constants.ProfileImagesFolder)
                .Child(fileName)
                .PutAsync(stream);

            return ServiceResult<string>.SuccessResult(downloadUrl);
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.FailureResult("Yükleme hatası", ex.Message);
        }
    }

    public async Task<ServiceResult<string>> UploadMessageImageAsync(string localPath, string conversationId)
    {
        try
        {
            // Dosya var mı kontrol et
            if (!File.Exists(localPath))
            {
                return ServiceResult<string>.FailureResult(
                    "Dosya bulunamadı",
                    "Seçilen görsel dosyası bulunamadı"
                );
            }

            // Dosya boyutu kontrolü - Mesaj görselleri için 1MB limit
            var fileSize = await GetFileSizeAsync(localPath);
            const long maxMessageImageSize = 1 * 1024 * 1024; // 1 MB
            if (fileSize > maxMessageImageSize)
            {
                return ServiceResult<string>.FailureResult(
                    "Dosya çok büyük",
                    "Maksimum 1 MB olabilir"
                );
            }

            // Dosya uzantısını al ve unique dosya adı oluştur
            var extension = Path.GetExtension(localPath);
            var fileName = $"msg_{Guid.NewGuid()}{extension}";

            // Firebase Storage'a yükle
            await using var stream = File.OpenRead(localPath);
            var downloadUrl = await _storage
                .Child(Constants.MessageImagesFolder)
                .Child(conversationId)
                .Child(fileName)
                .PutAsync(stream);

            return ServiceResult<string>.SuccessResult(downloadUrl, "Görsel başarıyla yüklendi");
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.FailureResult(
                "Görsel yüklenemedi",
                ex.Message
            );
        }
    }

    public async Task<ServiceResult<bool>> DeleteImageAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return ServiceResult<bool>.FailureResult("Geçersiz URL");
            }

            // 1️⃣ URI nesnesine çevir
            var uri = new Uri(imageUrl);

            // 2️⃣ /o/ kısmından sonrasını al (Firebase dosya yolu bu kısımda)
            var rawPath = uri.AbsolutePath.Split("/o/").Last();

            // 3️⃣ URL encode çöz (ör: %2F → /)
            var path = Uri.UnescapeDataString(rawPath);

            // Artık path şöyle olacak:
            // product_images/2fa921e7-a3c2-4f61-ade4-98d4a3cc3d11/2fa921e7-a3c2-4f61-ade4-98d4a3cc3d11_0_c7dc7ccc-de0d-42ca-ae42-4d5ce44d5764.jpg

            // 4️⃣ Firebase Storage’dan sil
            await _storage.Child(path).DeleteAsync();

            return ServiceResult<bool>.SuccessResult(true, "Görsel silindi");
        }

        catch (Exception ex)
        {
            return ServiceResult<bool>.FailureResult("Silme hatası", ex.Message);
        }
    }

    public async Task<long> GetFileSizeAsync(string localPath)
    {
        return await Task.Run(() =>
        {
            var fileInfo = new FileInfo(localPath);
            return fileInfo.Length;
        });
    }

    // FAZ 2: Teslimat fotoğrafı yükleme metodları

    /// <summary>
    /// Teslimat fotoğrafını sıkıştırır, thumbnail oluşturur ve Firebase Storage'a yükler
    /// </summary>
    public async Task<ServiceResult<DeliveryPhotoUploadResult>> UploadDeliveryPhotoAsync(
        byte[] photoData, string transactionId, string qrCodeId, string userId)
    {
        try
        {
            // 1. Önce orijinal boyutları al (sıkıştırmadan önce)
            int width, height;
            using (var originalImage = SKBitmap.Decode(photoData))
            {
                if (originalImage == null)
                {
                    return ServiceResult<DeliveryPhotoUploadResult>.FailureResult(
                        "Geçersiz fotoğraf", 
                        "Fotoğraf dosyası okunamadı veya bozuk");
                }
                width = originalImage.Width;
                height = originalImage.Height;
            }
            
            // 2. Fotoğrafı sıkıştır (max 1MB)
            var compressedData = await CompressPhotoAsync(photoData, 1048576);
            
            // 3. Thumbnail oluştur (200x200)
            var thumbnailData = await CreateThumbnailAsync(photoData, 200);
            
            // 4. Dosya adları oluştur
            var timestamp = DateTime.UtcNow.Ticks;
            var fullFileName = $"{qrCodeId}_{timestamp}_full.jpg";
            var thumbFileName = $"{qrCodeId}_{timestamp}_thumb.jpg";
            
            // 5. Paralel olarak yükle
            var uploadTasks = new[]
            {
                UploadPhotoToStorageAsync(compressedData, transactionId, fullFileName),
                UploadPhotoToStorageAsync(thumbnailData, transactionId, thumbFileName)
            };
            
            var results = await Task.WhenAll(uploadTasks);
            
            if (!results[0].Success || !results[1].Success)
            {
                return ServiceResult<DeliveryPhotoUploadResult>.FailureResult(
                    "Fotoğraf yüklenemedi", 
                    results[0].Message ?? results[1].Message);
            }
            
            var result = new DeliveryPhotoUploadResult
            {
                FullPhotoUrl = results[0].Data!,
                ThumbnailUrl = results[1].Data!,
                FileSize = compressedData.Length,
                Width = width,
                Height = height
            };
            
            return ServiceResult<DeliveryPhotoUploadResult>.SuccessResult(result, "Fotoğraf başarıyla yüklendi");
        }
        catch (Exception ex)
        {
            return ServiceResult<DeliveryPhotoUploadResult>.FailureResult(
                "Fotoğraf işleme hatası", ex.Message);
        }
    }

    /// <summary>
    /// Fotoğrafı belirtilen maksimum boyuta sıkıştırır
    /// JPEG kalitesi: 90 → 20 arasında azaltılarak hedef boyuta ulaşılır
    /// </summary>
    public async Task<byte[]> CompressPhotoAsync(byte[] photoData, int maxSizeBytes = 1048576)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(photoData);
            using var bitmap = SKBitmap.Decode(inputStream);
            
            if (bitmap == null)
                throw new InvalidOperationException("Fotoğraf decode edilemedi");
            
            var quality = 90;
            byte[] compressed;
            
            // Sıkıştır: kaliteyi 10'ar 10'ar azalt, hedef boyuta veya min kaliteye ulaşana kadar
            // Not: Her iterasyonda yeni MemoryStream oluşturulması kasıtlıdır - 
            // modern GC kısa ömürlü küçük nesneleri verimli yönetir
            do
            {
                using var outputStream = new MemoryStream();
                bitmap.Encode(outputStream, SKEncodedImageFormat.Jpeg, quality);
                compressed = outputStream.ToArray();
                
                // Hedef boyuta ulaştık veya minimum kaliteye indik
                if (compressed.Length <= maxSizeBytes || quality <= 20)
                    break;
                
                quality -= 10;
            } while (compressed.Length > maxSizeBytes && quality > 20);
            
            return compressed;
        });
    }

    /// <summary>
    /// Thumbnail oluşturur (aspect ratio korunur)
    /// </summary>
    public async Task<byte[]> CreateThumbnailAsync(byte[] photoData, int size = 200)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(photoData);
            using var bitmap = SKBitmap.Decode(inputStream);
            
            if (bitmap == null)
                throw new InvalidOperationException("Fotoğraf decode edilemedi");
            
            // Aspect ratio'yu koru
            var aspectRatio = (float)bitmap.Width / bitmap.Height;
            int newWidth, newHeight;
            
            if (aspectRatio > 1)
            {
                newWidth = size;
                newHeight = (int)(size / aspectRatio);
            }
            else
            {
                newHeight = size;
                newWidth = (int)(size * aspectRatio);
            }
            
            using var resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
            using var outputStream = new MemoryStream();
            resized.Encode(outputStream, SKEncodedImageFormat.Jpeg, 85);
            
            return outputStream.ToArray();
        });
    }

    /// <summary>
    /// Firebase Storage'a fotoğraf yükler
    /// </summary>
    private async Task<ServiceResult<string>> UploadPhotoToStorageAsync(
        byte[] photoData, string transactionId, string fileName)
    {
        try
        {
            using var stream = new MemoryStream(photoData);
            var downloadUrl = await _storage
                .Child(Constants.DeliveryPhotosFolder)
                .Child(transactionId)
                .Child(fileName)
                .PutAsync(stream);
            
            return ServiceResult<string>.SuccessResult(downloadUrl);
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.FailureResult("Yükleme hatası", ex.Message);
        }
    }
}
