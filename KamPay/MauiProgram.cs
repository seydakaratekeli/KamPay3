using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using FFImageLoading.Maui;
using KamPay.Services;
using KamPay.ViewModels;
using KamPay.Views;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Text.Json;
using KamPay.Resources.Languages;
using Firebase.Database; 
using Firebase.Auth; // ✅ YENİ EKLEME
using KamPay.Helpers;

namespace KamPay
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("⚙️ MauiApp başlatılıyor...");

                //  Türkçe karakter desteği için encoding provider'ı kaydet
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                System.Diagnostics.Debug.WriteLine("✓ Encoding provider kaydedildi");

                // ⚠️ ÖNEMLI: Culture ayarını daha minimalist yap
                // Sadece neutral culture kullan, satellite assembly yüklenmesini bekle
                try
                {
                    // Invariant culture ile başla, sonra LocalizationResourceManager ayarlayacak
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    System.Diagnostics.Debug.WriteLine("✓ Invariant culture ayarlandi (geçici)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Culture ayarlama hatası: {ex.Message}");
                }

                var builder = MauiApp.CreateBuilder();
                System.Diagnostics.Debug.WriteLine("✓ MauiApp builder oluştu");

                builder
                    .UseMauiApp<App>()
                    .UseSkiaSharp()
                    .UseBarcodeReader()
                    .UseMauiCommunityToolkit()
                    .UseFFImageLoading()
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                        fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                    });


                // ✅ E-posta Ayarları - appsettings.json'dan yükleniyor
                var emailSettings = LoadEmailSettings();
                System.Diagnostics.Debug.WriteLine($"✓ Email ayarları yüklendi: {emailSettings.SmtpHost}");

                // 🔥 Firebase Configuration - appsettings.json'dan yükleniyor
                var firebaseConfig = LoadFirebaseConfig();
                System.Diagnostics.Debug.WriteLine($"✓ Firebase config yüklendi: {firebaseConfig.ProjectId}");

                // 🧪 TEST: Email ayarlarını logla ve test modu uyarısı göster
                EmailTestHelper.LogEmailSettings(emailSettings);
                EmailTestHelper.ShowTestModeWarning(emailSettings);

                // Servislerin DI kaydı
                builder.Services.AddSingleton(emailSettings);
                builder.Services.AddSingleton(firebaseConfig);
                
                // ✅ YENİ: Firebase temel servislerini DI'ye kaydet
                builder.Services.AddSingleton<FirebaseClient>(sp =>
                {
                    var client = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
                    System.Diagnostics.Debug.WriteLine($"✅ FirebaseClient oluşturuldu: {Constants.FirebaseRealtimeDbUrl}");
                    return client;
                });

                builder.Services.AddSingleton<FirebaseAuthProvider>(sp =>
                {
                    var config = sp.GetRequiredService<FirebaseConfigSettings>();
                    var provider = new FirebaseAuthProvider(new FirebaseConfig(config.ApiKey));
                    System.Diagnostics.Debug.WriteLine($"✅ FirebaseAuthProvider oluşturuldu");
                    return provider;
                });

                // ✅ İYİLEŞTİRME 1: Localization Service DI'ye kaydet
                builder.Services.AddSingleton<ILocalizationService, LocalizationResourceManager>();
                System.Diagnostics.Debug.WriteLine("✅ ILocalizationService DI'ye kaydedildi");

                // ✅ İYİLEŞTİRME 2: RealtimeSnapshotService Generic Factory
                builder.Services.AddTransient(typeof(IRealtimeSnapshotService<>), typeof(RealtimeSnapshotService<>));
                System.Diagnostics.Debug.WriteLine("✅ IRealtimeSnapshotService<T> DI'ye kaydedildi");

                builder.Services.AddSingleton<IEmailService, EmailService>();

                // ✅ IUserProfileService'i önce kaydet
                builder.Services.AddSingleton<IUserProfileService, FirebaseUserProfileService>();
                
                // ✅ INotificationService'i kaydet (IMessagingService bağımlı)
                builder.Services.AddSingleton<INotificationService, FirebaseNotificationService>();

                // 🔥 Firebase Authentication Service (FirebaseAuthProvider DI'den geliyor)
                builder.Services.AddSingleton<IAuthenticationService, FirebaseAuthService>();

                // AppShell ve App
                builder.Services.AddSingleton<AppShell>();
                builder.Services.AddSingleton<App>();

                // Product ve Storage servisleri
                builder.Services.AddSingleton<IProductService, FirebaseProductService>();
                builder.Services.AddSingleton<IStorageService, FirebaseStorageService>();

                // IMessagingService (INotificationService'e bağımlı)
                builder.Services.AddSingleton<IMessagingService>(sp =>
                    new FirebaseMessagingService(sp.GetRequiredService<INotificationService>()));

                // IFavoriteService (INotificationService'e bağımlı)
                builder.Services.AddSingleton<IFavoriteService>(sp =>
                    new FirebaseFavoriteService(sp.GetRequiredService<INotificationService>()));
                
                // IQRCodeService (IUserProfileService ve IStorageService'e bağımlı)
                builder.Services.AddSingleton<IQRCodeService, FirebaseQRCodeService>();

                // Diğer servisler
                builder.Services.AddSingleton<IFirebaseObserverService, FirebaseObserverService>();
                builder.Services.AddSingleton<IProductCacheService, ProductCacheService>();
                builder.Services.AddSingleton<IReverseGeocodeService, ReverseGeocodeService>();
                
                // ISurpriseBoxService
                builder.Services.AddSingleton<ISurpriseBoxService>(sp =>
                    new FirebaseSurpriseBoxService(
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IProductService>(),
                        sp.GetRequiredService<INotificationService>()
                    )
                );
                
                // IGoodDeedService
                builder.Services.AddSingleton<IGoodDeedService, FirebaseGoodDeedService>();

                // IServiceSharingService (tüm bağımlılıkları hazır)
                builder.Services.AddSingleton<IServiceSharingService, FirebaseServiceSharingService>();

                // ITransactionService (tüm bağımlılıkları hazır)
                builder.Services.AddSingleton<ITransactionService>(sp =>
                    new FirebaseTransactionService(
                        sp.GetRequiredService<INotificationService>(),
                        sp.GetRequiredService<IProductService>(),
                        sp.GetRequiredService<IQRCodeService>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<FirebaseClient>()
                    )
                );
                // UserStateService - Singleton olarak global kullanıcı durumu yönetimi
                //  Tüm bağımlı servisler yukarıda kayıtlı olduğu için burada tanımlanıyor
                builder.Services.AddSingleton<IUserStateService>(sp =>
                    new UserStateService(
                        sp.GetRequiredService<IAuthenticationService>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IProductService>(),
                        sp.GetRequiredService<IServiceSharingService>(),
                        sp.GetRequiredService<IGoodDeedService>(),
                        sp.GetRequiredService<IMessagingService>())
                );

                // ViewModels
                builder.Services.AddSingleton<AppShellViewModel>(); // Singleton olarak ekliyoruz
                builder.Services.AddTransient<RegisterViewModel>();
                builder.Services.AddTransient<LoginViewModel>();
                builder.Services.AddTransient<MainViewModel>();
                builder.Services.AddTransient<NotificationsViewModel>();
                builder.Services.AddTransient<EditProductViewModel>();
                builder.Services.AddTransient<AddProductViewModel>();
                builder.Services.AddTransient<ProductListViewModel>();
                builder.Services.AddTransient<TradeOfferViewModel>();
                builder.Services.AddTransient<ProductDetailViewModel>();
                builder.Services.AddTransient<MessagesViewModel>();
                builder.Services.AddTransient<ChatViewModel>();
                builder.Services.AddTransient<FavoritesViewModel>();
                builder.Services.AddTransient<ProfileViewModel>();
                builder.Services.AddTransient<QRCodeViewModel>();
                builder.Services.AddTransient<SurpriseBoxViewModel>();
                builder.Services.AddTransient<GoodDeedBoardViewModel>();
                builder.Services.AddTransient<ServiceSharingViewModel>();
                builder.Services.AddTransient<ServiceRequestsViewModel>();
                builder.Services.AddTransient<ImageViewerViewModel>();
                builder.Services.AddTransient<PaymentViewModel>();
                builder.Services.AddTransient<EditProfileViewModel>();
                builder.Services.AddTransient<OffersViewModel>();

                // 🎯 ARMUT MODELİ: Yeni ViewModels
                builder.Services.AddTransient<CreateCustomerRequestViewModel>();
                builder.Services.AddTransient<CustomerRequestsListViewModel>();
                builder.Services.AddTransient<CustomerRequestDetailsViewModel>();
                
                // ✅ Pages - Tüm parametreli constructor'a sahip sayfalar
                builder.Services.AddTransient<LoginPage>();
                builder.Services.AddTransient<RegisterPage>();
                builder.Services.AddTransient<MainPage>();
                builder.Services.AddTransient<NotificationsPage>();
                builder.Services.AddTransient<AddProductPage>();
                builder.Services.AddTransient<EditProductPage>();
                builder.Services.AddTransient<ProductListPage>();
                builder.Services.AddTransient<ProductDetailPage>();
                builder.Services.AddTransient<ChatPage>();
                builder.Services.AddTransient<MessagesPage>();
                builder.Services.AddTransient<FavoritesPage>();
                builder.Services.AddTransient<ProfilePage>();
                builder.Services.AddTransient<EditProfilePage>();
                builder.Services.AddTransient<GoodDeedBoardPage>();
                builder.Services.AddTransient<ImageViewerPage>();
                builder.Services.AddTransient<OffersPage>();
                builder.Services.AddTransient<PaymentPage>();
                builder.Services.AddTransient<QRCodeDisplayPage>();
                builder.Services.AddTransient<QRScannerPage>();
                builder.Services.AddTransient<ServiceRequestsPage>();
                builder.Services.AddTransient<ServiceSharingPage>();
                builder.Services.AddTransient<SurpriseBoxPage>();
                builder.Services.AddTransient<TradeOfferView>();

                // 🎯 ARMUT MODELİ: Yeni Pages
                builder.Services.AddTransient<CreateCustomerRequestPage>();
                builder.Services.AddTransient<CustomerRequestsListPage>();
                builder.Services.AddTransient<CustomerRequestDetailsPage>();

                //  Singleton yaptık: Sayfa ve ViewModel bir kere oluşturulur ve hafızada kalır.
                builder.Services.AddSingleton<ICategoryService, FirebaseCategoryService>();

