using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IEmailService
    {
        /// Doðrulama e-postasý gönderir. True dönerse gönderim baþarýlýdýr.
        Task<bool> SendVerificationEmailAsync(string toEmail, string verificationCode);
    }
}
