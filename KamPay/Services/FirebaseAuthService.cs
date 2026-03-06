using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.ViewModels;
using AppUser = KamPay.Models.User;

namespace KamPay.Services
{
    /// <summary>
    /// ?? Firebase Authentication kullanan authentication servisi
    /// Manuel þifre hash'leme yerine Firebase'in güvenli authentication sistemini kullanýr
    /// </summary>
    public class FirebaseAuthService : IAuthenticationService
    {
        private readonly FirebaseAuthProvider _authProvider;
        private readonly FirebaseClient _firebaseClient;
        private readonly IEmailService _emailService;
        private readonly IUserProfileService _userProfileService;
        private AppUser? _currentUser;
        private FirebaseAuthLink? _authLink;

        public FirebaseAuthService(
            string apiKey,
            IEmailService emailService,
            IUserProfileService userProfileService)
        {
            _authProvider = new FirebaseAuthProvider(new FirebaseConfig(apiKey));
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _emailService = emailService;
            _userProfileService = userProfileService;
        }

        #region Registration

        public async Task<ServiceResult<AppUser>> RegisterAsync(Models.RegisterRequest request)
        {
            try
            {
                if (request == null)
                    return ServiceResult<AppUser>.FailureResult("Hata", "Veriler boþ.");

                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<AppUser>.FailureResult("Baðlantý Hatasý", "Ýnternet yok.");

                var validation = ValidateRegistration(request);
                if (!validation.IsValid)
                    return ServiceResult<AppUser>.FailureResult("Geçersiz bilgiler", validation.Errors.ToArray());

                string safeEmail = request.Email?.Trim().ToLower() ?? string.Empty;

                // 1?? Firebase Authentication ile kullanýcý oluþtur
                FirebaseAuthLink authResult;
                try
                {
                    authResult = await _authProvider.CreateUserWithEmailAndPasswordAsync(
                        safeEmail,
                        request.Password,
                        $"{request.FirstName} {request.LastName}" // DisplayName
                    );
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<AppUser>.FailureResult("Kayýt hatasý", GetFriendlyErrorMessage(ex));
                }

                // 2?? Kullanýcý bilgilerini Realtime Database'e kaydet
                var user = new AppUser
                {
                    UserId = authResult.User.LocalId, // Firebase UID kullan
                    FirstName = InputSanitizer.SanitizeName(request.FirstName?.Trim() ?? ""),
                    LastName = InputSanitizer.SanitizeName(request.LastName?.Trim() ?? ""),
                    Email = safeEmail,
                    Username = $"{request.FirstName.ToLower().Replace(" ", "")}{new Random().Next(100, 999)}",
                    PhoneNumber = "",
                    PasswordHash = "", // Artýk Firebase yönetiyor
                    IsEmailVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);

                // 3?? Firebase Email Verification gönder (SADECE BU!)
                try
                {
                    await _authProvider.SendEmailVerificationAsync(authResult.FirebaseToken);
                    
                    System.Diagnostics.Debug.WriteLine($"? Firebase email verification gönderildi: {user.Email}");
                    System.Diagnostics.Debug.WriteLine($"?? Kullanýcý e-postasýndaki linke týklayarak doðrulayacak");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"?? Firebase email verification gönderilemedi: {ex.Message}");
                    return ServiceResult<AppUser>.FailureResult("E-posta doðrulama hatasý", "Doðrulama e-postasý gönderilemedi");
                }

                return ServiceResult<AppUser>.SuccessResult(user, "Kayýt baþarýlý! E-postanýza gönderilen linke týklayarak hesabýnýzý doðrulayýn.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? RegisterAsync hatasý: {ex.Message}");
                return ServiceResult<AppUser>.FailureResult("Kayýt hatasý", ex.Message);
            }
        }

        #endregion

        #region Login

