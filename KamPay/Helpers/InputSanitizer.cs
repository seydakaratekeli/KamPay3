using System;
using System.Text.RegularExpressions;

namespace KamPay.Helpers
{
    //// XSS ve enjeksiyon saldýrýlarýný önlemek için girdi doðrulama ve temizleme iþlemleri için yardýmcý sýnýf
    ///
    public static class InputSanitizer
    {
        // Potansiyel olarak zararlý HTML/komut dosyasý etiketlerini ve özel karakterleri kaldýrarak metin giriþini temizler

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

        // Metnin potansiyel olarak tehlikeli içerik içerip içermediðini doðrular
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

        // E-posta biçimini doðrular

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

        // URL biçimini doðrular

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        // Kullanýcý adýný temizler ve doðrular (yalnýzca alfanümerik karakterler, alt çizgiler ve tireler)
        public static string SanitizeUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return username;

            // Keep only alphanumeric, underscores, and hyphens
            var sanitized = Regex.Replace(username, @"[^a-zA-Z0-9_\-]", string.Empty);

            return sanitized.Trim();
        }

        // Metin uzunluðunun kabul edilebilir aralýkta olup olmadýðýný doðrular
        public static bool IsValidLength(string text, int minLength, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return minLength == 0;

            var length = text.Trim().Length;
            return length >= minLength && length <= maxLength;
        }

        // Aþýrý boþluklarý kaldýrýr ve satýr sonlarýný normalleþtirir
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

        // Telefon numarasý formatýný doðrular (temel doðrulama)
        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove common separators
            var digits = Regex.Replace(phoneNumber, @"[\s\-\(\)\+]", string.Empty);

            // Check if it's all digits and has reasonable length (7-15 digits)
            return Regex.IsMatch(digits, @"^\d{7,15}$");
        }

        // Sayýsal giriþi temizler ve ayrýþtýrýlmýþ deðeri döndürür
        public static bool TrySanitizeNumeric(string input, out decimal result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Remove non-numeric characters except decimal point and minus sign
            var sanitized = Regex.Replace(input, @"[^\d\.\-]", string.Empty);

            return decimal.TryParse(sanitized, out result);
        }

        // Tek týrnaklarý kaçýrarak SQL enjeksiyonunu önler
        //  Not: Mümkün olduðunda parametreli sorgular kullanýn
        public static string EscapeSqlInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            return input.Replace("'", "''");
        }

        // Giriþin SQL enjeksiyon kalýplarý içermediðini doðrular
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
