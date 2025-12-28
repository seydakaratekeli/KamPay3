// KamPay/AppShell.xaml.cs

using KamPay.Views;
using KamPay.ViewModels;

namespace KamPay
{
    public partial class AppShell : Shell
    {
        private bool _isNavigating = false;

        public AppShell(AppShellViewModel vm)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("⚙️ AppShell başlatılıyor...");
                
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("✓ AppShell.InitializeComponent tamamlandı");
                
                BindingContext = vm;
                System.Diagnostics.Debug.WriteLine("✓ AppShell.BindingContext atandı");

                // Rota Kayıtları (Mevcut kodun aynısı)
                RegisterRoutes();
                System.Diagnostics.Debug.WriteLine("✓ AppShell rotaları kaydedildi");
                
                System.Diagnostics.Debug.WriteLine("✓ AppShell başarıyla başlatıldı");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ KRITIK: AppShell constructor hatası: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Constructor'da kritik hatalar yeniden fırlatılmalı
            }
        }

        private void RegisterRoutes()
        {
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(AddProductPage), typeof(AddProductPage));
            Routing.RegisterRoute(nameof(EditProductPage), typeof(EditProductPage));
            Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
            Routing.RegisterRoute(nameof(OffersPage), typeof(OffersPage));
            Routing.RegisterRoute(nameof(TradeOfferView), typeof(TradeOfferView));
            Routing.RegisterRoute(nameof(GoodDeedBoardPage), typeof(GoodDeedBoardPage));
            Routing.RegisterRoute(nameof(ServiceSharingPage), typeof(ServiceSharingPage));
            Routing.RegisterRoute(nameof(QRCodeDisplayPage), typeof(QRCodeDisplayPage));
            Routing.RegisterRoute("qrscanner", typeof(QRScannerPage));
            Routing.RegisterRoute(nameof(ServiceRequestsPage), typeof(ServiceRequestsPage));
            Routing.RegisterRoute(nameof(PaymentPage), typeof(PaymentPage));
            Routing.RegisterRoute(nameof(SurpriseBoxPage), typeof(SurpriseBoxPage));
            Routing.RegisterRoute(nameof(ImageViewerPage), typeof(ImageViewerPage));
            Routing.RegisterRoute("myproducts", typeof(ProductListPage));
        }

        // Shell ilk göründüğünde giriş kontrolünü yap
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Çoklu yönlendirmeyi önlemek için flag kontrol et
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // Kullanıcı ID kontrolü
                var userId = Preferences.Get("current_user_id", string.Empty);

                if (!string.IsNullOrEmpty(userId))
                {
                    // Shell nesnesi artık hazır olduğu için yönlendir
                    // Ama hata oluşursa kapat değil, login'de kal
                    try
                    {
                        await GoToAsync("//MainApp");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Navigation Error] Could not navigate to MainApp: {ex.Message}");
                        // Hata durumunda login sayfasında kal
                        Preferences.Remove("current_user_id");
                        Preferences.Remove("current_user_email");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell Error] {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }
}