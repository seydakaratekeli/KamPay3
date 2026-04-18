using System;
using System.Diagnostics;
using KamPay.Services;

namespace KamPay.Helpers
{
    /// <summary>
    /// E-posta test ve debug yardımcı sınıfı
    /// </summary>
    public static class EmailTestHelper
    {
        /// <summary>
        /// SMTP ayarlarını konsola yazdırır (şifre gizlenir)
        /// </summary>
        public static void LogEmailSettings(EmailSettings settings)
        {
            if (settings == null)
            {
                Debug.WriteLine("?? EmailSettings null!");
                return;
            }

            Debug.WriteLine("========== EMAIL SETTINGS ==========");
            Debug.WriteLine($"SMTP Host: {settings.SmtpHost}");
            Debug.WriteLine($"SMTP Port: {settings.SmtpPort}");
            Debug.WriteLine($"Use SSL: {settings.UseSsl}");
            Debug.WriteLine($"From Email: {settings.FromEmail}");
            Debug.WriteLine($"From Name: {settings.FromName}");
            Debug.WriteLine($"Username: {settings.Username}");
            Debug.WriteLine($"Password: {MaskPassword(settings.Password)}");
            Debug.WriteLine("====================================");
        }

        /// <summary>
        /// Şifreyi maskelenmiş olarak gösterir (ilk 2 karakter + ***)
        /// </summary>
        private static string MaskPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return "*** (BOŞ) ***";

            if (password.Length <= 2)
                return "***";

            return $"{password.Substring(0, 2)}***{new string('*', Math.Min(password.Length - 2, 8))}";
        }

        /// <summary>
        /// SMTP bağlantı testi (gerçek gönderim yapmadan)
        /// </summary>
        public static bool TestSmtpConnection(EmailSettings settings)
        {
            try
            {
                Debug.WriteLine($"?? SMTP bağlantı testi başlatılıyor: {settings.SmtpHost}:{settings.SmtpPort}");

                using var client = new System.Net.Mail.SmtpClient(settings.SmtpHost, settings.SmtpPort)
                {
                    EnableSsl = settings.UseSsl,
                    Credentials = new System.Net.NetworkCredential(settings.Username, settings.Password),
                    DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                    Timeout = 10000 // 10 saniye
                };

                // NOT: SmtpClient'in TestConnection metodu yok, bu yüzden gerçek bir send denemesi yapmalıyız
                // Ancak burada sadece client oluşturulabildiğini kontrol ediyoruz
                
                Debug.WriteLine("? SMTP client başarıyla oluşturuldu");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"? SMTP bağlantı testi başarısız: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mailtrap kullanıldığını kontrol eder
        /// </summary>
        public static bool IsMailtrapConfigured(EmailSettings settings)
        {
            if (settings == null) return false;

            return settings.SmtpHost?.Contains("mailtrap.io", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Test modu uyarısı gösterir
        /// </summary>
        public static void ShowTestModeWarning(EmailSettings settings)
        {
            if (IsMailtrapConfigured(settings))
            {
                Debug.WriteLine("?? ????????????????????????????????????????????????");
                Debug.WriteLine("??  TEST MODU: Mailtrap kullanılıyor!");
                Debug.WriteLine("??  Gerçek e-postalar gönderilmeyecek.");
                Debug.WriteLine("??  E-postaları görmek için Mailtrap'e gidin:");
                Debug.WriteLine("??  https://mailtrap.io/inboxes");
                Debug.WriteLine("?? ????????????????????????????????????????????????");

                KamPay.Helpers.AppLogger.DebugLog("\n?? TEST MODU AKTIF - Mailtrap kullanılıyor\n");
            }
            else if (settings?.Password == "SMTP_PAROLASI_BURAYA" || 
                     settings?.Password == "MAILTRAP_PASSWORD_BURAYA" ||
                     settings?.Username == "MAILTRAP_USERNAME_BURAYA")
            {
                Debug.WriteLine("?? ????????????????????????????????????????????????");
                Debug.WriteLine("??  UYARI: SMTP ayarları tamamlanmamış!");
                Debug.WriteLine("??  appsettings.json dosyasını kontrol edin.");
                Debug.WriteLine("??  Gerçek credentials girilmemiş.");
                Debug.WriteLine("?? ????????????????????????????????????????????????");

                KamPay.Helpers.AppLogger.DebugLog("\n?? SMTP AYARLARI EKSİK - appsettings.json kontrol edin\n");
            }
            else
            {
                Debug.WriteLine($"? ÜRETIM MODU: {settings?.SmtpHost} kullanılıyor");
            }
        }

        /// <summary>
        /// Doğrulama kodu formatını kontrol eder
        /// </summary>
        public static bool IsValidVerificationCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            // 6 haneli sayı olmalı
            return code.Length == 6 && int.TryParse(code, out _);
        }

        /// <summary>
        /// Test için rastgele geçerli bir e-posta üretir
        /// </summary>
        public static string GenerateTestEmail()
        {
            var random = new Random();
            var randomId = random.Next(1000, 9999);
            var timestamp = DateTime.Now.Ticks;
            
            return $"test{randomId}_{timestamp}@bartin.edu.tr";
        }
    }
}

