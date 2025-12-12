using KamPay.Models;
using System;
using System.IO;
using System.Linq;

namespace KamPay.Helpers
{
    // Yüklemeden önce resim dosyalarını doğrulamak için yardımcı sınıf

    public static class ImageValidator
    {
        // Maximum file size: 5MB
        public const long MaxFileSizeBytes = 5 * 1024 * 1024;

        // Maximum file size: 10MB for profile images
        public const long MaxProfileImageSizeBytes = 10 * 1024 * 1024;

        // Allowed image file extensions
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // Allowed MIME types
        private static readonly string[] AllowedMimeTypes = 
        { 
            "image/jpeg", 
            "image/png", 
            "image/gif", 
            "image/webp" 
        };

       
        // Dosyanın kabul edilebilir boyut ve formatta geçerli bir resim olup olmadığını doğrular
       

        /// <param name="filePath">Doğrulanacak resim dosyasının yolu</param>

        /// <param name="isProfileImage">Profil resimleri için true olarak ayarlayın (10 MB'a kadar daha büyük dosya boyutuna izin verir)</param>

        /// <returns>Resmin geçerli olup olmadığını gösteren Doğrulama Sonucu</returns>

        public static ValidationResult ValidateImage(string filePath, bool isProfileImage = false)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                result.AddError("Dosya yolu boş olamaz");
                return result;
            }

            if (!File.Exists(filePath))
            {
                result.AddError("Dosya bulunamadı");
                return result;
            }

            // Check file extension
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                result.AddError($"Geçersiz dosya formatı. İzin verilen formatlar: {string.Join(", ", AllowedExtensions)}");
            }

            // Check file size
            try
            {
                var fileInfo = new FileInfo(filePath);
                var maxSize = isProfileImage ? MaxProfileImageSizeBytes : MaxFileSizeBytes;
                
                if (fileInfo.Length > maxSize)
                {
                    var maxSizeMB = maxSize / (1024 * 1024);
                    result.AddError($"Dosya boyutu çok büyük. Maksimum {maxSizeMB}MB olabilir");
                }

                if (fileInfo.Length == 0)
                {
                    result.AddError("Dosya boş olamaz");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Dosya bilgileri alınamadı: {ex.Message}");
            }

            return result;
        }

        // Birden fazla görüntüyü doğrular

        public static ValidationResult ValidateImages(string[] filePaths, int maxImages = 5)
        {
            var result = new ValidationResult();

            if (filePaths == null || filePaths.Length == 0)
            {
                result.AddError("En az bir görsel seçilmelidir");
                return result;
            }

            if (filePaths.Length > maxImages)
            {
                result.AddError($"En fazla {maxImages} görsel eklenebilir");
            }

            foreach (var filePath in filePaths)
            {
                var imageValidation = ValidateImage(filePath);
                if (!imageValidation.IsValid)
                {
                    foreach (var error in imageValidation.Errors)
                    {
                        result.AddError($"{Path.GetFileName(filePath)}: {error}");
                    }
                }
            }

            return result;
        }

        
        // Dosya uzantısının izin verilip verilmediğini kontrol eder

        public static bool IsAllowedExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);
        }

        // MIME türünün izin verilip verilmediğini kontrol eder

        public static bool IsAllowedMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return false;

            return AllowedMimeTypes.Contains(mimeType.ToLowerInvariant());
        }


        // Potansiyel olarak tehlikeli karakterleri kaldırarak güvenli bir dosya adı elde eder

        public static string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return Guid.NewGuid().ToString();

            // Remove path information
            fileName = Path.GetFileName(fileName);

            // Remove invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Remove additional potentially problematic characters
            fileName = fileName.Replace(" ", "_")
                              .Replace("'", "")
                              .Replace("\"", "");

            // Ensure the file name is not too long (max 100 characters before extension)
            var extension = Path.GetExtension(fileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            
            if (nameWithoutExtension.Length > 100)
            {
                nameWithoutExtension = nameWithoutExtension.Substring(0, 100);
            }

            return nameWithoutExtension + extension;
        }

        // Dosya boyutunu insan tarafından okunabilir biçimde biçimlendirir

        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
