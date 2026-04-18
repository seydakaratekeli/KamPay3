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
using Firebase.Auth; // âœ… YENÄ° EKLEME
using KamPay.Helpers;
using KamPay.Security;
using KamPay.Services.Payment; // âœ… EKLEME: Payment namespace
using KamPay.Services.ServiceSharing; // âœ… FAZ 3.2


namespace KamPay
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog("âš™ï¸ MauiApp baÅŸlatÄ±lÄ±yor...");

                //  TÃ¼rkÃ§e karakter desteÄŸi iÃ§in encoding provider'Ä± kaydet
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                KamPay.Helpers.AppLogger.DebugLog("âœ“ Encoding provider kaydedildi");

                // âš ï¸ Ã–NEMLI: Culture ayarÄ±nÄ± daha minimalist yap
                // Sadece neutral culture kullan, satellite assembly yÃ¼klenmesini bekle
                try
                {
                    // Invariant culture ile baÅŸla, sonra LocalizationResourceManager ayarlayacak
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                    KamPay.Helpers.AppLogger.DebugLog("âœ“ Invariant culture ayarlandi (geÃ§ici)");
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Culture ayarlama hatasÄ±: {ex.Message}");
                }

                var builder = MauiApp.CreateBuilder();
                KamPay.Helpers.AppLogger.DebugLog("âœ“ MauiApp builder oluÅŸtu");

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
                    })
                    // âœ… CUSTOM HANDLERS: Native performance optimizations
                    .ConfigureMauiHandlers(handlers =>
                    {
#if ANDROID
                        KamPay.Helpers.AppLogger.DebugLog("ğŸš€ Android Custom Handlers kaydediliyor...");

                        // 1ï¸âƒ£ Glide ile optimize edilmiÅŸ gÃ¶rsel yÃ¼kleme
                        // handlers.AddHandler<Image, KamPay.Handlers.OptimizedImageHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  âœ“ OptimizedImageHandler (Glide) kaydedildi");

                        // 2ï¸âƒ£ RecyclerView ile optimize edilmiÅŸ liste/koleksiyon
                        // âš ï¸ ÅU AN KAPALI: Derleme hatasÄ± nedeniyle (type constraint sorunu)
                        // TODO: .NET MAUI 8 CollectionViewHandler implementation'Ä±nÄ± kontrol et
                        // handlers.AddHandler<CollectionView, KamPay.Handlers.OptimizedCollectionViewHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  âœ“ OptimizedCollectionViewHandler (RecyclerView) kaydedildi");
                        
                        // 3ï¸âƒ£ Camera2 API ile hÄ±zlÄ± QR tarama (opsiyonel - ZXing.Net.Maui yerine)
                        // âš ï¸ DÄ°KKAT: Åu an kapalÄ± (ZXing.Net.Maui zaten yeterince hÄ±zlÄ±)
                        // handlers.AddHandler<ZXing.Net.Maui.Controls.CameraBarcodeReaderView, KamPay.Handlers.FastQRScannerHandler>();
                        // KamPay.Helpers.AppLogger.DebugLog("  âœ“ FastQRScannerHandler (Camera2) kaydedildi");
                        
#elif IOS || MACCATALYST
                        KamPay.Helpers.AppLogger.DebugLog("ğŸš€ iOS Custom Handlers kaydediliyor...");
                        
                        // 1ï¸âƒ£ SDWebImage ile optimize edilmiÅŸ gÃ¶rsel yÃ¼kleme
                        handlers.AddHandler<Image, KamPay.Handlers.OptimizedImageHandler>();
                        KamPay.Helpers.AppLogger.DebugLog("  âœ“ OptimizedImageHandler (SDWebImage) kaydedildi");
#endif
                    });


                // âœ… appsettings.json'dan ayarlarÄ± yÃ¼kle
                var appConfig = LoadAppConfig();
                var emailSettings = appConfig.EmailSettings ?? GetDefaultEmailSettings();
                KamPay.Helpers.AppLogger.DebugLog($"âœ“ Email ayarlarÄ± yÃ¼klendi: {emailSettings.SmtpHost}");

                var firebaseConfig = appConfig.FirebaseConfig ?? GetDefaultFirebaseConfig();
                KamPay.Helpers.AppLogger.DebugLog($"âœ“ Firebase config yÃ¼klendi: {firebaseConfig.ProjectId}");

                // ğŸ§ª TEST: Email ayarlarÄ±nÄ± logla ve test modu uyarÄ±sÄ± gÃ¶ster
                EmailTestHelper.LogEmailSettings(emailSettings);
                EmailTestHelper.ShowTestModeWarning(emailSettings);

                // Servislerin DI kaydÄ±
                builder.Services.AddSingleton(emailSettings);
                builder.Services.AddSingleton(firebaseConfig);
                
                // âœ… YENÄ°: Firebase temel servislerini DI'ye kaydet
                builder.Services.AddSingleton<FirebaseClient>(sp =>
                {
                    var client = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… FirebaseClient oluÅŸturuldu: {Constants.FirebaseRealtimeDbUrl}");
                    return client;
                });

                builder.Services.AddSingleton<FirebaseAuthProvider>(sp =>
                {
                    var config = sp.GetRequiredService<FirebaseConfigSettings>();
                    var provider = new FirebaseAuthProvider(new FirebaseConfig(config.ApiKey));
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… FirebaseAuthProvider oluÅŸturuldu");
                    return provider;
                });

                // âœ… Ä°YÄ°LEÅTÄ°RME 1: Localization Service DI'ye kaydet
                builder.Services.AddSingleton<ILocalizationService, LocalizationResourceManager>();
                KamPay.Helpers.AppLogger.DebugLog("âœ… ILocalizationService DI'ye kaydedildi");

                // âœ… Ä°YÄ°LEÅTÄ°RME 2: RealtimeSnapshotService Generic Factory
                builder.Services.AddTransient(typeof(IRealtimeSnapshotService<>), typeof(RealtimeSnapshotService<>));
                KamPay.Helpers.AppLogger.DebugLog("âœ… IRealtimeSnapshotService<T> DI'ye kaydedildi");

                builder.Services.AddSingleton<IEmailService, EmailService>();

                // âœ… IUserProfileService'i Ã¶nce kaydet
                builder.Services.AddSingleton<IUserProfileService, FirebaseUserProfileService>();
                
                // âœ… INotificationService'i kaydet (IMessagingService baÄŸÄ±mlÄ±)
                builder.Services.AddSingleton<INotificationService, FirebaseNotificationService>();

                // ğŸ”¥ Firebase Authentication Service (FirebaseAuthProvider DI'den geliyor)
                builder.Services.AddSingleton<IAuthenticationService, FirebaseAuthService>();

                // AppShell ve App
                builder.Services.AddSingleton<AppShell>();
                builder.Services.AddSingleton<App>();

                // Product ve Storage servisleri
                builder.Services.AddSingleton<IProductImageCoordinator, ProductImageCoordinator>(); // âœ… GÃ¶rsel koordinatÃ¶rÃ¼
                builder.Services.AddSingleton<IProductCreationCoordinator, ProductCreationCoordinator>(); // âœ… YENÄ°: ÃœrÃ¼n oluÅŸturma koordinatÃ¶rÃ¼

                // âœ… YENÄ° API BAÄLANTISI (Garson) - ArtÄ±k doÄŸrudan Firebase ile deÄŸil, kendi API'miz ile haberleÅŸiyoruz
                // âš ï¸ Development: Self-signed SSL sertifikasÄ± bypass (Android emÃ¼latÃ¶r + localhost iÃ§in)
                builder.Services.AddSingleton<HttpClient>(sp =>
                {
#if DEBUG
                    var handler = new HttpClientHandler
                    {
                        // Development ortamÄ±nda localhost'un self-signed sertifikasÄ±nÄ± kabul et
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    var client = new HttpClient(handler);
                    KamPay.Helpers.AppLogger.DebugLog("âš ï¸ HttpClient: SSL sertifika doÄŸrulamasÄ± KAPALI (Development)");
#else
                    var client = new HttpClient();
                    KamPay.Helpers.AppLogger.DebugLog("ğŸ”’ HttpClient: SSL sertifika doÄŸrulamasÄ± AKTÄ°F (Production)");
#endif
                    client.Timeout = TimeSpan.FromSeconds(30);
                    return client;
                });
                builder.Services.AddSingleton<IProductService, KamPay.Services.ProductApiService>();

                // Eski servis (Yorum SatÄ±rÄ±nda)
                // builder.Services.AddSingleton<IProductService, FirebaseProductService>();

                builder.Services.AddSingleton<IStorageService, FirebaseStorageService>();

                // âœ… YENÄ°: ARMUT MODELÄ° - MÃ¼ÅŸteri ve Profesyonel YÃ¶netimi
                builder.Services.AddSingleton<ICustomerRequestManager, CustomerRequestManager>();
                builder.Services.AddSingleton<IProviderProposalManager, ProviderProposalManager>();
                
                // IMessagingService (INotificationService ve FirebaseClient'a baÄŸÄ±mlÄ±)
                builder.Services.AddSingleton<IMessagingService>(sp =>
                    new FirebaseMessagingService(
                        sp.GetRequiredService<FirebaseClient>(), // âœ… FIX: FirebaseClient eklendi
                        sp.GetRequiredService<INotificationService>()));

                // âœ… YENÄ°: Mesaj medya koordinatÃ¶rÃ¼
                builder.Services.AddSingleton<IMessageMediaCoordinator, MessageMediaCoordinator>();

                // IFavoriteService (INotificationService'e baÄŸÄ±mlÄ±)
                builder.Services.AddSingleton<IFavoriteService>(sp =>
                    new FirebaseFavoriteService(sp.GetRequiredService<INotificationService>()));
                
                // IQRCodeService (IUserProfileService, IStorageService ve TransactionCompletionHelper'a baÄŸÄ±mlÄ±)
                builder.Services.AddSingleton<IQRCodeService>(sp => 
                    new FirebaseQRCodeService(
                        sp.GetRequiredService<FirebaseClient>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IStorageService>(),
                        sp.GetRequiredService<KamPay.Services.Shared.TransactionCompletionHelper>()
                    )
                );

                // âœ… FAZ2: PaylaÅŸÄ±lan transaction tamamlama yardÄ±mcÄ±sÄ±
                builder.Services.AddSingleton<KamPay.Services.Shared.TransactionCompletionHelper>();

                // DiÄŸer servisler
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

                // âœ… FAZ 3.2: ServiceSharing Services ParÃ§alama
                builder.Services.AddSingleton<ServiceOfferService>();
                builder.Services.AddSingleton<ServiceRequestService>();
                builder.Services.AddSingleton<IServiceSharingService, ServiceSharingFacade>();

                // âœ… FAZ 3: Transaction Services ParÃ§alama
                builder.Services.AddSingleton<TransactionCrudService>();
                builder.Services.AddSingleton<TransactionPaymentService>();
                builder.Services.AddSingleton<TransactionNegotiationService>();
                builder.Services.AddSingleton<TransactionCompletionService>();
                
                builder.Services.AddSingleton<ITransactionService, KamPay.Services.Transactions.TransactionFacade>();

                // âœ… YENÄ°: Ã–DEME SÄ°STEMÄ° - OCP PRENSÄ°BÄ°
                KamPay.Helpers.AppLogger.DebugLog("âœ… Ã–deme sistemi kaydediliyor (OCP Pattern)...");
                
                // Provider'larÄ± DI'ye kaydet (IEnumerable<IPaymentProvider> olarak inject edilecek)
                builder.Services.AddSingleton<IPaymentProvider, CardSimulationProvider>();
                builder.Services.AddSingleton<IPaymentProvider, BankTransferSimulationProvider>();
                
                // Factory'yi kaydet (Constructor'da IEnumerable<IPaymentProvider> alacak)
                builder.Services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();
                
                KamPay.Helpers.AppLogger.DebugLog("  âœ“ IPaymentProviderFactory kaydedildi");

                // âœ… KOORDINATÃ–RLER - Orkestrasyon Servisleri (BaÄŸÄ±mlÄ±lÄ±klardan SONRA kaydedilmeli)
                KamPay.Helpers.AppLogger.DebugLog("âœ… KoordinatÃ¶rler kaydediliyor...");
                
                // CacheCoordinator - IProductCacheService'e baÄŸÄ±mlÄ±
                builder.Services.AddSingleton<ICacheCoordinator, CacheCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  âœ“ ICacheCoordinator kaydedildi");
                
                // ValidationCoordinator - IProductService'e baÄŸÄ±mlÄ±
                builder.Services.AddSingleton<IValidationCoordinator, ValidationCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  âœ“ IValidationCoordinator kaydedildi");
                
                // NotificationCoordinator - INotificationService'e baÄŸÄ±mlÄ±
                builder.Services.AddSingleton<INotificationCoordinator, NotificationCoordinator>();
                KamPay.Helpers.AppLogger.DebugLog("  âœ“ INotificationCoordinator kaydedildi");
                
                // TransactionOrchestrator - ITransactionService, INotificationService, IProductService, IUserProfileService'e baÄŸÄ±mlÄ±
                builder.Services.AddSingleton<ITransactionOrchestrator, TransactionOrchestrator>();
                KamPay.Helpers.AppLogger.DebugLog("  âœ“ ITransactionOrchestrator kaydedildi");

                // UserStateService - Singleton olarak global kullanÄ±cÄ± durumu yÃ¶netimi
                //  TÃ¼m baÄŸÄ±mlÄ± servisler yukarÄ±da kayÄ±tlÄ± olduÄŸu iÃ§in burada tanÄ±mlanÄ±yor
                builder.Services.AddSingleton<IUserStateService>(sp =>
                    new UserStateService(
                        sp.GetRequiredService<IAuthenticationService>(),
                        sp.GetRequiredService<IUserProfileService>(),
                        sp.GetRequiredService<IProductService>(),
                        sp.GetRequiredService<IServiceSharingService>(),
                        sp.GetRequiredService<IGoodDeedService>(),
                        sp.GetRequiredService<IMessagingService>())
                );

                // Security Audit Service kaydÄ±
                builder.Services.AddSingleton<ISecurityAuditService, FirebaseSecurityAuditService>(); 

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

                // ğŸ¯ ARMUT MODELÄ°: Yeni ViewModels
                builder.Services.AddTransient<CreateCustomerRequestViewModel>();
                builder.Services.AddTransient<CustomerRequestsListViewModel>();
                builder.Services.AddTransient<CustomerRequestDetailsViewModel>();
                
                // âœ… Pages - TÃ¼m parametreli constructor'a sahip sayfalar
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

                // ğŸ¯ ARMUT MODELÄ°: Yeni Pages
                builder.Services.AddTransient<CreateCustomerRequestPage>();
                builder.Services.AddTransient<CustomerRequestsListPage>();
                builder.Services.AddTransient<CustomerRequestDetailsPage>();

                //  Singleton yaptÄ±k: Sayfa ve ViewModel bir kere oluÅŸturulur ve hafÄ±zada kalÄ±r.
                builder.Services.AddSingleton<ICategoryService, FirebaseCategoryService>();

#if DEBUG
                builder.Logging.AddDebug();
#endif
                LocalizationResourceManager.EnsureInitialized();
                KamPay.Helpers.AppLogger.DebugLog("âš™ï¸ MauiApp build ediliyor...");
                var app = builder.Build();
                KamPay.Helpers.AppLogger.DebugLog("âœ“ MauiApp baÅŸarÄ±lÄ±yla oluÅŸturuldu");

                return app;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ KRITIK: MauiProgram.CreateMauiApp hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ StackTrace: {ex.StackTrace}");
                throw; // Kritik hatalar yeniden fÄ±rlatÄ±lmalÄ±
            }
        }

        /// <summary>
        /// appsettings.json'dan ayarlarÄ± yÃ¼kler
        /// </summary>
        private static AppConfig LoadAppConfig()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "KamPay.appsettings.json";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ {resourceName} bulunamadÄ±");
                    return new AppConfig();
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ appsettings.json okunamadÄ±: {ex.Message}");
                return new AppConfig();
            }
        }

        /// <summary>
        /// VarsayÄ±lan email ayarlarÄ± (appsettings.json okunamazsa)
        /// </summary>
        private static EmailSettings GetDefaultEmailSettings()
        {
            KamPay.Helpers.AppLogger.DebugLog("âš ï¸ VarsayÄ±lan SMTP ayarlarÄ± kullanÄ±lÄ±yor (PLACEHOLDER)");
            return new EmailSettings
            {
                SmtpHost = "smtp.bartin.edu.tr",
                SmtpPort = 587,
                UseSsl = true,
                FromEmail = "kampay@bartin.edu.tr",
                FromName = "KamPay DoÄŸrulama",
                Username = "kampay@bartin.edu.tr",
                Password = "SMTP_PAROLASI_BURAYA" // âš ï¸ appsettings.json'da gerÃ§ek ÅŸifre olmalÄ±
            };
        }

        /// <summary>
        /// VarsayÄ±lan Firebase config
        /// </summary>
        private static FirebaseConfigSettings GetDefaultFirebaseConfig()
        {
            KamPay.Helpers.AppLogger.DebugLog("âš ï¸ VarsayÄ±lan Firebase config kullanÄ±lÄ±yor");
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
        /// appsettings.json deserializasyon iÃ§in model
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

