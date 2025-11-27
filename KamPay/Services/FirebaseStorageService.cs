using Firebase.Storage;
using KamPay.Helpers;
using KamPay.Models;

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
}
