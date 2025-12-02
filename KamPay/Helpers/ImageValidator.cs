using System;
using System.IO;
using System.Linq;

namespace KamPay.Helpers
{
    /// <summary>
    /// Helper class for validating image files before upload
    /// </summary>
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

        /// <summary>
        /// Validates if the file is a valid image with acceptable size and format
        /// </summary>
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

        /// <summary>
        /// Validates multiple images
        /// </summary>
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

        /// <summary>
        /// Checks if the file extension is allowed
        /// </summary>
        public static bool IsAllowedExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);
        }

        /// <summary>
        /// Checks if the MIME type is allowed
        /// </summary>
        public static bool IsAllowedMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return false;

            return AllowedMimeTypes.Contains(mimeType.ToLowerInvariant());
        }

        /// <summary>
        /// Gets a safe file name by removing potentially dangerous characters
        /// </summary>
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

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
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