        public async Task<ServiceResult<AppUser>> LoginAsync(Models.LoginRequest request)
        {
            try
            {
                var validation = ValidateLogin(request);
                if (!validation.IsValid)
                    return ServiceResult<AppUser>.FailureResult("Giriþ bilgileri geçersiz", validation.Errors.ToArray());

                // 1?? Firebase Authentication ile giriþ yap
                try
                {
                    _authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                        request.Email.ToLower(),
                        request.Password
                    );
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<AppUser>.FailureResult("Giriþ baþarýsýz", GetFriendlyErrorMessage(ex));
                }

                // 2?? Firebase'den e-posta doðrulama durumunu kontrol et
                if (!_authLink.User.IsEmailVerified)
                {
                    // Kullanýcý doðrulama e-postaasýný almamýþsa tekrar gönder
                    try
                    {
                        await _authProvider.SendEmailVerificationAsync(_authLink.FirebaseToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"?? Yeniden doðrulama e-postasý gönderilemedi: {ex.Message}");
                    }

                    return ServiceResult<AppUser>.FailureResult(
                        "E-posta doðrulanmamýþ",
                        "Lütfen e-postanýza gönderilen linke týklayarak hesabýnýzý doðrulayýn. Yeni bir doðrulama linki gönderildi."
                    );
                }

                // 3?? Kullanýcý bilgilerini Realtime Database'den al
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(_authLink.User.LocalId)
                    .OnceSingleAsync<AppUser>();

                if (user == null)
                    return ServiceResult<AppUser>.FailureResult("Kullanýcý bulunamadý", "Hesap bilgileri eksik.");

                // 4?? Realtime Database'deki doðrulama durumunu güncelle
                if (!user.IsEmailVerified)
                {
                    user.IsEmailVerified = true;
                    
                    // Ýlk doðrulamada profil resmi oluþtur
                    if (string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        user.ProfileImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random";
                    }

                    await _firebaseClient
                        .Child(Constants.UsersCollection)
                        .Child(user.UserId)
                        .PutAsync(user);

                    // Ýlk giriþ: Profil oluþtur
                    await _userProfileService.CreateUserProfileAsync(user);
                }

                // 5?? Aktif kullanýcý kontrolü
                if (!user.IsActive)
                {
                    return ServiceResult<AppUser>.FailureResult(
                        "Hesap devre dýþý",
                        "Hesabýnýz yönetici tarafýndan devre dýþý býrakýlmýþ"
                    );
                }

                // 6?? Son giriþ zamanýný güncelle
                user.LastLoginAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 7?? Oturum bilgisini sakla
                _currentUser = user;
                if (request.RememberMe)
                {
                    await SaveUserSessionAsync(user, _authLink.FirebaseToken);
                }

                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(true));

