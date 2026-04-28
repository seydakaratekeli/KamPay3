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
using Syncfusion.Maui.Core.Hosting;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Text.Json;
using KamPay.Resources.Languages;
using Firebase.Database; 
using Firebase.Auth; // ✅ YENİ EKLEME
using KamPay.Helpers;
using KamPay.Models.Configuration;
using KamPay.Security;
using KamPay.Services.Payment; // ✅ EKLEME: Payment namespace
using KamPay.Services.ServiceSharing;
using KamPay.Services.Auth;
using KamPay.Services.Products;
using KamPay.Services.Messaging;
using KamPay.Services.QRCode;
using KamPay.Services.Products.Coordinators;
using KamPay.Services.Transactions; // ✅ FAZ 3.2


namespace KamPay
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog("⚙️ MauiApp başlatılıyor...");

                //  Türkçe karakter desteği için encoding provider'ı kaydet
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                KamPay.Helpers.AppLogger.DebugLog("✓ Encoding provider kaydedildi");

                // ⚠️ ÖNEMLI: Culture ayarını daha minimalist yap
                // Sadece neutral culture kullan, satellite assembly yüklenmesini bekle
                try
                {
                    // Invariant culture ile başla, sonra LocalizationResourceManager ayarlayacak
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    KamPay.Helpers.AppLogger.DebugLog("✓ Invariant culture ayarlandi (geçici)");
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ Culture ayarlama hatası: {ex.Message}");
                }

                var builder = MauiApp.CreateBuilder();
                KamPay.Helpers.AppLogger.DebugLog("✓ MauiApp builder oluştu");

                builder
                    .UseMauiApp<App>()
                    .ConfigureSyncfusionCore()
                    .UseSkiaSharp()
                    .UseBarcodeReader()
                    .UseMauiCommunityToolkit()
                    .UseFFImageLoading()
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                        fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                    })
                    // ✅ CUSTOM HANDLERS: Native performance optimizations
                    .ConfigureMauiHandlers(handlers =>
                    {
#if ANDROID
                        KamPay.Helpers.AppLogger.DebugLog("🚀 Android Custom Handlers kaydediliyor...");

                        // 1️⃣ Glide ile optimize edilmiş görsel yükleme
                        // handlers.AddHandler<Image, KamPay.Handlers.OptimizedImageHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  ✓ OptimizedImageHandler (Glide) kaydedildi");

                        // 2️⃣ RecyclerView ile optimize edilmiş liste/koleksiyon
                        // ⚠️ ŞU AN KAPALI: Derleme hatası nedeniyle (type constraint sorunu)
                        // TODO: .NET MAUI 8 CollectionViewHandler implementation'ını kontrol et
                        // handlers.AddHandler<CollectionView, KamPay.Handlers.OptimizedCollectionViewHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  ✓ OptimizedCollectionViewHandler (RecyclerView) kaydedildi");
                        
                        // 3️⃣ Camera2 API ile hızlı QR tarama (opsiyonel - ZXing.Net.Maui yerine)
                        // ⚠️ DİKKAT: Şu an kapalı (ZXing.Net.Maui zaten yeterince hızlı)
                        // handlers.AddHandler<ZXing.Net.Maui.Controls.CameraBarcodeReaderView, KamPay.Handlers.FastQRScannerHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  ✓ FastQRScannerHandler (Camera2) kaydedildi");
                        
#elif IOS || MACCATALYST
                        KamPay.Helpers.AppLogger.DebugLog("🚀 iOS Custom Handlers kaydediliyor...");
                        
                        // 1️⃣ SDWebImage ile optimize edilmiş görsel yükleme
                        handlers.AddHandler<Image, KamPay.Handlers.OptimizedImageHandler>();
                        KamPay.Helpers.AppLogger.DebugLog("  ✓ OptimizedImageHandler (SDWebImage) kaydedildi");
#endif
                    });


                // ✅ appsettings.json'dan ayarları yükle
                var appConfig = LoadAppConfig();

                var apiSettings = appConfig.ApiSettings ?? new ApiSettings();
                KamPay.Helpers.AppLogger.DebugLog($"✅ ApiSettings yüklendi: {apiSettings.LocalApiBaseUrl}");

                var emailSettings = appConfig.EmailSettings;
                if (emailSettings == null || string.IsNullOrEmpty(emailSettings.SmtpHost))
                    throw new InvalidOperationException("Email settings could not be loaded from appsettings.json");

                KamPay.Helpers.AppLogger.DebugLog($"✓ Email ayarları yüklendi: {emailSettings.SmtpHost}");

                var firebaseConfig = appConfig.FirebaseConfig;
                if (firebaseConfig == null || string.IsNullOrEmpty(firebaseConfig.ApiKey))
                    throw new InvalidOperationException("Firebase config could not be loaded from appsettings.json");

                KamPay.Helpers.AppLogger.DebugLog($"✓ Firebase config yüklendi: {firebaseConfig.ProjectId}");

                // 🧪 TEST: Email ayarlarını logla ve test modu uyarısı göster
                EmailTestHelper.LogEmailSettings(emailSettings);
                EmailTestHelper.ShowTestModeWarning(emailSettings);

                // Servislerin DI kaydı
                builder.Services.AddSingleton<KamPay.Services.Configuration.IConfigurationService>(
                    new KamPay.Services.Configuration.ConfigurationService(emailSettings, firebaseConfig)
                );

                builder.Services.AddSingleton(emailSettings);
                builder.Services.AddSingleton(firebaseConfig);
                builder.Services.AddSingleton(apiSettings);

                // ✅ YENİ: Firebase temel servislerini DI'ye kaydet
                builder.Services.AddSingleton<FirebaseClient>(sp =>
                {
                    var config = sp.GetRequiredService<FirebaseConfigSettings>();
                    var client = new FirebaseClient(config.DatabaseURL);
                    KamPay.Helpers.AppLogger.DebugLog($"✅ FirebaseClient oluşturuldu: {config.DatabaseURL}");
                    return client;
                });

                builder.Services.AddSingleton<FirebaseAuthProvider>(sp =>
                {
                    var config = sp.GetRequiredService<FirebaseConfigSettings>();
                    var provider = new FirebaseAuthProvider(new FirebaseConfig(config.ApiKey));
                    KamPay.Helpers.AppLogger.DebugLog($"✅ FirebaseAuthProvider oluşturuldu");
                    return provider;
                });

                // ✅ İYİLEŞTİRME 1: Localization Service DI'ye kaydet
                builder.Services.AddSingleton<ILocalizationService, LocalizationResourceManager>();
                KamPay.Helpers.AppLogger.DebugLog("✅ ILocalizationService DI'ye kaydedildi");

                // ✅ İYİLEŞTİRME 2: RealtimeSnapshotService Generic Factory
                builder.Services.AddTransient(typeof(IRealtimeSnapshotService<>), typeof(RealtimeSnapshotService<>));
                KamPay.Helpers.AppLogger.DebugLog("✅ IRealtimeSnapshotService<T> DI'ye kaydedildi");


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
                builder.Services.AddSingleton<IProductImageCoordinator, ProductImageCoordinator>(); // ✅ Görsel koordinatörü
                builder.Services.AddSingleton<IProductCreationCoordinator, ProductCreationCoordinator>(); // ✅ YENİ: Ürün oluşturma koordinatörü

                // ✅ YENİ API BAĞLANTISI (Garson) - Artık doğrudan Firebase ile değil, kendi API'miz ile haberleşiyoruz
                // ⚠️ Development: Self-signed SSL sertifikası bypass (Android emülatör + localhost için)
                builder.Services.AddSingleton<HttpClient>(sp =>
                {
#if DEBUG
                    var handler = new HttpClientHandler
                    {
                        // Development ortamında localhost'un self-signed sertifikasını kabul et
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    var client = new HttpClient(handler);
                    KamPay.Helpers.AppLogger.DebugLog("⚠️ HttpClient: SSL sertifika doğrulaması KAPALI (Development)");
#else
                    var client = new HttpClient();
                    KamPay.Helpers.AppLogger.DebugLog("🔒 HttpClient: SSL sertifika doğrulaması AKTİF (Production)");
#endif
                    client.Timeout = TimeSpan.FromSeconds(30);
                    return client;
                });

builder.Services.AddSingleton<IProductService, Services.Products.ProductApiService>();

                // Eski servis (Yorum Satırında)
                // builder.Services.AddSingleton<IProductService, FirebaseProductService>();

                builder.Services.AddSingleton<IStorageService, FirebaseStorageService>();

                
                // IMessagingService (INotificationService ve FirebaseClient'a bağımlı)
                builder.Services.AddSingleton<IMessagingService>(sp =>
                    new FirebaseMessagingService(
                        sp.GetRequiredService<FirebaseClient>(), // ✅ FIX: FirebaseClient eklendi
                        sp.GetRequiredService<INotificationService>()));

                // ✅ YENİ: Mesaj medya koordinatörü
                builder.Services.AddSingleton<IMessageMediaCoordinator, MessageMediaCoordinator>();

                // IFavoriteService (FirebaseClient ve INotificationService'e bağımlı)
                builder.Services.AddSingleton<IFavoriteService>(sp =>
                    new FirebaseFavoriteService(
                        sp.GetRequiredService<FirebaseClient>(),
                        sp.GetRequiredService<INotificationService>()));

                // IQRCodeService (IUserProfileService, IStorageService ve TransactionCompletionHelper'a bağımlı)
                builder.Services.AddSingleton<IQRCodeService>(sp => 
                    new FirebaseQRCodeService(
                        sp.GetRequiredService<FirebaseClient>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IStorageService>(),
                        sp.GetRequiredService<KamPay.Services.Shared.TransactionCompletionHelper>()
                    )
                );

                // ✅ FAZ2: Paylaşılan transaction tamamlama yardımcısı
                builder.Services.AddSingleton<KamPay.Services.Shared.TransactionCompletionHelper>();

                // Diğer servisler
                builder.Services.AddSingleton<IFirebaseObserverService, FirebaseObserverService>();
                builder.Services.AddSingleton<IProductCacheService, ProductCacheService>();
                builder.Services.AddSingleton<IReverseGeocodeService, ReverseGeocodeService>();
                
                // ✅ FAZ 7: Offline Caching Service
                builder.Services.AddTransient(typeof(KamPay.Services.Caching.ILocalDatabaseService<>), typeof(KamPay.Services.Caching.LocalDatabaseService<>));
                
                // ISurpriseBoxService
                builder.Services.AddSingleton<ISurpriseBoxService>(sp =>
                    new FirebaseSurpriseBoxService(
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IProductService>(),
                        sp.GetRequiredService<INotificationService>(),
                        sp.GetRequiredService<FirebaseClient>()
                    )
                );
                
                // IGoodDeedService
                builder.Services.AddSingleton<IGoodDeedService, FirebaseGoodDeedService>();

                // ? FAZ 3.2: ServiceSharing Services Par�alama
                builder.Services.AddSingleton<ServiceOfferService>();
                builder.Services.AddSingleton<ICustomerRequestManager, CustomerRequestManager>();
                builder.Services.AddSingleton<IProviderProposalManager, ProviderProposalManager>();
                builder.Services.AddSingleton<ServiceRequestCrudService>();
                builder.Services.AddSingleton<ServiceRequestNegotiationService>();
                builder.Services.AddSingleton<ServiceRequestCompletionService>();
                builder.Services.AddSingleton<IServiceSharingService, ServiceSharingFacade>();
            builder.Services.AddSingleton<IServiceReviewService, ServiceReviewService>();

                // ? FAZ 3: Transaction Services (Temizlenmi� - Tek Sorumluluk)
                // S�ra �nemli: CompletionService � PaymentService + NegotiationService � CrudService
                builder.Services.AddSingleton<TransactionCompletionService>();
                builder.Services.AddSingleton<TransactionCrudService>();
                builder.Services.AddSingleton<TransactionNegotiationService>(sp =>
                    new TransactionNegotiationService(
                        sp.GetRequiredService<Firebase.Database.FirebaseClient>(),
                        sp.GetRequiredService<INotificationService>(),
                        sp.GetRequiredService<TransactionCrudService>(),
                        sp.GetRequiredService<IProductService>() // ✅ 4. Parametre (IProductService) EKLENDİ

                    ));
                builder.Services.AddSingleton<TransactionPaymentService>(sp =>
                    new TransactionPaymentService(
                        sp.GetRequiredService<Firebase.Database.FirebaseClient>(),
                        sp.GetRequiredService<INotificationService>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<KamPay.Services.Payment.IPaymentProviderFactory>(),
                        sp.GetRequiredService<TransactionCompletionService>()
                    ));
                
                builder.Services.AddSingleton<ITransactionService, KamPay.Services.Transactions.TransactionFacade>();

                // ✅ YENİ: ÖDEME SİSTEMİ - OCP PRENSİBİ
                KamPay.Helpers.AppLogger.DebugLog("✅ Ödeme sistemi kaydediliyor (OCP Pattern)...");
                
                // Provider'ları DI'ye kaydet (IEnumerable<IPaymentProvider> olarak inject edilecek)
                builder.Services.AddSingleton<IPaymentProvider, CardSimulationProvider>();
                builder.Services.AddSingleton<IPaymentProvider, BankTransferSimulationProvider>();
                
                // Factory'yi kaydet (Constructor'da IEnumerable<IPaymentProvider> alacak)
                builder.Services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();
                
                KamPay.Helpers.AppLogger.DebugLog("  ✓ IPaymentProviderFactory kaydedildi");

                // ✅ KOORDINATÖRLER - Orkestrasyon Servisleri (Bağımlılıklardan SONRA kaydedilmeli)
                KamPay.Helpers.AppLogger.DebugLog("✅ Koordinatörler kaydediliyor...");
                
                // CacheCoordinator - IProductCacheService'e bağımlı
                builder.Services.AddSingleton<ICacheCoordinator, CacheCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  ✓ ICacheCoordinator kaydedildi");
                
                // ValidationCoordinator - IProductService'e bağımlı
                builder.Services.AddSingleton<IValidationCoordinator, ValidationCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  ✓ IValidationCoordinator kaydedildi");
                
                // NotificationCoordinator - INotificationService'e bağımlı
                builder.Services.AddSingleton<INotificationCoordinator, NotificationCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  ✓ INotificationCoordinator kaydedildi");
                
                // TransactionOrchestrator - ITransactionService, INotificationService, IProductService, IUserProfileService'e bağımlı
                builder.Services.AddSingleton<ITransactionOrchestrator, TransactionOrchestrator>();
                KamPay.Helpers.AppLogger.DebugLog("  ✓ ITransactionOrchestrator kaydedildi");

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

                // Security Audit Service kaydı
                builder.Services.AddSingleton<ISecurityAuditService, FirebaseSecurityAuditService>(); 

                // ViewModels
                builder.Services.AddSingleton<AppShellViewModel>(); // Singleton olarak ekliyoruz
                builder.Services.AddTransient<RegisterViewModel>();
                builder.Services.AddTransient<LoginViewModel>();
                builder.Services.AddTransient<MainViewModel>();
                builder.Services.AddTransient<NotificationsViewModel>();
                builder.Services.AddTransient<EditProductViewModel>();
                builder.Services.AddTransient<AddProductViewModel>();
                builder.Services.AddSingleton<ProductListViewModel>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<TradeOfferViewModel>();
                builder.Services.AddTransient<ProductDetailViewModel>();
                builder.Services.AddSingleton<MessagesViewModel>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<ChatViewModel>();
                builder.Services.AddTransient<FavoritesViewModel>();
                builder.Services.AddSingleton<ProfileViewModel>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<QRCodeViewModel>();
                builder.Services.AddTransient<SurpriseBoxViewModel>();
                builder.Services.AddSingleton<GoodDeedBoardViewModel>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddSingleton<GoodDeedPostDetailViewModel>();
                builder.Services.AddTransient<EditGoodDeedPostViewModel>();
                builder.Services.AddSingleton<ServiceSharingViewModel>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<ServiceOfferDetailViewModel>();
                builder.Services.AddTransient<EditServiceOfferViewModel>();
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
                builder.Services.AddSingleton<ProductListPage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<ProductDetailPage>();
                builder.Services.AddTransient<ChatPage>();
                builder.Services.AddSingleton<MessagesPage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddSingleton<NegotiationMessagesPage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<FavoritesPage>();
                builder.Services.AddSingleton<ProfilePage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<EditProfilePage>();
                builder.Services.AddSingleton<GoodDeedBoardPage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<GoodDeedPostDetailPage>();
                builder.Services.AddTransient<EditGoodDeedPostPage>();
                builder.Services.AddTransient<ImageViewerPage>();
                builder.Services.AddTransient<OffersPage>();
                builder.Services.AddTransient<PaymentPage>();
                builder.Services.AddTransient<QRCodeDisplayPage>();
                builder.Services.AddTransient<QRScannerPage>();
                builder.Services.AddTransient<ServiceRequestsPage>();
                builder.Services.AddSingleton<ServiceSharingPage>(); // Tab sayfası - Singleton olmalı
                builder.Services.AddTransient<ServiceOfferDetailPage>();
                builder.Services.AddTransient<EditServiceOfferPage>();
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
                KamPay.Helpers.AppLogger.DebugLog("⚙️ MauiApp build ediliyor...");
                var app = builder.Build();
                KamPay.Helpers.AppLogger.DebugLog("✓ MauiApp başarılıyla oluşturuldu");

                return app;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ KRITIK: MauiProgram.CreateMauiApp hatası: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Kritik hatalar yeniden fırlatılmalı
            }
        }

        /// <summary>
        /// appsettings.json'dan ayarları yükler
        /// </summary>
        private static AppConfig LoadAppConfig()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // First try to load Development config
                var devResourceName = "KamPay.appsettings.Development.json";
                using var devStream = assembly.GetManifestResourceStream(devResourceName);

                Stream targetStream = devStream;
                if (targetStream == null)
                {
                    // Fallback to prod config
                    var resourceName = "KamPay.appsettings.json";
                    targetStream = assembly.GetManifestResourceStream(resourceName);
                }

                if (targetStream == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ appsettings file not found in embedded resources");
                    return new AppConfig();
                }

                using var reader = new StreamReader(targetStream);
                var json = reader.ReadToEnd();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ appsettings.json okunamadı: {ex.Message}");
                return new AppConfig();
            }
        }

        /// <summary>
        /// appsettings.json deserializasyon için model
        /// </summary>
        private class AppConfig
        {
            public ApiSettings? ApiSettings { get; set; }
            public EmailSettings? EmailSettings { get; set; }
            public FirebaseConfigSettings? FirebaseConfig { get; set; }
        }
    }

    /// <summary>
    /// API Configuration Settings Model
    /// </summary>
    public class ApiSettings
    {
        public string RealDeviceApiUrl { get; set; } = string.Empty;
        public string EmulatorApiUrl { get; set; } = string.Empty;
        public string LocalhostApiUrl { get; set; } = string.Empty;

        public string LocalApiBaseUrl
        {
            get
            {
#if ANDROID
                return RealDeviceApiUrl;
#elif IOS
                return RealDeviceApiUrl;
#else
                return LocalhostApiUrl;
#endif
            }
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

