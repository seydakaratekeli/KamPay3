// KamPay/AppShell.xaml.cs

using KamPay.Views;
using KamPay.ViewModels;
using KamPay.Services;

namespace KamPay
{
    public partial class AppShell : Shell
    {
        private bool _isNavigating = false;
        private readonly AppShellViewModel _viewModel;

        public AppShell(AppShellViewModel vm)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog("⚙️ AppShell başlatılıyor...");
                
                InitializeComponent();
                KamPay.Helpers.AppLogger.DebugLog("✓ AppShell.InitializeComponent tamamlandı");
                
                _viewModel = vm;
                BindingContext = vm;
                KamPay.Helpers.AppLogger.DebugLog("✓ AppShell.BindingContext atandı");

                // Rota Kayıtları (Mevcut kodun aynısı)
                RegisterRoutes();
                KamPay.Helpers.AppLogger.DebugLog("✓ AppShell rotaları kaydedildi");
                
                // Tab title'ları ayarla
                SetupTabTitles();
                
                KamPay.Helpers.AppLogger.DebugLog("✓ AppShell başarıyla başlatıldı");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ KRITIK: AppShell constructor hatası: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ StackTrace: {ex.StackTrace}");
                throw; // Constructor'da kritik hatalar yeniden fırlatılmalı
            }
        }

        private void SetupTabTitles()
        {
            try
            {
                // ViewModel'den PropertyChanged eventi dinle
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == null || 
                        e.PropertyName == nameof(AppShellViewModel.HomeTitle) ||
                        e.PropertyName == nameof(AppShellViewModel.ServicesTitle) ||
                        e.PropertyName == nameof(AppShellViewModel.GoodDeedBoardTitle) ||
                        e.PropertyName == nameof(AppShellViewModel.MessagesTitle) ||
                        e.PropertyName == nameof(AppShellViewModel.ProfileTitle))
                    {
                        UpdateTabTitles();
                    }
                };
                
                // İlk kez title'ları ayarla
                UpdateTabTitles();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Tab title setup hatası: {ex.Message}");
            }
        }

        private void UpdateTabTitles()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrEmpty(_viewModel.HomeTitle))
                        HomeTab.Title = _viewModel.HomeTitle;
                    
                    if (!string.IsNullOrEmpty(_viewModel.ServicesTitle))
                        ServicesTab.Title = _viewModel.ServicesTitle;
                    
                    if (!string.IsNullOrEmpty(_viewModel.GoodDeedBoardTitle))
                        GoodDeedTab.Title = _viewModel.GoodDeedBoardTitle;
                    
                    if (!string.IsNullOrEmpty(_viewModel.MessagesTitle))
                        MessagesTab.Title = _viewModel.MessagesTitle;
                    
                    if (!string.IsNullOrEmpty(_viewModel.ProfileTitle))
                        ProfileTab.Title = _viewModel.ProfileTitle;
                });
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"⚠️ Tab title güncelleme hatası: {ex.Message}");
            }
        }

        private void RegisterRoutes()
        {
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));

            Routing.RegisterRoute(nameof(AddProductPage), typeof(AddProductPage));
            Routing.RegisterRoute(nameof(EditProductPage), typeof(EditProductPage));
            Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
            Routing.RegisterRoute(nameof(FavoritesPage), typeof(FavoritesPage));
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

            // 🎯 ARMUT MODELİ: Yeni Rotalar
            Routing.RegisterRoute(nameof(CreateCustomerRequestPage), typeof(CreateCustomerRequestPage));
            Routing.RegisterRoute(nameof(CustomerRequestsListPage), typeof(CustomerRequestsListPage));
            Routing.RegisterRoute("CustomerRequestDetailsPage", typeof(CustomerRequestDetailsPage));
        }

        // ✅ GÜNCELLEME: Shell ilk göründüğünde giriş kontrolünü yap
        // NOT: Otomatik giriş App.xaml.cs'de yapılıyor, burada sadece route kontrolü yapılıyor
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Çoklu yönlendirmeyi önlemek için flag kontrol et
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // 🔒 GÜVENLIK: SecureStorage'dan kontrol et
                string userId = string.Empty;
                try
                {
                    userId = await SecureStorage.GetAsync("secure_user_id") ?? string.Empty;
                }
                catch (Exception ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"⚠️ SecureStorage okuma hatası: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    // Kullanıcı bilgisi varsa UserStateService'i kontrol et
                    try
                    {
                        var userStateService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                        if (userStateService != null)
                        {
                            var currentUser = userStateService.CurrentUser;
                            
                            // Eğer UserStateService'de kullanıcı yoksa, logout yapılmış demektir
                            if (currentUser == null)
                            {
                                KamPay.Helpers.AppLogger.DebugLog("⚠️ UserStateService'de kullanıcı yok - login ekranına yönlendiriliyor");
                                
                                // 🔒 GÜVENLIK: SecureStorage'ı temizle
                                SecureStorage.Remove("secure_user_id");
                                SecureStorage.Remove("secure_user_email");
                                SecureStorage.Remove("secure_firebase_token");
                                SecureStorage.Remove("secure_remember_me");
                                SecureStorage.Remove("secure_token_expiry");
                                
                                // Login sayfasında kal
                                await GoToAsync("//LoginPage");
                                return;
                            }
                            else
                            {
                                // Kullanıcı geçerli - ana ekrana yönlendir (eğer login sayfasındaysa)
                                if (CurrentState?.Location?.ToString().Contains("LoginPage") == true)
                                {
                                    KamPay.Helpers.AppLogger.DebugLog("✅ Geçerli kullanıcı var - ana ekrana yönlendiriliyor");
                                    await GoToAsync("//MainApp");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"⚠️ Kullanıcı doğrulama hatası: {ex.Message}");
                        // Hata durumunda güvenli taraf: Login ekranında kal
                        await GoToAsync("//LoginPage");
                    }
                }
                else
                {
                    // userId yoksa login ekranında kal
                    KamPay.Helpers.AppLogger.DebugLog("⏭️ userId yok, login ekranında kalınıyor");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"❌ AppShell.OnAppearing hatası: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }
}
