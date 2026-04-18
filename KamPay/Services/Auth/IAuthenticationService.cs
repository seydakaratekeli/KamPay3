using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services.Auth
{
    public interface IAuthenticationService
    {
        /// Yeni kullanıcı kaydı yapar ve doğrulama kodu gönderir
        Task<ServiceResult<User>> RegisterAsync(RegisterRequest request);

        /// Kullanıcı girişi yapar
        Task<ServiceResult<User>> LoginAsync(LoginRequest request);

        /// E-posta doğrulama kodu gönderir
        Task<ServiceResult<bool>> SendVerificationCodeAsync(string email);

        /// E-posta doğrulama kodunu kontrol eder
        Task<ServiceResult<bool>> VerifyEmailAsync(VerificationRequest request);

        /// Kayıt isteğini doğrular
        ValidationResult ValidateRegistration(RegisterRequest request);

        /// Giriş isteğini doğrular
        ValidationResult ValidateLogin(LoginRequest request);

        /// Kullanıcı çıkışı yapar
        Task<ServiceResult<bool>> LogoutAsync();

        /// Şu anki kullanıcıyı getirir
        Task<User> GetCurrentUserAsync();

        /// <summary>
        /// Geçerli kullanıcının Firebase ID Token'ını (JWT) getirir.
        /// Token süresi dolmuşsa veya dolmaya yakınsa otomatik olarak yeniler.
        /// </summary>
        Task<string> GetValidTokenAsync();

        /// <summary>
        /// Geçerli kullanıcının Firebase ID Token'ını döner.
        /// ProductApiService gibi API servisleri tarafından HTTP isteklerine
        /// Bearer Token eklemek için kullanılır.
        /// İç yapıda GetValidTokenAsync()'i çağırarak token yenileme işlemini de halleder.
        /// </summary>
        Task<string> GetCurrentUserTokenAsync();

        /// Kullanıcının giriş yapıp yapmadığını kontrol eder
        bool IsUserLoggedIn();

        // 🔐 YENİ: Firebase Authentication Özellikleri

        /// <summary>
        /// Şifre sıfırlama e-postası gönderir
        /// </summary>
        Task<ServiceResult<bool>> SendPasswordResetEmailAsync(string email);

        /// <summary>
        /// Şifre sıfırlama kodunu doğrular ve yeni şifre belirler
        /// </summary>
        Task<ServiceResult<bool>> ResetPasswordAsync(string email, string verificationCode, string newPassword);

        /// <summary>
        /// Kullanıcının e-posta adresini değiştirir (doğrulama kodu ile)
        /// </summary>
        Task<ServiceResult<bool>> ChangeEmailAsync(string currentEmail, string newEmail, string password);

        /// <summary>
        /// Yeni e-posta adresini doğrular
        /// </summary>
        Task<ServiceResult<bool>> VerifyNewEmailAsync(string newEmail, string verificationCode);

        /// <summary>
        /// Kullanıcının şifresini değiştirir
        /// </summary>
        Task<ServiceResult<bool>> ChangePasswordAsync(string email, string currentPassword, string newPassword);

        /// <summary>
        /// 🔄 YENİ: Uygulama başlangıcında otomatik giriş kontrolü (Remember Me)
        /// "Beni Hatırla" işaretliyse ve token geçerliyse otomatik giriş yapar
        /// </summary>
        Task<ServiceResult<User>> TryAutoLoginAsync();
    }
}