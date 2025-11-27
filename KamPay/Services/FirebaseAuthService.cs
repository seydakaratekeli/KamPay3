using System;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging; 
 using KamPay.ViewModels; 
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services
{
    public class FirebaseAuthService : IAuthenticationService
    {
        private readonly FirebaseClient _firebaseClient;
        private User _currentUser;

        private readonly IEmailService _emailService;

        public FirebaseAuthService(IEmailService emailService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _emailService = emailService;
        }


        public async Task<ServiceResult<User>> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // 1. Validasyon
                var validation = ValidateRegistration(request);
                if (!validation.IsValid)
                {
                    return ServiceResult<User>.FailureResult(
                        "Kayıt bilgileri geçersiz",
                        validation.Errors.ToArray()
                    );
                }

                // 2. E-posta kontrolü (daha önce kayıtlı mı?)
                var existingUsers = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .OrderBy("Email")
                    .EqualTo(request.Email.ToLower())
                    .OnceAsync<User>();

                if (existingUsers.Any())
                {
                    return ServiceResult<User>.FailureResult(
                        "Bu e-posta adresi zaten kayıtlı",
                        "Lütfen farklı bir e-posta adresi kullanın veya giriş yapın"
                    );
                }

                // 3. Kullanıcı nesnesi oluştur
                var user = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.ToLower().Trim(),
                    PasswordHash = HashPassword(request.Password),
                    IsEmailVerified = false,
                    CreatedAt = DateTime.UtcNow
                };

                // 4. Doğrulama kodu oluştur
                user.VerificationCode = GenerateVerificationCode();
                user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15); // 15 dakika geçerli

                // 5. Firebase'e kaydet
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 6. Doğrulama kodu gönder (şimdilik simüle ediyoruz)
                await SendVerificationCodeAsync(user.Email);

                return ServiceResult<User>.SuccessResult(
                    user,
                    "Kayıt başarılı! E-postanıza gönderilen doğrulama kodunu girin."
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<User>.FailureResult(
                    "Kayıt sırasında bir hata oluştu",
                    ex.Message
                );
            }
        }

        public async Task<ServiceResult<User>> LoginAsync(LoginRequest request)
        {
            try
            {
                // 1. Validasyon
                var validation = ValidateLogin(request);
                if (!validation.IsValid)
                {
                    return ServiceResult<User>.FailureResult(
                        "Giriş bilgileri geçersiz",
                        validation.Errors.ToArray()
                    );
                }

                // 2. Kullanıcıyı bul
                var users = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .OrderBy("Email")
                    .EqualTo(request.Email.ToLower())
                    .OnceAsync<User>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null)
                {
                    return ServiceResult<User>.FailureResult(
                        "Giriş başarısız",
                        "E-posta veya şifre hatalı"
                    );
                }

                var user = userEntry.Object;

                // 3. Şifre kontrolü
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    return ServiceResult<User>.FailureResult(
                        "Giriş başarısız",
                        "E-posta veya şifre hatalı"
                    );
                }

                // 4. E-posta doğrulaması kontrolü
                if (!user.IsEmailVerified)
                {
                    return ServiceResult<User>.FailureResult(
                        "E-posta doğrulanmamış",
                        "Lütfen e-postanıza gönderilen doğrulama kodunu girin"
                    );
                }

                // 5. Aktif kullanıcı kontrolü
                if (!user.IsActive)
                {
                    return ServiceResult<User>.FailureResult(
                        "Hesap devre dışı",
                        "Hesabınız yönetici tarafından devre dışı bırakılmış"
                    );
                }

                // 6. Son giriş zamanını güncelle
                user.LastLoginAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 7. Oturum bilgisini sakla
                _currentUser = user;
                if (request.RememberMe)
                {
                    await SaveUserSessionAsync(user);
                }
                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(true));


                return ServiceResult<User>.SuccessResult(user, "Giriş başarılı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<User>.FailureResult(
                    "Giriş sırasında bir hata oluştu",
                    ex.Message
                );
            }
        }

        public async Task<ServiceResult<bool>> SendVerificationCodeAsync(string email)
        {
            try
            {
                // 1️⃣ Kullanıcıyı e-posta ile bul
                var users = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .OrderBy("Email")
                    .EqualTo(email.ToLower())
                    .OnceAsync<User>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Kullanıcı bulunamadı",
                        "Bu e-posta adresiyle kayıtlı kullanıcı yok"
                    );
                }

                var user = userEntry.Object;

                // 2️⃣ Yeni doğrulama kodu oluştur ve güncelle
                user.VerificationCode = GenerateVerificationCode();
                user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);

                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 3️⃣ Doğrulama kodunu gönder
                var emailSent = await _emailService.SendVerificationEmailAsync(user.Email, user.VerificationCode);

                // 4️⃣ Debug Log — her zaman yaz
                System.Diagnostics.Debug.WriteLine("---------- KamPay Doğrulama Kodu ----------");
                System.Diagnostics.Debug.WriteLine($"Kullanıcı: {user.Email}");
                System.Diagnostics.Debug.WriteLine($"Kod: {user.VerificationCode}");
                System.Diagnostics.Debug.WriteLine($"Geçerlilik Süresi: {user.VerificationCodeExpiry}");
                System.Diagnostics.Debug.WriteLine("--------------------------------------------");

                // 5️⃣ Gönderim sonucu kontrolü
                if (emailSent)
                {
                    return ServiceResult<bool>.SuccessResult(
                        true,
                        "Doğrulama kodu e-postanıza gönderildi."
                    );
                }
                else
                {
                    return ServiceResult<bool>.FailureResult(
                        "E-posta gönderimi başarısız",
                        "Kod gönderilemedi, lütfen tekrar deneyin."
                    );
                }
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Doğrulama kodu gönderilemedi",
                    ex.Message
                );
            }
        }

        public async Task<ServiceResult<bool>> VerifyEmailAsync(VerificationRequest request)
        {
            try
            {
                var users = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .OrderBy("Email")
                    .EqualTo(request.Email.ToLower())
                    .OnceAsync<User>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Kullanıcı bulunamadı"
                    );
                }

                var user = userEntry.Object;

                // Kod kontrolü
                if (user.VerificationCode != request.VerificationCode)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Geçersiz doğrulama kodu",
                        "Lütfen e-postanıza gelen kodu kontrol edin"
                    );
                }

                // Süre kontrolü
                if (DateTime.UtcNow > user.VerificationCodeExpiry)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Doğrulama kodunun süresi dolmuş",
                        "Lütfen yeni bir kod talep edin"
                    );
                }

                // E-postayı doğrula
                user.IsEmailVerified = true;
                user.VerificationCode = null;
                user.VerificationCodeExpiry = DateTime.MinValue;

                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "E-posta başarıyla doğrulandı!"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult(
                    "Doğrulama sırasında hata oluştu",
                    ex.Message
                );
            }
        }

        public ValidationResult ValidateRegistration(RegisterRequest request)
        {
            var result = new ValidationResult();

            // Ad kontrolü
            if (string.IsNullOrWhiteSpace(request.FirstName))
                result.AddError("Ad alanı boş bırakılamaz");
            else if (request.FirstName.Length < 2)
                result.AddError("Ad en az 2 karakter olmalıdır");

            // Soyad kontrolü
            if (string.IsNullOrWhiteSpace(request.LastName))
                result.AddError("Soyad alanı boş bırakılamaz");
            else if (request.LastName.Length < 2)
                result.AddError("Soyad en az 2 karakter olmalıdır");

            // E-posta kontrolü
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                result.AddError("E-posta alanı boş bırakılamaz");
            }
            else
            {
                // E-posta format kontrolü
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(request.Email))
                {
                    result.AddError("Geçersiz e-posta formatı");
                }

                // Üniversite e-posta kontrolü
                if (!request.Email.ToLower().EndsWith(Constants.UniversityEmailDomain))
                {
                    result.AddError($"Sadece {Constants.UniversityEmailDomain} uzantılı e-postalar kabul edilir");
                }
            }

            // Şifre kontrolü
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                result.AddError("Şifre alanı boş bırakılamaz");
            }
            else
            {
                if (request.Password.Length < Constants.MinPasswordLength)
                    result.AddError($"Şifre en az {Constants.MinPasswordLength} karakter olmalıdır");

                if (request.Password.Length > Constants.MaxPasswordLength)
                    result.AddError($"Şifre en fazla {Constants.MaxPasswordLength} karakter olmalıdır");

                // Şifre karmaşıklık kontrolü
                if (!Regex.IsMatch(request.Password, @"[A-Z]"))
                    result.AddError("Şifre en az bir büyük harf içermelidir");

                if (!Regex.IsMatch(request.Password, @"[a-z]"))
                    result.AddError("Şifre en az bir küçük harf içermelidir");

                if (!Regex.IsMatch(request.Password, @"[0-9]"))
                    result.AddError("Şifre en az bir rakam içermelidir");
            }

            // Şifre tekrarı kontrolü
            if (request.Password != request.PasswordConfirm)
            {
                result.AddError("Şifreler eşleşmiyor");
            }

            return result;
        }

        public ValidationResult ValidateLogin(LoginRequest request)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(request.Email))
                result.AddError("E-posta alanı boş bırakılamaz");

            if (string.IsNullOrWhiteSpace(request.Password))
                result.AddError("Şifre alanı boş bırakılamaz");

            return result;
        }

        public async Task<ServiceResult<bool>> LogoutAsync()
        {
            try
            {
                _currentUser = null;
                await ClearUserSessionAsync();

                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(false));

                return ServiceResult<bool>.SuccessResult(true, "Çıkış başarılı");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Çıkış yapılamadı", ex.Message);
            }
        }

        public async Task<User> GetCurrentUserAsync()
        {
            if (_currentUser != null)
                return _currentUser;

            // Preferences'tan kullanıcı bilgisini al
            var userId = Preferences.Get("current_user_id", string.Empty);
            if (string.IsNullOrEmpty(userId))
                return null;

            try
            {
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<User>();

                _currentUser = user;
                return user;
            }
            catch
            {
                return null;
            }
        }

        public bool IsUserLoggedIn()
        {
            return _currentUser != null || !string.IsNullOrEmpty(Preferences.Get("current_user_id", string.Empty));
        }

        // Helper metodlar
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            var passwordHash = HashPassword(password);
            return passwordHash == hash;
        }

        private string GenerateVerificationCode()
        {
            // 6 haneli rastgele kod
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task SaveUserSessionAsync(User user)
        {
            Preferences.Set("current_user_id", user.UserId);
            Preferences.Set("current_user_email", user.Email);
            await Task.CompletedTask;
        }

        private async Task ClearUserSessionAsync()
        {
            Preferences.Remove("current_user_id");
            Preferences.Remove("current_user_email");
            await Task.CompletedTask;
        }
    }
}