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
using KamPay.Security;
using AppUser = KamPay.Models.User;
using System.Net.Http;
using System.Net.Http.Json;

namespace KamPay.Services
{
    /// <summary>
    /// 🔥 Firebase Authentication kullanan authentication servisi
    /// Manuel şifre hash'leme yerine Firebase'in güvenli authentication sistemini kullanır
    /// ✅ "Beni Hatırla" özelliği ile otomatik giriş desteği
    /// ✅ DI ile FirebaseAuthProvider ve FirebaseClient kullanımı
    /// 🔒 GÜVENLIK: Tüm hassas bilgiler SecureStorage'da saklanır
    /// </summary>
    public class FirebaseAuthService : IAuthenticationService
    {
        private readonly FirebaseAuthProvider _authProvider;
        private readonly FirebaseClient _firebaseClient;
        private readonly IEmailService _emailService;
        private readonly IUserProfileService _userProfileService;
        private readonly ISecurityAuditService _securityAudit; // ✅ Ekle
        private AppUser? _currentUser;
        private FirebaseAuthLink? _authLink;

        // 🔒 SecureStorage anahtarları
        private const string KEY_USER_ID = "secure_user_id";
        private const string KEY_USER_EMAIL = "secure_user_email";
        private const string KEY_FIREBASE_TOKEN = "secure_firebase_token";
        private const string KEY_REMEMBER_ME = "secure_remember_me";
        private const string KEY_TOKEN_EXPIRY = "secure_token_expiry";

        // ✅ YENİ: Constructor artık tüm bağımlılıkları DI'den alıyor
        public FirebaseAuthService(
            FirebaseAuthProvider authProvider,
            FirebaseClient firebaseClient,
            IEmailService emailService,
            IUserProfileService userProfileService,
            ISecurityAuditService securityAudit) 
        {
            _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _securityAudit = securityAudit; 
            
            System.Diagnostics.Debug.WriteLine("✅ FirebaseAuthService oluşturuldu (DI ile)");
        }

        #region Registration

