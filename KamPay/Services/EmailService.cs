using System;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using KamPay.Helpers;

namespace KamPay.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(EmailSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<bool> SendVerificationEmailAsync(string toEmail, string verificationCode)
        {
            try
            {
                var loc = LocalizationResourceManager.Instance;
                
                var subject = loc["EmailVerificationSubject"] ?? "KamPay - E-posta Doğrulama";
                var body = string.Format(
                    loc["EmailVerificationBody"] ?? 
                    "Merhaba,\n\nKamPay hesabınızı doğrulamak için aşağıdaki kodu kullanın:\n\n{0}\n\nBu kod 15 dakika geçerlidir.\n\nİyi günler,\nKamPay Ekibi", 
                    verificationCode
                );

                // ✅ Debug için konsola da yazdır
                Debug.WriteLine("========== KamPay Doğrulama Kodu ==========");
                Debug.WriteLine($"To: {toEmail}");
                Debug.WriteLine($"Kod: {verificationCode}");
                Debug.WriteLine("==========================================");
                Console.WriteLine($"📧 E-posta gönderiliyor: {toEmail}");

                // ✅ GERÇEK SMTP GÖNDERİMİ
                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    EnableSsl = _settings.UseSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);

                Debug.WriteLine($"✅ E-posta başarıyla gönderildi: {toEmail}");
                Console.WriteLine($"✅ Doğrulama kodu e-postaya gönderildi!");
                
                return true;
            }
            catch (SmtpException smtpEx)
            {
                Debug.WriteLine($"❌ SMTP Hatası: {smtpEx.Message}");
                Debug.WriteLine($"StatusCode: {smtpEx.StatusCode}");
                Console.WriteLine($"❌ E-posta gönderilemedi (SMTP): {smtpEx.Message}");
                
                // ⚠️ Hata durumunda konsola yine de kodu yazdır (geliştirme için)
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Kod: {verificationCode}");
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ E-posta gönderim hatası: {ex.Message}");
                Console.WriteLine($"❌ E-posta gönderilemedi: {ex.Message}");
                
                // ⚠️ Hata durumunda konsola yine de kodu yazdır (geliştirme için)
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Kod: {verificationCode}");
                
                return false;
            }
        }

        // 🔥 YENİ: ŞİFRE SIFIRLAMA E-POSTASI

        /// <summary>
        /// Şifre sıfırlama e-postası gönderir
        /// </summary>
        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetCode)
        {
            try
            {
                var loc = LocalizationResourceManager.Instance;
                
                var subject = loc["EmailPasswordResetSubject"] ?? "KamPay - Şifre Sıfırlama";
                var body = string.Format(
                    loc["EmailPasswordResetBody"] ?? 
                    "Merhaba,\n\nŞifrenizi sıfırlamak için aşağıdaki kodu kullanın:\n\n{0}\n\nBu kod 15 dakika geçerlidir.\n\nEğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.\n\nİyi günler,\nKamPay Ekibi", 
                    resetCode
                );

                Debug.WriteLine("========== Şifre Sıfırlama Kodu ==========");
                Debug.WriteLine($"To: {toEmail}");
                Debug.WriteLine($"Kod: {resetCode}");
                Debug.WriteLine("==========================================");
                Console.WriteLine($"📧 Şifre sıfırlama e-postası gönderiliyor: {toEmail}");

                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    EnableSsl = _settings.UseSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);

                Debug.WriteLine($"✅ Şifre sıfırlama e-postası gönderildi: {toEmail}");
                Console.WriteLine($"✅ Şifre sıfırlama kodu e-postaya gönderildi!");
                
                return true;
            }
            catch (SmtpException smtpEx)
            {
                Debug.WriteLine($"❌ SMTP Hatası: {smtpEx.Message}");
                Console.WriteLine($"❌ Şifre sıfırlama e-postası gönderilemedi (SMTP): {smtpEx.Message}");
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Şifre Sıfırlama Kodu: {resetCode}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ E-posta gönderim hatası: {ex.Message}");
                Console.WriteLine($"❌ Şifre sıfırlama e-postası gönderilemedi: {ex.Message}");
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Şifre Sıfırlama Kodu: {resetCode}");
                return false;
            }
        }

        // 🔥 YENİ: E-POSTA DEĞİŞTİRME DOĞRULAMA

        /// <summary>
        /// E-posta değişikliği için doğrulama kodu gönderir
        /// </summary>
        public async Task<bool> SendEmailChangeVerificationAsync(string newEmail, string verificationCode)
        {
            try
            {
                var loc = LocalizationResourceManager.Instance;
                
                var subject = loc["EmailChangeVerificationSubject"] ?? "KamPay - E-posta Değişikliği Doğrulama";
                var body = string.Format(
                    loc["EmailChangeVerificationBody"] ?? 
                    "Merhaba,\n\nE-posta adresinizi değiştirmek için aşağıdaki kodu kullanın:\n\n{0}\n\nBu kod 15 dakika geçerlidir.\n\nEğer bu talebi siz yapmadıysanız, hesabınızın güvenliği için şifrenizi değiştirmenizi öneririz.\n\nİyi günler,\nKamPay Ekibi", 
                    verificationCode
                );

                Debug.WriteLine("========== E-posta Değişikliği Doğrulama Kodu ==========");
                Debug.WriteLine($"To: {newEmail}");
                Debug.WriteLine($"Kod: {verificationCode}");
                Debug.WriteLine("=======================================================");
                Console.WriteLine($"📧 E-posta değişikliği doğrulama kodu gönderiliyor: {newEmail}");

                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    EnableSsl = _settings.UseSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(newEmail);

                await client.SendMailAsync(message);

                Debug.WriteLine($"✅ E-posta değişikliği doğrulama kodu gönderildi: {newEmail}");
                Console.WriteLine($"✅ E-posta değişikliği doğrulama kodu gönderildi!");
                
                return true;
            }
            catch (SmtpException smtpEx)
            {
                Debug.WriteLine($"❌ SMTP Hatası: {smtpEx.Message}");
                Console.WriteLine($"❌ E-posta değişikliği doğrulama kodu gönderilemedi (SMTP): {smtpEx.Message}");
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Doğrulama Kodu: {verificationCode}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ E-posta gönderim hatası: {ex.Message}");
                Console.WriteLine($"❌ E-posta değişikliği doğrulama kodu gönderilemedi: {ex.Message}");
                Console.WriteLine($"⚠️ GELİŞTİRME MODU - Doğrulama Kodu: {verificationCode}");
                return false;
            }
        }
    }
}
