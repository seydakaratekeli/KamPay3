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

namespace KamPay
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseBarcodeReader()
                .UseMauiCommunityToolkit()
                            .UseFFImageLoading() // ✅ BURAYA EKLE

                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });


            // 📧 E-posta Ayarları
            // Gerçek değerler IT'den alınmalı (örnek olarak gösteriliyor)
            var emailSettings = new EmailSettings
            {
                SmtpHost = "smtp.bartin.edu.tr",
                SmtpPort = 587,          // IT'den gelen bilgiye göre 465 de olabilir
                UseSsl = true,
                FromEmail = "kampay@bartin.edu.tr",
                FromName = "KamPay Doğrulama",
                Username = "kampay@bartin.edu.tr",
                Password = "SMTP_PAROLASI_BURAYA" // ⚠️ Gerçek uygulamada güvenli kaynakta sakla
            };

            // Servislerin DI kaydı
            builder.Services.AddSingleton(emailSettings);
            builder.Services.AddSingleton<IEmailService, EmailService>();

            // 👇 FirebaseAuthService, constructor'da IEmailService beklediği için bu şekilde ekliyoruz:
            builder.Services.AddSingleton<IAuthenticationService>(sp =>
                new FirebaseAuthService(sp.GetRequiredService<IEmailService>())
            );

            // AppShell'in kendisini ve ViewModel'ini DI container'a kaydediyoruz.
            builder.Services.AddSingleton<AppShellViewModel>();
            builder.Services.AddSingleton<AppShell>();

            builder.Services.AddSingleton<IProductService, FirebaseProductService>();
            builder.Services.AddSingleton<IStorageService, FirebaseStorageService>();

            builder.Services.AddSingleton<IFavoriteService>(sp =>
             new FirebaseFavoriteService(sp.GetRequiredService<INotificationService>()));

            builder.Services.AddSingleton<IMessagingService>(sp =>
      new FirebaseMessagingService(sp.GetRequiredService<INotificationService>()));

            builder.Services.AddSingleton<IUserProfileService, FirebaseUserProfileService>();
            builder.Services.AddSingleton<IQRCodeService>(sp =>
                new FirebaseQRCodeService(sp.GetRequiredService<IUserProfileService>())
            );

            // ✅ SERVICES BÖLÜMÜNE EKLE (mevcut satırlardan sonra)
            builder.Services.AddSingleton<IFirebaseObserverService, FirebaseObserverService>();
            builder.Services.AddSingleton<IProductCacheService, ProductCacheService>();
            builder.Services.AddSingleton<IReverseGeocodeService, ReverseGeocodeService>();
            builder.Services.AddSingleton<ISurpriseBoxService>(sp =>
                new FirebaseSurpriseBoxService(
                    sp.GetRequiredService<IUserProfileService>(),
                    sp.GetRequiredService<IProductService>(),
                    sp.GetRequiredService<INotificationService>() // 🔥 YENİ
                )
            ); builder.Services.AddSingleton<IGoodDeedService, FirebaseGoodDeedService>();

            builder.Services.AddSingleton<IServiceSharingService>(sp =>
     new FirebaseServiceSharingService(
         sp.GetRequiredService<INotificationService>(),
         sp.GetRequiredService<IUserProfileService>() // <-- YENİ EKLENEN SATIR
     )
 );
            builder.Services.AddSingleton<INotificationService, FirebaseNotificationService>();

            builder.Services.AddSingleton<ITransactionService>(sp =>
           new FirebaseTransactionService(
          sp.GetRequiredService<INotificationService>(),
          sp.GetRequiredService<IProductService>(),
          sp.GetRequiredService<IQRCodeService>(),
            sp.GetRequiredService<IUserProfileService>()) // IQRCodeService'i buraya ekleyin
  );

            // ViewModels
            // builder.Services.AddSingleton<AppShellViewModel>(); // Singleton olarak ekliyoruz
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
            builder.Services.AddTransient<ServiceRequestsViewModel>(); // Bu satırı ekleyin
            builder.Services.AddTransient<SurpriseBoxViewModel>();

            // Views
            builder.Services.AddTransient<SurpriseBoxPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<EditProductPage>();
            builder.Services.AddTransient<AddProductPage>();
            builder.Services.AddTransient<ProductListPage>();
            builder.Services.AddTransient<ProductDetailPage>();
            builder.Services.AddTransient<MessagesPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<FavoritesPage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<NotificationsPage>();
            // 🔥 Singleton yaptık: Sayfa ve ViewModel bir kere oluşturulur ve hafızada kalır.
            builder.Services.AddSingleton<OffersViewModel>();
            builder.Services.AddSingleton<OffersPage>();
            builder.Services.AddTransient<TradeOfferView>();
            builder.Services.AddTransient<GoodDeedBoardPage>();
            builder.Services.AddTransient<ServiceSharingPage>();
            builder.Services.AddTransient<QRCodeDisplayPage>();
            builder.Services.AddTransient<QRScannerPage>();
            builder.Services.AddTransient<ServiceRequestsPage>(); 
            builder.Services.AddSingleton<ICategoryService, FirebaseCategoryService>();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
