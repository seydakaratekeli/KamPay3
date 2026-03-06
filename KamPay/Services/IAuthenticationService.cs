using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services
{
    public interface IAuthenticationService
    {
        /// Yeni kullanýcý kaydý yapar ve doðrulama kodu gönderir
        Task<ServiceResult<User>> RegisterAsync(RegisterRequest request);

        /// Kullanýcý giriþi yapar
        Task<ServiceResult<User>> LoginAsync(LoginRequest request);

        /// E-posta doðrulama kodu gönderir
        Task<ServiceResult<bool>> SendVerificationCodeAsync(string email);

        /// E-posta doðrulama kodunu kontrol eder
        Task<ServiceResult<bool>> VerifyEmailAsync(VerificationRequest request);

        /// Kayýt isteðini doðrular
        ValidationResult ValidateRegistration(RegisterRequest request);

        /// Giriþ isteðini doðrular
        ValidationResult ValidateLogin(LoginRequest request);

        /// Kullanýcý çýkýþý yapar
        Task<ServiceResult<bool>> LogoutAsync();

        /// Þu anki kullanýcýyý getirir
        Task<User> GetCurrentUserAsync();

        /// Kullanýcýnýn giriþ yapýp yapmadýðýný kontrol eder
        bool IsUserLoggedIn();

        // ?? YENÝ: Firebase Authentication Özellikleri

        /// <summary>
        /// Þifre sýfýrlama e-postasý gönderir
        /// </summary>
        Task<ServiceResult<bool>> SendPasswordResetEmailAsync(string email);

        /// <summary>
        /// Þifre sýfýrlama kodunu doðrular ve yeni þifre belirler
        /// </summary>
        Task<ServiceResult<bool>> ResetPasswordAsync(string email, string verificationCode, string newPassword);

        /// <summary>
        /// Kullanýcýnýn e-posta adresini deðiþtirir (doðrulama kodu ile)
        /// </summary>
        Task<ServiceResult<bool>> ChangeEmailAsync(string currentEmail, string newEmail, string password);

        /// <summary>
        /// Yeni e-posta adresini doðrular
        /// </summary>
        Task<ServiceResult<bool>> VerifyNewEmailAsync(string newEmail, string verificationCode);

        /// <summary>
        /// Kullanýcýnýn þifresini deðiþtirir
        /// </summary>
        Task<ServiceResult<bool>> ChangePasswordAsync(string email, string currentPassword, string newPassword);

        /// <summary>
        /// ? YENÝ: Uygulama baþlangýcýnda otomatik giriþ kontrolü (Remember Me)
        /// "Beni Hatýrla" iþaretliyse ve token geçerliyse otomatik giriþ yapar
        /// </summary>
        Task<ServiceResult<User>> TryAutoLoginAsync();
    }
}