        public async Task<ServiceResult<AppUser>> RegisterAsync(Models.RegisterRequest request)
        {
            try
            {
                if (request == null)
                    return ServiceResult<AppUser>.FailureResult("Hata", "Veriler boş.");

                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<AppUser>.FailureResult("Bağlantı Hatası", "İnternet yok.");

                var validation = ValidateRegistration(request);
                if (!validation.IsValid)
                    return ServiceResult<AppUser>.FailureResult("Geçersiz bilgiler", validation.Errors.ToArray());

                string safeEmail = request.Email?.Trim().ToLower() ?? string.Empty;

                // 1️⃣ Firebase Authentication ile kullanıcı oluştur
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
                    return ServiceResult<AppUser>.FailureResult("Kayıt hatası", GetFriendlyErrorMessage(ex));
                }

                // 2️⃣ Kullanıcı bilgilerini Realtime Database'e kaydet
                var user = new AppUser
                {
                    UserId = authResult.User.LocalId, // Firebase UID kullan
                    FirstName = InputSanitizer.SanitizeName(request.FirstName?.Trim() ?? ""),
                    LastName = InputSanitizer.SanitizeName(request.LastName?.Trim() ?? ""),
                    Email = safeEmail,
                    Username = $"{request.FirstName.ToLower().Replace(" ", "")}{new Random().Next(100, 999)}",
                    PhoneNumber = "",
                    PasswordHash = "", // Artık Firebase yönetiyor
                    IsEmailVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);

                // 3️⃣ Firebase Email Verification gönder (SADECE BU!)
                try
                {
                    await _authProvider.SendEmailVerificationAsync(authResult.FirebaseToken);
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Firebase email verification gönderildi: {user.Email}");
                    System.Diagnostics.Debug.WriteLine($"📧 Kullanıcı e-postasındaki linke tıklayarak doğrulayacak");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Firebase email verification gönderilemedi: {ex.Message}");
                    return ServiceResult<AppUser>.FailureResult("E-posta doğrulama hatası", "Doğrulama e-postası gönderilemedi");
                }

                return ServiceResult<AppUser>.SuccessResult(user, "Kayıt başarılı! E-postanıza gönderilen linke tıklayarak hesabınızı doğrulayın.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ RegisterAsync hatası: {ex.Message}");
                return ServiceResult<AppUser>.FailureResult("Kayıt hatası", ex.Message);
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
                    return ServiceResult<AppUser>.FailureResult("Giriş bilgileri geçersiz", validation.Errors.ToArray());

                // 1️⃣ Firebase Authentication ile giriş yap
                try
                {
                    _authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                        request.Email.ToLower(),
                        request.Password
                    );
                    // Geliştirme aşaması için Token'ı konsola yazdır (Postman'de kullanmak için):
                    System.Diagnostics.Debug.WriteLine($"\n\n=== POSTMAN ICIN BEARER TOKEN ===\n{_authLink.FirebaseToken}\n=================================\n\n");

                    // 🌟 YENİ: Firebase Token'ını KamPay.API'ye gönderip kendi Custom JWT'mizi alıyoruz 🌟
                    try
                    {
                        using var apiHttpClient = new System.Net.Http.HttpClient();

                        // NOT: Geliştirme ortamında (localhost) test ediyorsanız doğru IP'yi (örn; Android emülatör için 10.0.2.2) ayarlamalısınız.
                        // Canlı sunucunuz varsa direkt onun URL'sini yazın: https://YOUR_API_DOMAIN/api/Auth/login
                        // https://localhost:7143/api/Auth/login YERİNE:
                        string apiUrl = "http://192.168.88.177:5011/api/Auth/login"; // Kendi IP'nizi ve API portunuzu yazın. SSL sorunu yaşamamak için http tavsiye edilir

                        var loginPayload = new { IdToken = _authLink.FirebaseToken };
                        var apiResponse = await apiHttpClient.PostAsJsonAsync(apiUrl, loginPayload);

                        if (apiResponse.IsSuccessStatusCode)
                        {
                            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var responseData = await apiResponse.Content.ReadFromJsonAsync<Models.ApiLoginResponseDto>(options);
                            if (responseData != null && !string.IsNullOrEmpty(responseData.Token))
                            {
                                await Microsoft.Maui.Storage.SecureStorage.SetAsync("KAMPAY_API_JWT", responseData.Token);
                                System.Diagnostics.Debug.WriteLine($"✅ KamPay API JWT başarıyla alındı ve kaydedildi.\n\n=== SİZİN API'NIZIN JWT'Sİ ===\n{responseData.Token}\n============================\n\n");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("⚠️ API başarılı yanıt döndü ama Token okunamadı (NULL)!");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ API Login Hatası: {apiResponse.StatusCode}");
                            var errorRaw = await apiResponse.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"API DETAY: {errorRaw}");
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ API'ye erişilirken hata oluştu: {apiEx.Message}");
                    }
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<AppUser>.FailureResult("Giriş başarısız", GetFriendlyErrorMessage(ex));
                }

                // 2️⃣ Firebase'den e-posta doğrulama durumunu kontrol et
                if (!_authLink.User.IsEmailVerified)
                {
                    // Kullanıcı doğrulama e-postaasını almamışsa tekrar gönder
                    try
                    {
                        await _authProvider.SendEmailVerificationAsync(_authLink.FirebaseToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Yeniden doğrulama e-postası gönderilemedi: {ex.Message}");
                    }

                    return ServiceResult<AppUser>.FailureResult(
                        "E-posta doğrulanmamış",
                        "Lütfen e-postanıza gönderilen linke tıklayarak hesabınızı doğrulayın. Yeni bir doğrulama linki gönderildi."
                    );
                }

                // 3️⃣ Kullanıcı bilgilerini Realtime Database'den al
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(_authLink.User.LocalId)
                    .OnceSingleAsync<AppUser>();

                if (user == null)
                    return ServiceResult<AppUser>.FailureResult("Kullanıcı bulunamadı", "Hesap bilgileri eksik.");

                // 4️⃣ Realtime Database'deki doğrulama durumunu güncelle
                if (!user.IsEmailVerified)
                {
                    user.IsEmailVerified = true;
                    
                    // İlk doğrulamada profil resmi oluştur
                    if (string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        user.ProfileImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random";
                    }

                    await _firebaseClient
                        .Child(Constants.UsersCollection)
                        .Child(user.UserId)
                        .PutAsync(user);

                    // İlk giriş: Profil oluştur
                    await _userProfileService.CreateUserProfileAsync(user);
                }

                // 5️⃣ Aktif kullanıcı kontrolü
                if (!user.IsActive)
                {
                    return ServiceResult<AppUser>.FailureResult(
                        "Hesap devre dışı",
                        "Hesabınız yönetici tarafından devre dışı bırakılmış"
                    );
                }

                // 6️⃣ Son giriş zamanını güncelle
                user.LastLoginAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 7️⃣ Oturum bilgisini sakla
                _currentUser = user;
                
                // 🔒 GÜVENLIK: Hassas bilgileri SecureStorage'da sakla
                await SaveUserSessionAsync(user, _authLink.FirebaseToken, request.RememberMe);

                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(true));

                return ServiceResult<AppUser>.SuccessResult(user, "Giriş başarılı!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoginAsync hatası: {ex.Message}");
                return ServiceResult<AppUser>.FailureResult("Giriş sırasında hata", ex.Message);
            }
        }

