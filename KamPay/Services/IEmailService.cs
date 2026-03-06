using System.Threading.Tasks;

namespace KamPay.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Doðrulama e-postasý gönderir. True dönerse gönderim baþarýlýdýr.
        /// ?? NOT: Artýk Firebase native doðrulama kullanýlýyor.
        /// Bu metod sadece özel durumlar için tutulmuþtur.
        /// </summary>
        Task<bool> SendVerificationEmailAsync(string toEmail, string verificationCode);

        // ?? Aþaðýdaki metodlar artýk kullanýlmýyor (Firebase native kullanýlýyor)
        // Backward compatibility için interface'te kalýyor

        /// <summary>
        /// [DEPRECATED] Firebase native þifre sýfýrlama kullanýlýyor
        /// </summary>
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetCode);

        /// <summary>
        /// [DEPRECATED] Firebase native e-posta deðiþtirme kullanýlýyor
        /// </summary>
        Task<bool> SendEmailChangeVerificationAsync(string newEmail, string verificationCode);
    }
}