#if DEBUG
                builder.Logging.AddDebug();
#endif
                LocalizationResourceManager.EnsureInitialized();
                System.Diagnostics.Debug.WriteLine("⚙️ MauiApp build ediliyor...");
                var app = builder.Build();
                System.Diagnostics.Debug.WriteLine("✓ MauiApp başarıyla oluşturuldu");

                return app;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ KRITIK: MauiProgram.CreateMauiApp hatası: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Kritik hatalar yeniden fırlatılmalı
            }
        }

        /// <summary>
        /// appsettings.json dosyasından EmailSettings yükler
        /// </summary>
        private static EmailSettings LoadEmailSettings()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "KamPay.appsettings.json";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ {resourceName} bulunamadı, varsayılan ayarlar kullanılıyor");
                    return GetDefaultEmailSettings();
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                
                var config = JsonSerializer.Deserialize<AppConfig>(json, options);
                
                if (config?.EmailSettings == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ EmailSettings null, varsayılan ayarlar kullanılıyor");
                    return GetDefaultEmailSettings();
                }

                System.Diagnostics.Debug.WriteLine($"✅ appsettings.json'dan email ayarları yüklendi");
                return config.EmailSettings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ appsettings.json okunamadı: {ex.Message}");
                return GetDefaultEmailSettings();
            }
        }

        /// <summary>
        /// appsettings.json'dan Firebase Config yükler
        /// </summary>
        private static FirebaseConfigSettings LoadFirebaseConfig()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "KamPay.appsettings.json";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Firebase config bulunamadı");
                    return GetDefaultFirebaseConfig();
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                
                var config = JsonSerializer.Deserialize<AppConfig>(json, options);
                
                if (config?.FirebaseConfig == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ FirebaseConfig null, varsayılan ayarlar kullanılıyor");
                    return GetDefaultFirebaseConfig();
                }

                System.Diagnostics.Debug.WriteLine($"✅ Firebase config yüklendi");
                return config.FirebaseConfig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Firebase config okunamadı: {ex.Message}");
                return GetDefaultFirebaseConfig();
            }
        }

        /// <summary>
        /// Varsayılan email ayarları (appsettings.json okunamazsa)
        /// </summary>
        private static EmailSettings GetDefaultEmailSettings()
        {
            System.Diagnostics.Debug.WriteLine("⚠️ Varsayılan SMTP ayarları kullanılıyor (PLACEHOLDER)");
            return new EmailSettings
            {
                SmtpHost = "smtp.bartin.edu.tr",
                SmtpPort = 587,
                UseSsl = true,
                FromEmail = "kampay@bartin.edu.tr",
                FromName = "KamPay Doğrulama",
                Username = "kampay@bartin.edu.tr",
                Password = "SMTP_PAROLASI_BURAYA" // ⚠️ appsettings.json'da gerçek şifre olmalı
            };
        }

        /// <summary>
        /// Varsayılan Firebase config
        /// </summary>
        private static FirebaseConfigSettings GetDefaultFirebaseConfig()
        {
            System.Diagnostics.Debug.WriteLine("⚠️ Varsayılan Firebase config kullanılıyor");
            return new FirebaseConfigSettings
            {
                ApiKey = "YOUR_API_KEY_HERE",
                AuthDomain = "kampay-b006d.firebaseapp.com",
                DatabaseURL = "https://kampay-b006d-default-rtdb.europe-west1.firebasedatabase.app",
                ProjectId = "kampay-b006d",
                StorageBucket = "kampay-b006d.appspot.com"
            };
        }

        /// <summary>
        /// appsettings.json deserializasyon için model
        /// </summary>
        private class AppConfig
        {
            public EmailSettings? EmailSettings { get; set; }
            public FirebaseConfigSettings? FirebaseConfig { get; set; }
        }
    }

    /// <summary>
    /// Firebase Configuration Settings Model
    /// </summary>
    public class FirebaseConfigSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string AuthDomain { get; set; } = string.Empty;
        public string DatabaseURL { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string StorageBucket { get; set; } = string.Empty;
    }
}
