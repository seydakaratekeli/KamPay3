using System;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public class EmailService : IEmailService
    {
        //bu sayfanın amacı e-posta gönderim işlevselliğini sağlamaktır. nasıl gönderileceği, hangi sunucu kullanılacağı gibi detayları kapsar.
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
                
                // ✅ EKLEME: Simülasyon uyarısı
                var subject = loc["EmailVerificationSubject"];
                var body = $"⚠️ SİMÜLASYON MODU ⚠️\n\n" +
                           $"Bu e-posta gerçek SMTP sunucusu olmadığı için gönderilmedi.\n" +
                           $"Doğrulama kodunuz konsol çıktısında görüntülenmektedir.\n\n" +
                           string.Format(loc["EmailVerificationBody"], verificationCode);

                // ✅ EKLEME: Detaylı simülasyon uyarısı
                Debug.WriteLine("========== KamPay Doğrulama Kodu (SİMÜLASYON) ==========");
                Debug.WriteLine($"⚠️ UYARI: Gerçek e-posta gönderimi devre dışı!");
                Debug.WriteLine($"To: {toEmail}");
                Debug.WriteLine($"Kod: {verificationCode}");
                Debug.WriteLine("=======================================================");
                
                Console.WriteLine("========== KamPay Doğrulama Kodu (SİMÜLASYON) ==========");
                Console.WriteLine($"⚠️ Bu kod sadece geliştirme ortamında geçerlidir");
                Console.WriteLine($"To: {toEmail}");
                Console.WriteLine($"Kod: {verificationCode}");
                Console.WriteLine("=========================================================");

                // 3)  SİMÜLASYON MODU: SMTP kodunu devre dışı bırak
                // Gerçek bir SMTP servisi bağlandığında aşağıdaki kod bloğu aktif edilecektir.
                
                /*
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
                */

                //  Simülasyon için kısa bir gecikme ekle (gerçekçi olsun)
                await Task.Delay(500);

                Debug.WriteLine($"[KamPay] ✅ E-posta simüle edildi: {toEmail}");
                Console.WriteLine($"✅ Doğrulama kodu başarıyla oluşturuldu!");
                
                return true; //  Simülasyonda her zaman başarılı
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KamPay] E-posta simülasyon hatası: {ex.Message}");
                Console.WriteLine($"❌ E-posta simülasyon hatası: {ex.Message}");
                return false;
            }
        }
    }
}