        #endregion

        #region Auto Login (Remember Me)

        /// <summary>
        /// 🔒 GÜVENLIK: Uygulama başlangıcında otomatik giriş kontrolü (SecureStorage)
        /// "Beni Hatırla" işaretliyse ve token geçerliyse otomatik giriş yapar
        /// </summary>
        public async Task<ServiceResult<AppUser>> TryAutoLoginAsync()
        {
            try
            {
                Console.WriteLine("🔐 Otomatik giriş kontrolü başlatılıyor...");

                // 1️⃣ "Beni Hatırla" kontrolü - SecureStorage'dan al
                var rememberMeStr = await SecureStorage.GetAsync(KEY_REMEMBER_ME);
                var rememberMe = !string.IsNullOrEmpty(rememberMeStr) && bool.Parse(rememberMeStr);
                
                if (!rememberMe)
                {
                    Console.WriteLine("⏭️ Beni Hatırla işaretli değil, otomatik giriş yapılmayacak");
                    return ServiceResult<AppUser>.FailureResult("Otomatik giriş yok", "Kullanıcı beni hatırla seçeneğini işaretlememiş");
                }

                // 2️⃣ Session bilgilerini al - SecureStorage'dan
                var userId = await SecureStorage.GetAsync(KEY_USER_ID);
                var firebaseToken = await SecureStorage.GetAsync(KEY_FIREBASE_TOKEN);
                var tokenExpiryStr = await SecureStorage.GetAsync(KEY_TOKEN_EXPIRY);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(firebaseToken))
                {
                    Console.WriteLine("⚠️ Session bilgileri eksik");
                    return ServiceResult<AppUser>.FailureResult("Session yok", "Kaydedilmiş oturum bulunamadı");
                }

                Console.WriteLine($"✅ SecureStorage'dan session bilgileri alındı: UserId={userId.Substring(0, Math.Min(8, userId.Length))}...");

                // 3️⃣ Token süresini kontrol et
                if (!string.IsNullOrEmpty(tokenExpiryStr) && DateTime.TryParse(tokenExpiryStr, out var tokenExpiry))
                {
                    if (DateTime.UtcNow >= tokenExpiry)
                    {
                        Console.WriteLine("🔄 Token süresi dolmuş, yenileniyor...");
                        
                        // Token yenileme
                        try
                        {
                            var refreshedAuth = await _authProvider.RefreshAuthAsync(new FirebaseAuthLink(_authProvider, new Firebase.Auth.FirebaseAuth
                            {
                                FirebaseToken = firebaseToken,
                                User = new Firebase.Auth.User { LocalId = userId }
                            }));

                            // Yenilenen token'ı kaydet
                            _authLink = refreshedAuth;
                            await SaveUserSessionAsync(null, refreshedAuth.FirebaseToken, true, refreshedAuth.ExpiresIn);

                            Console.WriteLine("✅ Token başarıyla yenilendi");

                            // 🌟 YENİ: Firebase Token yenilendiğinde kendi API'mize de bildirip API JWT'mizi yeniliyoruz 🌟
                            try
                            {
                                using var apiHttpClient = new System.Net.Http.HttpClient();
                                string apiUrl = "http://192.168.88.177:5011/api/Auth/login"; 
                                var loginPayload = new { IdToken = refreshedAuth.FirebaseToken };
                                var apiResponse = await apiHttpClient.PostAsJsonAsync(apiUrl, loginPayload);

                                if (apiResponse.IsSuccessStatusCode)
                                {
                                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                    var responseData = await apiResponse.Content.ReadFromJsonAsync<Models.ApiLoginResponseDto>(options);
                                    if (responseData != null && !string.IsNullOrEmpty(responseData.Token))
                                    {
                                        await Microsoft.Maui.Storage.SecureStorage.SetAsync("KAMPAY_API_JWT", responseData.Token);
                                        System.Diagnostics.Debug.WriteLine($"✅ KamPay API JWT başarıyla yenilendi.\n\n=== SİZİN API'NIZIN JWT'Sİ ===\n{responseData.Token}\n============================\n\n");
                                    }
                                }
                            }
                            catch (Exception apiEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ API Token yenilerken hata: {apiEx.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Token yenileme hatası: {ex.Message}");
                            await ClearUserSessionAsync();
                            return ServiceResult<AppUser>.FailureResult("Token yenilenemedi", "Lütfen tekrar giriş yapın");
                        }
                    }
                }

                // 4️⃣ İnternet kontrolü
                if (!NetworkHelper.HasInternetConnection())
                {
                    Console.WriteLine("⚠️ İnternet bağlantısı yok, cache'den kullanıcı yükleniyor");
                    
                    // Cache'den kullanıcı bilgilerini al (offline destek)
                    var cachedEmail = await SecureStorage.GetAsync(KEY_USER_EMAIL);
                    
                    if (!string.IsNullOrEmpty(cachedEmail))
                    {
                        _currentUser = new AppUser
                        {
                            UserId = userId,
                            Email = cachedEmail,
                            // Diğer bilgiler online olunca güncellenecek
                        };
                        return ServiceResult<AppUser>.SuccessResult(_currentUser, "Offline modda giriş yapıldı");
                    }
                }

                // 5️⃣ Kullanıcı bilgilerini Firebase'den al
                var user = await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(userId)
                    .OnceSingleAsync<AppUser>();

                if (user == null)
                {
                    Console.WriteLine("❌ Kullanıcı bulunamadı");
                    await ClearUserSessionAsync();
                    return ServiceResult<AppUser>.FailureResult("Kullanıcı bulunamadı", "Hesap silinmiş veya devre dışı bırakılmış olabilir");
                }

                // 6️⃣ Hesap aktiflik kontrolü
                if (!user.IsActive)
                {
                    Console.WriteLine("❌ Hesap devre dışı");
                    await ClearUserSessionAsync();
                    return ServiceResult<AppUser>.FailureResult("Hesap devre dışı", "Hesabınız yönetici tarafından devre dışı bırakılmış");
                }

                // 7️⃣ Son giriş zamanını güncelle
                user.LastLoginAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child(Constants.UsersCollection)
                    .Child(user.UserId)
                    .PutAsync(user);

                // 8️⃣ Current user'ı ayarla
                _currentUser = user;

                // 🌟 YENİ: Firebase Token henüz süresi dolmamışsa bile API tarafında JWT var mı yok mu/geçerli mi kontrolü yapılmıyordu
                // LocalStorage'de API Token yoksa yeniden Login ol
                var apiToken = await Microsoft.Maui.Storage.SecureStorage.GetAsync("KAMPAY_API_JWT");
                if (string.IsNullOrEmpty(apiToken))
                {
                    try
                    {
                        using var apiHttpClient = new System.Net.Http.HttpClient();
                        string apiUrl = "http://192.168.88.177:5011/api/Auth/login"; 
                        var loginPayload = new { IdToken = firebaseToken };
                        var apiResponse = await apiHttpClient.PostAsJsonAsync(apiUrl, loginPayload);

                        if (apiResponse.IsSuccessStatusCode)
                        {
                            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var responseData = await apiResponse.Content.ReadFromJsonAsync<Models.ApiLoginResponseDto>(options);
                            if (responseData != null && !string.IsNullOrEmpty(responseData.Token))
                            {
                                await Microsoft.Maui.Storage.SecureStorage.SetAsync("KAMPAY_API_JWT", responseData.Token);
                                System.Diagnostics.Debug.WriteLine($"✅ TryAutoLogin: KamPay API JWT yeniden alındı.\n\n=== SİZİN API'NIZIN JWT'Sİ ===\n{responseData.Token}\n============================\n\n");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ TryAutoLogin API Login Hatası: {apiResponse.StatusCode}");
                            var errorRaw = await apiResponse.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"API DETAY: {errorRaw}");
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ TryAutoLogin API çağırma hatası: {apiEx.Message}");
                    }
                }

                Console.WriteLine($"✅ Otomatik giriş başarılı: {user.Email}");
                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(true));

                return ServiceResult<AppUser>.SuccessResult(user, "Otomatik giriş başarılı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TryAutoLoginAsync hatası: {ex.Message}");
                await ClearUserSessionAsync();
                return ServiceResult<AppUser>.FailureResult("Otomatik giriş hatası", ex.Message);
            }
        }

        #endregion

        #region Email Verification

        /// <summary>
        /// Doğrulama e-postasını yeniden gönderir (Firebase native)
        /// </summary>
        public async Task<ServiceResult<bool>> SendVerificationCodeAsync(string email)
        {
            try
            {
                // Firebase'de oturum açmış kullanıcıya yeniden doğrulama linki gönder
                if (_authLink == null)
                {
                    // Eğer oturum yoksa, e-posta ile kullanıcıyı bul ve bilgilendir
                    return ServiceResult<bool>.SuccessResult(
                        true,
                        "Lütfen giriş yaparak doğrulama linkini alın."
                    );
                }

                await _authProvider.SendEmailVerificationAsync(_authLink.FirebaseToken);

                System.Diagnostics.Debug.WriteLine($"✅ Firebase doğrulama linki yeniden gönderildi: {email}");

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Doğrulama linki e-postanıza gönderildi. Lütfen e-postanızdaki linke tıklayın."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SendVerificationCodeAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Doğrulama linki gönderilemedi", ex.Message);
            }
        }

