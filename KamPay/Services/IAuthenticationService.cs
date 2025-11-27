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
    }
}