                return ServiceResult<AppUser>.SuccessResult(user, "Giriþ baþarýlý!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? LoginAsync hatasý: {ex.Message}");
                return ServiceResult<AppUser>.FailureResult("Giriþ sýrasýnda hata", ex.Message);
            }
        }

        #endregion

        #region Email Verification

        /// <summary>
        /// Doðrulama e-postasýný yeniden gönderir (Firebase native)
        /// </summary>
        public async Task<ServiceResult<bool>> SendVerificationCodeAsync(string email)
        {
            try
            {
                // Firebase'de oturum açmýþ kullanýcýya yeniden doðrulama linki gönder
                if (_authLink == null)
                {
                    // Eðer oturum yoksa, e-posta ile kullanýcýyý bul ve bilgilendir
                    return ServiceResult<bool>.SuccessResult(
                        true,
                        "Lütfen giriþ yaparak doðrulama linkini alýn."
                    );
                }

                await _authProvider.SendEmailVerificationAsync(_authLink.FirebaseToken);

                System.Diagnostics.Debug.WriteLine($"? Firebase doðrulama linki yeniden gönderildi: {email}");

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Doðrulama linki e-postanýza gönderildi. Lütfen e-postanýzdaki linke týklayýn."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? SendVerificationCodeAsync hatasý: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Doðrulama linki gönderilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Firebase'den e-posta doðrulama durumunu kontrol eder ve günceller
        /// </summary>
        public async Task<ServiceResult<bool>> VerifyEmailAsync(Models.VerificationRequest request)
        {
            try
            {
                // Firebase Authentication'da kullanýcý giriþ yap
                FirebaseAuthLink authLink;
                try
                {
                    authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                        request.Email.ToLower(),
                        "DUMMY_PASSWORD" // Þifre gerekmiyor, sadece token yenilemek için
                    );
                }
                catch
                {
                    // Kullanýcý þifresiz kontrol edemeyiz, manuel refresh gerekiyor
                    return ServiceResult<bool>.FailureResult(
                        "Doðrulama kontrol edilemedi",
                        "Lütfen e-postanýzdaki linke týklayýn ve ardýndan giriþ yapýn."
                    );
                }

                // Token'ý refresh et ve doðrulama durumunu kontrol et
                var refreshedAuth = await _authProvider.RefreshAuthAsync(authLink);
                
                if (!refreshedAuth.User.IsEmailVerified)
                {
                    return ServiceResult<bool>.FailureResult(
                        "E-posta henüz doðrulanmadý",
                        "Lütfen e-postanýzdaki linke týklayýn."
                    );
                }

                // Realtime Database'i güncelle
                var users = await _firebaseClient.Child(Constants.UsersCollection)
                    .OrderBy("Email").EqualTo(request.Email.ToLower()).OnceAsync<AppUser>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null)
                    return ServiceResult<bool>.FailureResult("Kullanýcý bulunamadý");

                var user = userEntry.Object;
                user.IsEmailVerified = true;

                if (string.IsNullOrEmpty(user.ProfileImageUrl))
                {
                    user.ProfileImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random";
                }

                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);

                // Profil oluþtur
                await _userProfileService.CreateUserProfileAsync(user);

                return ServiceResult<bool>.SuccessResult(true, "E-posta doðrulandý! Þimdi giriþ yapabilirsiniz.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? VerifyEmailAsync hatasý: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Doðrulama hatasý", ex.Message);
            }
        }

        #endregion

        #region Password Reset

        /// <summary>
        /// Firebase'in native þifre sýfýrlama e-postasýný gönderir
        /// </summary>
        public async Task<ServiceResult<bool>> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return ServiceResult<bool>.FailureResult("Hata", "E-posta adresi gerekli");

                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("Baðlantý Hatasý", "Ýnternet yok");

                // Rate limiting
                var limitCheck = RateLimiters.PasswordReset.CheckLimit(email);
                if (!limitCheck.IsAllowed)
                    return ServiceResult<bool>.FailureResult("Çok fazla deneme", limitCheck.Message);

                // ?? SADECE Firebase'in native þifre sýfýrlama sistemini kullan
                try
                {
                    await _authProvider.SendPasswordResetEmailAsync(email.ToLower());
                    
                    System.Diagnostics.Debug.WriteLine($"? Firebase þifre sýfýrlama linki gönderildi: {email}");
                }
                catch (FirebaseAuthException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Firebase password reset hatasý: {ex.Message}");
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Eðer bu e-posta kayýtlýysa, þifre sýfýrlama linki gönderildi. Lütfen e-postanýzý kontrol edin."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? SendPasswordResetEmailAsync hatasý: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        /// <summary>
        /// Firebase native þifre sýfýrlama kullanýldýðý için bu metod kullanýlmýyor.
        /// Kullanýcý e-postaadaki linke týklayýp Firebase sayfasýnda þifresini sýfýrlýyor.
        /// </summary>
        public async Task<ServiceResult<bool>> ResetPasswordAsync(string email, string verificationCode, string newPassword)
        {
            // ?? Firebase native kullanýldýðý için bu metod deprecated
            await Task.CompletedTask;
            
            return ServiceResult<bool>.FailureResult(
                "Bu özellik artýk kullanýlmýyor",
                "Lütfen e-postaunuza gönderilen Firebase linkini kullanarak þifrenizi sýfýrlayýn."
            );
        }

        #endregion

        #region Email Change

        /// <summary>
        /// Firebase'de e-posta deðiþtirir ve otomatik doðrulama linki gönderir
        /// </summary>
        public async Task<ServiceResult<bool>> ChangeEmailAsync(string currentEmail, string newEmail, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentEmail) || string.IsNullOrWhiteSpace(newEmail) || string.IsNullOrWhiteSpace(password))
                    return ServiceResult<bool>.FailureResult("Hata", "Tüm alanlar gerekli");

                if (!InputSanitizer.IsValidEmail(newEmail))
                    return ServiceResult<bool>.FailureResult("Hata", "Geçersiz e-posta formatý");

                if (!newEmail.ToLower().EndsWith(Constants.UniversityEmailDomain))
                    return ServiceResult<bool>.FailureResult("Hata", $"Sadece {Constants.UniversityEmailDomain} uzantýlý e-postalar kabul edilir");

                // 1?? Firebase ile tekrar giriþ yap (güvenlik)
                FirebaseAuthLink authLink;
                try
                {
                    authLink = await _authProvider.SignInWithEmailAndPasswordAsync(currentEmail.ToLower(), password);
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                // 2?? Firebase'de e-posta deðiþtir (otomatik doðrulama linki gönderir)
                try
                {
                    await _authProvider.ChangeUserEmail(authLink.FirebaseToken, newEmail.ToLower());
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                // 3?? Realtime Database'i güncelle
                var users = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .OrderBy("Email")
                    .EqualTo(currentEmail.ToLower())
                    .OnceAsync<AppUser>();

                var userEntry = users.FirstOrDefault();
                if (userEntry != null)
                {
                    var user = userEntry.Object;
                    user.Email = newEmail.ToLower();
                    user.IsEmailVerified = false; // Yeni e-posta doðrulanmalý

                    await _firebaseClient
                        .Child(Constants.UsersCollection)
                        .Child(user.UserId)
                        .PutAsync(user);

                    System.Diagnostics.Debug.WriteLine($"? E-posta deðiþtirildi: {currentEmail} ? {newEmail}");
                }

                // 4?? Yeni e-postaya doðrulama linki gönder
                try
                {
                    var refreshedAuth = await _authProvider.RefreshAuthAsync(authLink);
                    await _authProvider.SendEmailVerificationAsync(refreshedAuth.FirebaseToken);
                    
                    System.Diagnostics.Debug.WriteLine($"?? Yeni e-postaya doðrulama linki gönderildi: {newEmail}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"?? Doðrulama linki gönderilemedi: {ex.Message}");
                }

                return ServiceResult<bool>.SuccessResult(
                    true,
                    $"E-posta adresiniz {newEmail} olarak deðiþtirildi. Lütfen yeni e-postanýza gönderilen doðrulama linkine týklayýn."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? ChangeEmailAsync hatasý: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        /// <summary>
        /// Firebase native e-posta doðrulama kullanýldýðý için bu metod kullanýlmýyor.
        /// Kullanýcý e-postadaki linke týklayýp Firebase otomatik doðruluyor.
        /// </summary>
        public async Task<ServiceResult<bool>> VerifyNewEmailAsync(string newEmail, string verificationCode)
        {
            // ?? Firebase native kullanýldýðý için bu metod deprecated
            await Task.CompletedTask;
            
            return ServiceResult<bool>.FailureResult(
                "Bu özellik artýk kullanýlmýyor",
                "Lütfen e-postaunuza gönderilen Firebase linkine týklayarak yeni e-postanýzý doðrulayýn."
            );
        }

        #endregion

        #region Password Change

        public async Task<ServiceResult<bool>> ChangePasswordAsync(string email, string currentPassword, string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                    return ServiceResult<bool>.FailureResult("Hata", "Tüm alanlar gerekli");

                if (newPassword.Length < Constants.MinPasswordLength)
                    return ServiceResult<bool>.FailureResult("Hata", $"Yeni þifre en az {Constants.MinPasswordLength} karakter olmalýdýr");

                if (currentPassword == newPassword)
                    return ServiceResult<bool>.FailureResult("Hata", "Yeni þifre, mevcut þifre ile ayný olamaz");

                // Firebase ile þifre deðiþtir
                try
                {
                    var authLink = await _authProvider.SignInWithEmailAndPasswordAsync(email.ToLower(), currentPassword);
                    await _authProvider.ChangeUserPassword(authLink.FirebaseToken, newPassword);

                    System.Diagnostics.Debug.WriteLine($"? Þifre deðiþtirildi: {email}");

                    return ServiceResult<bool>.SuccessResult(true, "Þifreniz baþarýyla deðiþtirildi!");
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? ChangePasswordAsync hatasý: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        #endregion

        #region Logout & Session

        public async Task<ServiceResult<bool>> LogoutAsync()
        {
            try
            {
                Console.WriteLine("?? Çýkýþ iþlemi baþlatýlýyor...");

                _currentUser = null;
                _authLink = null;
                await ClearUserSessionAsync();

                var userStateService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                userStateService?.ClearUser();

                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(false));

                return ServiceResult<bool>.SuccessResult(true, "Çýkýþ baþarýlý");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Çýkýþ yapýlamadý", ex.Message);
            }
        }

        public async Task<AppUser> GetCurrentUserAsync()
        {
            if (_currentUser != null)
                return _currentUser;

            var userId = Preferences.Get("current_user_id", string.Empty);
            if (string.IsNullOrEmpty(userId))
                return null;

            try
            {
                if (!NetworkHelper.HasInternetConnection())
                    return _currentUser;

                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<AppUser>();

                _currentUser = user;
                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? GetCurrentUser hatasý: {ex.Message}");
                return _currentUser;
            }
        }

        public bool IsUserLoggedIn()
        {
            return _currentUser != null || !string.IsNullOrEmpty(Preferences.Get("current_user_id", string.Empty));
        }

        #endregion

        #region Validation

        public Models.ValidationResult ValidateRegistration(Models.RegisterRequest request)
        {
            var result = new Models.ValidationResult();

            if (string.IsNullOrWhiteSpace(request.FirstName))
                result.AddError("Ad alaný boþ býrakýlamaz");
            else if (!InputSanitizer.IsValidName(request.FirstName))
                result.AddError("Ad alaný geçersiz karakterler içeriyor");

            if (string.IsNullOrWhiteSpace(request.LastName))
                result.AddError("Soyad alaný boþ býrakýlamaz");
            else if (!InputSanitizer.IsValidName(request.LastName))
                result.AddError("Soyad alaný geçersiz karakterler içeriyor");

            if (string.IsNullOrWhiteSpace(request.Email))
                result.AddError("E-posta alaný boþ býrakýlamaz");
            else if (!InputSanitizer.IsValidEmail(request.Email))
                result.AddError("Geçersiz e-posta formatý");
            else if (!request.Email.ToLower().EndsWith(Constants.UniversityEmailDomain))
                result.AddError($"Sadece {Constants.UniversityEmailDomain} uzantýlý e-postalar kabul edilir");

            if (string.IsNullOrWhiteSpace(request.Password))
                result.AddError("Þifre alaný boþ býrakýlamaz");
            else if (request.Password.Length < Constants.MinPasswordLength)
                result.AddError($"Þifre en az {Constants.MinPasswordLength} karakter olmalýdýr");

            if (request.Password != request.PasswordConfirm)
                result.AddError("Þifreler eþleþmiyor");

            return result;
        }

        public Models.ValidationResult ValidateLogin(Models.LoginRequest request)
        {
            var result = new Models.ValidationResult();

            if (string.IsNullOrWhiteSpace(request.Email))
                result.AddError("E-posta alaný boþ býrakýlamaz");

            if (string.IsNullOrWhiteSpace(request.Password))
                result.AddError("Þifre alaný boþ býrakýlamaz");

            return result;
        }

        #endregion

        #region Helper Methods

        private async Task SaveUserSessionAsync(AppUser user, string firebaseToken)
        {
            Preferences.Set("current_user_id", user.UserId);
            Preferences.Set("current_user_email", user.Email);
            Preferences.Set("firebase_token", firebaseToken);
            await Task.CompletedTask;
        }

        private async Task ClearUserSessionAsync()
        {
            Preferences.Remove("current_user_id");
            Preferences.Remove("current_user_email");
            Preferences.Remove("firebase_token");
            await Task.CompletedTask;
        }

        private string GetFriendlyErrorMessage(FirebaseAuthException ex)
        {
            return ex.Reason switch
            {
                AuthErrorReason.EmailExists => "Bu e-posta adresi zaten kayýtlý",
                AuthErrorReason.InvalidEmailAddress => "Geçersiz e-posta adresi",
                AuthErrorReason.WeakPassword => "Þifre çok zayýf, en az 6 karakter olmalý",
                AuthErrorReason.WrongPassword => "E-posta veya þifre hatalý",
                AuthErrorReason.UserNotFound => "Kullanýcý bulunamadý",
                AuthErrorReason.TooManyAttemptsTryLater => "Çok fazla deneme yaptýnýz, lütfen daha sonra tekrar deneyin",
                AuthErrorReason.UserDisabled => "Hesabýnýz devre dýþý býrakýlmýþ",
                AuthErrorReason.InvalidIDToken => "Oturum süresi dolmuþ, lütfen tekrar giriþ yapýn",
                _ => $"Kimlik doðrulama hatasý: {ex.Message}"
            };
        }

        #endregion
    }
}