        /// <summary>
        /// Firebase'den e-posta doğrulama durumunu kontrol eder ve günceller
        /// </summary>
        public async Task<ServiceResult<bool>> VerifyEmailAsync(Models.VerificationRequest request)
        {
            try
            {
                // Firebase Authentication'da kullanıcı giriş yap
                FirebaseAuthLink authLink;
                try
                {
                    authLink = await _authProvider.SignInWithEmailAndPasswordAsync(
                        request.Email.ToLower(),
                        "DUMMY_PASSWORD" // Şifre gerekmiyor, sadece token yenilemek için
                    );
                }
                catch
                {
                    // Kullanıcı şifresiz kontrol edemeyiz, manuel refresh gerekiyor
                    return ServiceResult<bool>.FailureResult(
                        "Doğrulama kontrol edilemedi",
                        "Lütfen e-postaınızdaki linke tıklayın ve ardından giriş yapın."
                    );
                }

                // Token'ı refresh et ve doğrulama durumunu kontrol et
                var refreshedAuth = await _authProvider.RefreshAuthAsync(authLink);
                
                if (!refreshedAuth.User.IsEmailVerified)
                {
                    return ServiceResult<bool>.FailureResult(
                        "E-posta henüz doğrulanmadı",
                        "Lütfen e-postaalıanızdaki linke tıklayın."
                    );
                }

                // Realtime Database'i güncelle
                var users = await _firebaseClient.Child(Constants.UsersCollection)
                    .OrderBy("Email").EqualTo(request.Email.ToLower()).OnceAsync<AppUser>();

                var userEntry = users.FirstOrDefault();
                if (userEntry == null)
                    return ServiceResult<bool>.FailureResult("Kullanıcı bulunamadı");

                var user = userEntry.Object;
                user.IsEmailVerified = true;

                if (string.IsNullOrEmpty(user.ProfileImageUrl))
                {
                    user.ProfileImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FirstName)}+{Uri.EscapeDataString(user.LastName)}&background=random";
                }

