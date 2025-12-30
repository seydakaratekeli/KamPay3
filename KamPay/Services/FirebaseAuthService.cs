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
    // bu sayfanın amacı Firebase Realtime Database kullanarak kullanıcı kimlik doğrulama işlemlerini gerçekleştirmektir.nasıl kayıt olunacağı, giriş yapılacağı, e-posta doğrulama kodlarının gönderileceği ve doğrulanacağı gibi işlevleri kapsar.

    public class FirebaseAuthService : IAuthenticationService
    {
        private readonly FirebaseClient _firebaseClient;
        private User? _currentUser;
        private readonly IEmailService _emailService;
        private readonly IUserProfileService _userProfileService;

        public FirebaseAuthService(IEmailService emailService, IUserProfileService userProfileService)
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _emailService = emailService;
            _userProfileService = userProfileService;
        }
        


        public async Task<ServiceResult<User>> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (request == null) return ServiceResult<User>.FailureResult("Hata", "Veriler boş.");
                if (!NetworkHelper.HasInternetConnection()) return ServiceResult<User>.FailureResult("Bağlantı Hatası", "İnternet yok.");

                var validation = ValidateRegistration(request);
                if (!validation.IsValid) return ServiceResult<User>.FailureResult("Geçersiz bilgiler", validation.Errors.ToArray());

                string safeEmail = (request.Email?.Trim() ?? string.Empty).ToLower();

                // E-posta kontrolü
                var existingUsers = await _firebaseClient.Child(Constants.UsersCollection)
                    .OrderBy("Email").EqualTo(safeEmail).OnceAsync<User>();

                if (existingUsers != null && existingUsers.Any())
                    return ServiceResult<User>.FailureResult("Hata", "Bu e-posta zaten kayıtlı.");

                // ✅ Username ve PhoneNumber Fix'li User Nesnesi
                var user = new User
                {
                    FirstName = InputSanitizer.SanitizeName(request.FirstName?.Trim() ?? ""),
                    LastName = InputSanitizer.SanitizeName(request.LastName?.Trim() ?? ""),
                    Email = safeEmail,
                    // Kayıt anında otomatik username atıyoruz
                    Username = $"{request.FirstName.ToLower().Replace(" ", "")}{new Random().Next(100, 999)}",
                    PhoneNumber = "",
                    PasswordHash = HashPassword(request.Password ?? ""),
                    IsEmailVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                user.VerificationCode = GenerateVerificationCode();
                user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);

                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);
                await SendVerificationCodeAsync(user.Email);

                return ServiceResult<User>.SuccessResult(user, "Kayıt başarılı! Kod gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<User>.FailureResult("Kayıt hatası", ex.Message);
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
                // 1️ Kullanıcıyı e-posta ile bul
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

                // 2️ Yeni doğrulama kodu oluştur ve güncelle
                user.VerificationCode = GenerateVerificationCode();
                user.VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);

                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 3️ Doğrulama kodunu gönder
                var emailSent = await _emailService.SendVerificationEmailAsync(user.Email, user.VerificationCode);

                // 4️ Debug Log — her zaman yaz
                System.Diagnostics.Debug.WriteLine("---------- KamPay Doğrulama Kodu ----------");
                System.Diagnostics.Debug.WriteLine($"Kullanıcı: {user.Email}");
                System.Diagnostics.Debug.WriteLine($"Kod: {user.VerificationCode}");
                System.Diagnostics.Debug.WriteLine($"Geçerlilik Süresi: {user.VerificationCodeExpiry}");
                System.Diagnostics.Debug.WriteLine("--------------------------------------------");

                // 5️ Gönderim sonucu kontrolü
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
                var users = await _firebaseClient.Child(Constants.UsersCollection)
                    .OrderBy("Email").EqualTo(request.Email.ToLower()).OnceAsync<User>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null) return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");

                var user = userEntry.Object;

                if (user.VerificationCode != request.VerificationCode)
                    return ServiceResult<bool>.FailureResult("Geçersiz kod");

                if (DateTime.UtcNow > user.VerificationCodeExpiry)
                    return ServiceResult<bool>.FailureResult("Kodun süresi dolmuş");

                // Verileri hazırla
                user.IsEmailVerified = true;
                user.VerificationCode = "VERIFIED";
                user.VerificationCodeExpiry = DateTime.MinValue;

                if (string.IsNullOrEmpty(user.ProfileImageUrl))
                {
                    user.ProfileImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random";
                }

                // Tek seferde güncelle
                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);

                // Profil ve Stats oluştur
                var profileResult = await _userProfileService.CreateUserProfileAsync(user);

                return ServiceResult<bool>.SuccessResult(true, "E-posta doğrulandı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Doğrulama hatası", ex.Message);
            }
        }
        public ValidationResult ValidateRegistration(RegisterRequest request)
        {
            var result = new ValidationResult();

            // Ad kontrolü
            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                result.AddError("Ad alanı boş bırakılamaz");
            }
            else
            {
                // ✅ Türkçe karakter desteği ile yeni validasyon
                if (!InputSanitizer.IsValidName(request.FirstName))
                {
                    result.AddError("Ad alanı geçersiz karakterler içeriyor");
                }
                else if (request.FirstName.Trim().Length < 2)
                {
                    result.AddError("Ad en az 2 karakter olmalıdır");
                }
                else if (InputSanitizer.ContainsDangerousContent(request.FirstName))
                {
                    result.AddError("Ad alanı güvenli olmayan içerik içeriyor");
                }
            }

            // Soyad kontrolü
            if (string.IsNullOrWhiteSpace(request.LastName))
            {
                result.AddError("Soyad alanı boş bırakılamaz");
            }
            else
            {
                // ✅ Türkçe karakter desteği ile yeni validasyon
                if (!InputSanitizer.IsValidName(request.LastName))
                {
                    result.AddError("Soyad alanı geçersiz karakterler içeriyor");
                }
                else if (request.LastName.Trim().Length < 2)
                {
                    result.AddError("Soyad en az 2 karakter olmalıdır");
                }
                else if (InputSanitizer.ContainsDangerousContent(request.LastName))
                {
                    result.AddError("Soyad alanı güvenli olmayan içerik içeriyor");
                }
            }

            // E-posta kontrolü
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                result.AddError("E-posta alanı boş bırakılamaz");
            }
            else
            {
                // E-posta format kontrolü using InputSanitizer
                if (!InputSanitizer.IsValidEmail(request.Email))
                {
                    result.AddError("Geçersiz e-posta formatı");
                }
                // Üniversite e-posta kontrolü
                else if (!request.Email.ToLower().EndsWith(Constants.UniversityEmailDomain))
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

                // ✅ FIX: Türkçe büyük harfleri de destekleyen şifre karmaşıklık kontrolü
                // \p{Lu} = Tüm Unicode büyük harfleri (İ, Ğ, Ü, Ş, Ö, Ç dahil)
                if (!Regex.IsMatch(request.Password, @"\p{Lu}"))
                    result.AddError("Şifre en az bir büyük harf içermelidir");

                // \p{Ll} = Tüm Unicode küçük harfleri (ı, ğ, ü, ş, ö, ç dahil)
                if (!Regex.IsMatch(request.Password, @"\p{Ll}"))
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
                Console.WriteLine("🔓 Çıkış işlemi başlatılıyor...");

                // 1. Auth servisindeki yerel kullanıcıyı temizle
                _currentUser = null;

                // 2. Preferences'tan oturum bilgilerini sil
                await ClearUserSessionAsync();

                // 3. EKLEME: UserStateService'i de temizle
                // Uygulamanın DI konteynerinden servise erişiyoruz
                var userStateService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                userStateService?.ClearUser(); // içindeki ClearUser metodunu çağırır.

                // 4. Mesaj gönder (UI'ı güncelle)
                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(false));

                Console.WriteLine("✅ Çıkış başarılı - Tüm oturum bilgileri temizlendi");

                return ServiceResult<bool>.SuccessResult(true, "Çıkış başarılı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Çıkış hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Çıkış yapılamadı", ex.Message);
            }
        }

        public async Task<User> GetCurrentUserAsync()
        {
            // ✅ Önce bellekteki kullanıcıyı kontrol et
            if (_currentUser != null)
                return _currentUser;

            // ✅ Preferences'tan kullanıcı bilgisini al
            var userId = Preferences.Get("current_user_id", string.Empty);
            if (string.IsNullOrEmpty(userId))
                return null;

            try
            {
                // ✅ FIX: Firebase'e gitmeden önce internet kontrolü yap
                if (!NetworkHelper.HasInternetConnection())
                {
                    Console.WriteLine("⚠️ İnternet yok, cached kullanıcı kullanılacak");
                    return _currentUser; // null dönebilir ama crash etmez
                }

                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<User>();

                _currentUser = user;
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetCurrentUser hatası: {ex.Message}");
                
                // ✅ Hata durumunda cached user'ı döndür
                return _currentUser;
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