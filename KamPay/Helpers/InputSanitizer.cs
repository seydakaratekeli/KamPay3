using System;
using System.Text.RegularExpressions;

namespace KamPay.Helpers
{
    //// XSS ve enjeksiyon saldırılarını önlemek için girdi doğrulama ve temizleme işlemleri için yardımcı sınıf
    ///
    public static class InputSanitizer
    {
        // Potansiyel olarak zararlı HTML/komut dosyası etiketlerini ve özel karakterleri kaldırarak metin girişini temizler

        public static string SanitizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Remove HTML tags
            var sanitized = Regex.Replace(input, @"<[^>]*>", string.Empty);

            // Remove script tags and their content
            sanitized = Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove javascript: protocol
            sanitized = Regex.Replace(sanitized, @"javascript:", string.Empty, RegexOptions.IgnoreCase);

            // Remove on* event handlers
            sanitized = Regex.Replace(sanitized, @"\s*on\w+\s*=\s*[""'][^""']*[""']", string.Empty, RegexOptions.IgnoreCase);

            // Trim whitespace
            sanitized = sanitized.Trim();

            return sanitized;
        }

        // Metnin potansiyel olarak tehlikeli içerik içerip içermediğini doğrular
        public static bool ContainsDangerousContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Check for script tags
            if (Regex.IsMatch(input, @"<script[^>]*>", RegexOptions.IgnoreCase))
                return true;

            // Check for javascript: protocol
            if (Regex.IsMatch(input, @"javascript:", RegexOptions.IgnoreCase))
                return true;

            // Check for on* event handlers
            if (Regex.IsMatch(input, @"\s*on\w+\s*=", RegexOptions.IgnoreCase))
                return true;

            // Check for iframe tags
            if (Regex.IsMatch(input, @"<iframe[^>]*>", RegexOptions.IgnoreCase))
                return true;

            // Check for object/embed tags
            if (Regex.IsMatch(input, @"<(object|embed)[^>]*>", RegexOptions.IgnoreCase))
                return true;

            return false;
        }

        // E-posta biçimini doğrular

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Basic email validation regex
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        // URL biçimini doğrular

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        // Kullanıcı adını temizler ve doğrular (yalnızca alfanümerik karakterler, alt çizgiler ve tireler)
        public static string SanitizeUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return username;

            // Keep only alphanumeric, underscores, and hyphens
            var sanitized = Regex.Replace(username, @"[^a-zA-Z0-9_\-]", string.Empty);

            return sanitized.Trim();
        }

        // ✅ YENİ: Ad ve Soyad için Türkçe karakter desteği ile doğrulama
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Türkçe karakterler dahil harf, boşluk ve tire karakterlerine izin ver
            // Unicode kategorisi \p{L} tüm harfleri kapsar (Türkçe dahil)
            var nameRegex = new Regex(@"^[\p{L}\s\-']+$", RegexOptions.None);
            
            return nameRegex.IsMatch(name.Trim());
        }

        // ✅ YENİ: Ad ve Soyad temizleme (Türkçe karakterleri korur)
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            // Tehlikeli içerikleri kontrol et
            if (ContainsDangerousContent(name))
                return string.Empty;

            // Sadece harf, boşluk, tire ve kesme işaretine izin ver
            // \p{L} tüm Unicode harflerini kapsar (Türkçe karakterler dahil)
            var sanitized = Regex.Replace(name, @"[^\p{L}\s\-']", string.Empty);

            // Birden fazla boşluğu tek boşluğa indir
            sanitized = Regex.Replace(sanitized, @"\s+", " ");

            return sanitized.Trim();
        }

        // Metin uzunluğunun kabul edilebilir aralıkta olup olmadığını doğrular
        public static bool IsValidLength(string text, int minLength, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return minLength == 0;

            var length = text.Trim().Length;
            return length >= minLength && length <= maxLength;
        }

        // Aşırı boşlukları kaldırır ve satır sonlarını normalleştirir
        public static string NormalizeWhitespace(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Replace multiple spaces with single space
            var normalized = Regex.Replace(input, @"\s+", " ");

            // Normalize line breaks
            normalized = Regex.Replace(normalized, @"(\r\n|\r|\n)+", "\n");

            return normalized.Trim();
        }

        // Telefon numarası formatını doğrular (temel doğrulama)
        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove common separators
            var digits = Regex.Replace(phoneNumber, @"[\s\-\(\)\+]", string.Empty);

            // Check if it's all digits and has reasonable length (7-15 digits)
            return Regex.IsMatch(digits, @"^\d{7,15}$");
        }

        // Sayısal girişi temizler ve ayrıştırılmış değeri döndürür
        public static bool TrySanitizeNumeric(string input, out decimal result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Remove non-numeric characters except decimal point and minus sign
            var sanitized = Regex.Replace(input, @"[^\d\.\-]", string.Empty);

            return decimal.TryParse(sanitized, out result);
        }

        // Tek tırnakları kaçırarak SQL enjeksiyonunu önler
        //  Not: Mümkün olduğunda parametreli sorgular kullanın
        public static string EscapeSqlInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            return input.Replace("'", "''");
        }

        // Girişin SQL enjeksiyon kalıpları içermediğini doğrular
        public static bool ContainsSqlInjectionPatterns(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var patterns = new[]
            {
                @"('|(\""))\s*(or|and)\s*\1\s*=\s*\1",  // ' or '1'='1
                @";\s*(drop|delete|insert|update|create|alter)\s+",  // ; DROP TABLE
                @"--",  // SQL comment
                @"/\*.*\*/",  // Multi-line SQL comment
                @"exec\s*\(",  // exec()
                @"execute\s*\(",  // execute()
                @"xp_"  // Extended stored procedures
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