                await _firebaseClient.Child(Constants.UsersCollection).Child(user.UserId).PutAsync(user);

                // Profil oluştur
                await _userProfileService.CreateUserProfileAsync(user);

                return ServiceResult<bool>.SuccessResult(true, "E-posta doğrulandı! Şimdi giriş yapabilirsiniz.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ VerifyEmailAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Doğrulama hatası", ex.Message);
            }
        }

        #endregion

        #region Password Reset

        /// <summary>
        /// Firebase'in native şifre sıfırlama e-postasını gönderir
        /// </summary>
        public async Task<ServiceResult<bool>> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return ServiceResult<bool>.FailureResult("Hata", "E-posta adresi gerekli");

                if (!NetworkHelper.HasInternetConnection())
                    return ServiceResult<bool>.FailureResult("Bağlantı Hatası", "İnternet yok");

                // Rate limiting
                var limitCheck = RateLimiters.PasswordReset.CheckLimit(email);
                if (!limitCheck.IsAllowed)
                    return ServiceResult<bool>.FailureResult("Çok fazla deneme", limitCheck.Message);

                // 🔥 SADECE Firebase'in native şifre sıfırlama sistemini kullan
                try
                {
                    await _authProvider.SendPasswordResetEmailAsync(email.ToLower());
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Firebase şifre sıfırlama linki gönderildi: {email}");
                }
                catch (FirebaseAuthException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Firebase password reset hatası: {ex.Message}");
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                return ServiceResult<bool>.SuccessResult(
                    true,
                    "Eğer bu e-posta kayıtlıysa, şifre sıfırlama linki gönderildi. Lütfen e-postanızı kontrol edin."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SendPasswordResetEmailAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        /// <summary>
        /// Firebase native şifre sıfırlama kullanıldığı için bu metod kullanılmıyor.
        /// Kullanıcı e-postaadaki linke tıklayıp Firebase sayfasında şifresini sıfırlıyor.
        /// </summary>
        public async Task<ServiceResult<bool>> ResetPasswordAsync(string email, string verificationCode, string newPassword)
        {
            // 🔥 Firebase native kullanıldığı için bu metod deprecated
            await Task.CompletedTask;
            
            return ServiceResult<bool>.FailureResult(
                "Bu özellik artık kullanılmıyor",
                "Lütfen e-postaunuza gönderilen Firebase linkini kullanarak şifrenizi sıfırlayın."
            );
        }

        #endregion

        #region Email Change

        /// <summary>
        /// Firebase'de e-posta değiştirir ve otomatik doğrulama linki gönderir
        /// </summary>
        public async Task<ServiceResult<bool>> ChangeEmailAsync(string currentEmail, string newEmail, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentEmail) || string.IsNullOrWhiteSpace(newEmail) || string.IsNullOrWhiteSpace(password))
                    return ServiceResult<bool>.FailureResult("Hata", "Tüm alanlar gerekli");

                if (!InputSanitizer.IsValidEmail(newEmail))
                    return ServiceResult<bool>.FailureResult("Hata", "Geçersiz e-posta formatı");

                if (!newEmail.ToLower().EndsWith(Constants.UniversityEmailDomain))
                    return ServiceResult<bool>.FailureResult("Hata", $"Sadece {Constants.UniversityEmailDomain} uzantılı e-postalar kabul edilir");

                // 1️⃣ Firebase ile tekrar giriş yap (güvenlik)
                FirebaseAuthLink authLink;
                try
                {
                    authLink = await _authProvider.SignInWithEmailAndPasswordAsync(currentEmail.ToLower(), password);
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                // 2️⃣ Firebase'de e-posta değiştir (otomatik doğrulama linki gönderir)
                try
                {
                    await _authProvider.ChangeUserEmail(authLink.FirebaseToken, newEmail.ToLower());
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }

                // 3️⃣ Realtime Database'i güncelle
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
                    user.IsEmailVerified = false; // Yeni e-posta doğrulanmalı

                    await _firebaseClient
                        .Child(Constants.UsersCollection)
                        .Child(user.UserId)
                        .PutAsync(user);

                    System.Diagnostics.Debug.WriteLine($"✅ E-posta değiştirildi: {currentEmail} → {newEmail}");
                }

                // 4️⃣ Yeni e-postaya doğrulama linki gönder
                try
                {
                    var refreshedAuth = await _authProvider.RefreshAuthAsync(authLink);
                    await _authProvider.SendEmailVerificationAsync(refreshedAuth.FirebaseToken);
                    
                    System.Diagnostics.Debug.WriteLine($"📧 Yeni e-postaya doğrulama linki gönderildi: {newEmail}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Doğrulama linki gönderilemedi: {ex.Message}");
                }

                return ServiceResult<bool>.SuccessResult(
                    true,
                    $"E-posta adresiniz {newEmail} olarak değiştirildi. Lütfen yeni e-postanıza gönderilen doğrulama linkine tıklayın."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ChangeEmailAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        /// <summary>
        /// Firebase native e-posta doğrulama kullanıldığı için bu metod kullanılmıyor.
        /// Kullanıcı e-postadaki linke tıklayıp Firebase otomatik doğruluyor.
        /// </summary>
        public async Task<ServiceResult<bool>> VerifyNewEmailAsync(string newEmail, string verificationCode)
        {
            // 🔥 Firebase native kullanıldığı için bu metod deprecated
            await Task.CompletedTask;
            
            return ServiceResult<bool>.FailureResult(
                "Bu özellik artık kullanılmıyor",
                "Lütfen e-postaunuza gönderilen Firebase linkine tıklayıp yeni e-postanızı doğrulayın."
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
                    return ServiceResult<bool>.FailureResult("Hata", $"Yeni şifre en az {Constants.MinPasswordLength} karakter olmalıdır");

                if (currentPassword == newPassword)
                    return ServiceResult<bool>.FailureResult("Hata", "Yeni şifre, mevcut şifre ile aynı olamaz");

                // Firebase ile şifre değiştir
                try
                {
                    var authLink = await _authProvider.SignInWithEmailAndPasswordAsync(email.ToLower(), currentPassword);
                    await _authProvider.ChangeUserPassword(authLink.FirebaseToken, newPassword);

                    System.Diagnostics.Debug.WriteLine($"✅ Şifre değiştirildi: {email}");

                    return ServiceResult<bool>.SuccessResult(true, "Şifreniz başarıyla değiştirildi!");
                }
                catch (FirebaseAuthException ex)
                {
                    return ServiceResult<bool>.FailureResult("Hata", GetFriendlyErrorMessage(ex));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ChangePasswordAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Hata", ex.Message);
            }
        }

        #endregion

        #region Logout & Session

        public async Task<ServiceResult<bool>> LogoutAsync()
        {
            try
            {
                Console.WriteLine("🔓 Çıkış işlemi başlatılıyor...");

                _currentUser = null;
                _authLink = null;
                await ClearUserSessionAsync();

                // 🌟 YENİ: Custom API JWT Tokemimizi da silelim
                Microsoft.Maui.Storage.SecureStorage.Remove("KAMPAY_API_JWT");

                var userStateService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                userStateService?.ClearUser();

                // 🧹 Tüm UI cachelerini global olarak temizle
                try
                {
                    KamPay.ViewModels.ChatViewModel.ClearCache();
                    var productCache = Application.Current?.Handler?.MauiContext?.Services.GetService<KamPay.Services.IProductCacheService>();
                    if (productCache != null) await productCache.InvalidateCacheAsync();
                }
                catch (Exception cacheEx)
                {
                    Console.WriteLine($"⚠️ Cache temizleme hatası: {cacheEx.Message}");
                }

                WeakReferenceMessenger.Default.Send(new UserSessionChangedMessage(false));

                Console.WriteLine("✅ Çıkış başarıyla tamamlandı");

                return ServiceResult<bool>.SuccessResult(true, "Çıkış başarılı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LogoutAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Çıkış yapılamadı", ex.Message);
            }
        }

        public async Task<AppUser> GetCurrentUserAsync()
        {
            if (_currentUser != null)
                return _currentUser;

            var userId = await SecureStorage.GetAsync(KEY_USER_ID);
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
                System.Diagnostics.Debug.WriteLine($"❌ GetCurrentUser hatası: {ex.Message}");
                return _currentUser;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            try
            {
                // Hafızadaki mevcut session bilgilerini al
                var firebaseToken = await SecureStorage.GetAsync(KEY_FIREBASE_TOKEN);
                var tokenExpiryStr = await SecureStorage.GetAsync(KEY_TOKEN_EXPIRY);
                var userId = await SecureStorage.GetAsync(KEY_USER_ID);

                if (string.IsNullOrEmpty(firebaseToken) || string.IsNullOrEmpty(userId))
                    return null; // Geçerli oturum yok

                // Token'ın geçerlilik süresini kontrol et
                if (!string.IsNullOrEmpty(tokenExpiryStr) && DateTime.TryParse(tokenExpiryStr, out var tokenExpiry))
                {
                    // Token bitmesine 5 dakikadan az kaldıysa veya çoktan bittiyse yenile (refresh)
                    if (DateTime.UtcNow.AddMinutes(5) < tokenExpiry)
                    {
                        return firebaseToken; // Her şey yolunda, mevcut token hala geçerli
                    }
                }

                System.Diagnostics.Debug.WriteLine("🔄 Token süresi bitmek üzere, arka planda yenileniyor...");

                // Süre dolmuşsa veya 5 dakikadan az kalmışsa yeni bir token iste
                var refreshedAuth = await _authProvider.RefreshAuthAsync(new FirebaseAuthLink(_authProvider, new Firebase.Auth.FirebaseAuth
                {
                    FirebaseToken = firebaseToken,
                    User = new Firebase.Auth.User { LocalId = userId }
                }));

                // Yeni token bilgilerini RAM'de güncelle
                _authLink = refreshedAuth;

                // Storage'da güncellemek için Beni Hatırla durumunu oku
                var rememberMeStr = await SecureStorage.GetAsync(KEY_REMEMBER_ME);
                var rememberMe = !string.IsNullOrEmpty(rememberMeStr) && bool.Parse(rememberMeStr);

                await SaveUserSessionAsync(_currentUser, refreshedAuth.FirebaseToken, rememberMe, refreshedAuth.ExpiresIn);

                System.Diagnostics.Debug.WriteLine("✅ Yeni Token başarıyla oluşturuldu.");
                return refreshedAuth.FirebaseToken;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetValidTokenAsync hatası: {ex.Message}");
                // Bir nedenden yenilenemezse (internet yok vs.) elimizdeki son token'ı dönmeyi deneriz.
                return await SecureStorage.GetAsync(KEY_FIREBASE_TOKEN);
            }
        }

        /// <summary>
        /// Geçerli kullanıcının Firebase ID Token'ını döner.
        /// ProductApiService.SetAuthHeaderAsync() tarafından çağrılır.
        /// İç yapıda GetValidTokenAsync()'e delege eder.
        /// </summary>
        public async Task<string> GetCurrentUserTokenAsync()
        {
            return await GetValidTokenAsync();
        }

        public bool IsUserLoggedIn()
        {
            if (_currentUser != null) 
                return true;
            
            // 🔒 GÜVENLIK: SecureStorage'dan kontrol et
            try
            {
                var userId = SecureStorage.GetAsync(KEY_USER_ID).Result;
                return !string.IsNullOrEmpty(userId);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Validation

        public Models.ValidationResult ValidateRegistration(Models.RegisterRequest request)
        {
            var result = new Models.ValidationResult();

            if (string.IsNullOrWhiteSpace(request.FirstName))
                result.AddError("Ad alanı boş bırakılamaz");
            else if (!InputSanitizer.IsValidName(request.FirstName))
                result.AddError("Ad alanı geçersiz karakterler içeriyor");

            if (string.IsNullOrWhiteSpace(request.LastName))
                result.AddError("Soyad alanı boş bırakılamaz");
            else if (!InputSanitizer.IsValidName(request.LastName))
                result.AddError("Soyad alanı geçersiz karakterler içeriyor");

            if (string.IsNullOrWhiteSpace(request.Email))
                result.AddError("E-posta alanı boş bırakılamaz");
            else if (!InputSanitizer.IsValidEmail(request.Email))
                result.AddError("Geçersiz e-posta formatı");
            else if (!request.Email.ToLower().EndsWith(Constants.UniversityEmailDomain))
                result.AddError($"Sadece {Constants.UniversityEmailDomain} uzantılı e-postalar kabul edilir");

            if (string.IsNullOrWhiteSpace(request.Password))
                result.AddError("Şifre alanı boş bırakılamaz");
            else if (request.Password.Length < Constants.MinPasswordLength)
                result.AddError($"Şifre en az {Constants.MinPasswordLength} karakter olmalıdır");

            if (request.Password != request.PasswordConfirm)
                result.AddError("Şifreler eşleşmiyor");

            return result;
        }

        public Models.ValidationResult ValidateLogin(Models.LoginRequest request)
        {
            var result = new Models.ValidationResult();

            if (string.IsNullOrWhiteSpace(request.Email))
                result.AddError("E-posta alanı boş bırakılamaz");

            if (string.IsNullOrWhiteSpace(request.Password))
                result.AddError("Şifre alanı boş bırakılamaz");

            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 🔒 GÜVENLIK: Session bilgilerini SecureStorage'da saklar
        /// Tüm hassas bilgiler (user_id, token, email) güvenli şekilde şifrelenir
        /// </summary>
        private async Task SaveUserSessionAsync(AppUser? user, string firebaseToken, bool rememberMe, int? expiresIn = null)
        {
            try
            {
                Console.WriteLine("💾 Session kaydediliyor (SecureStorage)...");

                // User bilgilerini kaydet
                if (user != null)
                {
                    await SecureStorage.SetAsync(KEY_USER_ID, user.UserId);
                    await SecureStorage.SetAsync(KEY_USER_EMAIL, user.Email);
                    Console.WriteLine($"✅ User bilgileri SecureStorage'a kaydedildi: {user.Email}");
                }

                // Firebase token'ı kaydet
                await SecureStorage.SetAsync(KEY_FIREBASE_TOKEN, firebaseToken);

                // "Beni Hatırla" durumunu kaydet
                await SecureStorage.SetAsync(KEY_REMEMBER_ME, rememberMe.ToString());

                // Token expiry time'ı kaydet (varsayılan 1 saat)
                var expiryTime = DateTime.UtcNow.AddSeconds(expiresIn ?? 3600);
                await SecureStorage.SetAsync(KEY_TOKEN_EXPIRY, expiryTime.ToString("O")); // ISO 8601 format

                Console.WriteLine($"✅ Session güvenli şekilde kaydedildi - RememberMe: {rememberMe}, Token Expiry: {expiryTime:g}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SaveUserSessionAsync hatası: {ex.Message}");
                throw; // Kritik hata, üst katmana ilet
            }
        }

        /// <summary>
        /// 🗑️ Tüm session bilgilerini SecureStorage'dan temizler
        /// </summary>
        private async Task ClearUserSessionAsync()
        {
            try
            {
                Console.WriteLine("🗑️ Session temizleniyor (SecureStorage)...");

                SecureStorage.Remove(KEY_USER_ID);
                SecureStorage.Remove(KEY_USER_EMAIL);
                SecureStorage.Remove(KEY_FIREBASE_TOKEN);
                SecureStorage.Remove(KEY_REMEMBER_ME);
                SecureStorage.Remove(KEY_TOKEN_EXPIRY);

                Console.WriteLine("✅ Session güvenli şekilde temizlendi");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ClearUserSessionAsync hatası: {ex.Message}");
            }
        }

        private string GetFriendlyErrorMessage(FirebaseAuthException ex)
        {
            return ex.Reason switch
            {
                AuthErrorReason.EmailExists => "Bu e-posta adresi zaten kayıtlı",
                AuthErrorReason.InvalidEmailAddress => "Geçersiz e-posta adresi",
                AuthErrorReason.WeakPassword => "Şifre çok zayıf, en az 6 karakter olmalı",
                AuthErrorReason.WrongPassword => "E-posta veya şifre hatalı",
                AuthErrorReason.UserNotFound => "Kullanıcı bulunamadı",
                AuthErrorReason.TooManyAttemptsTryLater => "Çok fazla deneme yaptınız, lütfen daha sonra tekrar deneyin",
                AuthErrorReason.UserDisabled => "Hesabınız devre dışı bırakılmış",
                AuthErrorReason.InvalidIDToken => "Oturum süresi dolmuş, lütfen tekrar giriş yapın",
                _ => $"Kimlik doğrulama hatası: {ex.Message}"
            };
        }

        #endregion
    }
}
