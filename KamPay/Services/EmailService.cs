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
                
                // 1) E-posta gövdesi (HTML veya plain)
                var subject = loc["EmailVerificationSubject"];
                var body = string.Format(loc["EmailVerificationBody"], verificationCode);

                // 2) Debug'a yaz GELİŞTİRME AŞAMASI İÇİN SİMÜLASYON
                Debug.WriteLine("---------- KamPay Doğrulama Kodu (Debug) ----------");
                Debug.WriteLine($"To: {toEmail}");
                Debug.WriteLine($"Kod: {verificationCode}");
                Debug.WriteLine("--------------------------------------------------");

                // 3) SMTP ile gönderim
                //Gerçek bir SMTP servisi bağlandığında aşağıdaki kod bloğu kullanılacaktır.
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

                Debug.WriteLine($"[KamPay] E-posta başarıyla gönderildi: {toEmail}");
                return true;
            }
            catch (SmtpException smtpEx)
            {
                Debug.WriteLine($"[KamPay] SMTP hatası: {smtpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KamPay] E-posta gönderim hatası: {ex.Message}");
                return false;
            }
        }
    }
}
