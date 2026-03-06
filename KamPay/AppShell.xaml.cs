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
                System.Diagnostics.Debug.WriteLine("⚙️ AppShell başlatılıyor...");
                
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("✓ AppShell.InitializeComponent tamamlandı");
                
                _viewModel = vm;
                BindingContext = vm;
                System.Diagnostics.Debug.WriteLine("✓ AppShell.BindingContext atandı");

                // Rota Kayıtları (Mevcut kodun aynısı)
                RegisterRoutes();
                System.Diagnostics.Debug.WriteLine("✓ AppShell rotaları kaydedildi");
                
                // Tab title'ları ayarla
                SetupTabTitles();
                
                System.Diagnostics.Debug.WriteLine("✓ AppShell başarıyla başlatıldı");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ KRITIK: AppShell constructor hatası: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"⚠️ StackTrace: {ex.StackTrace}");
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
                System.Diagnostics.Debug.WriteLine($"⚠️ Tab title setup hatası: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"⚠️ Tab title güncelleme hatası: {ex.Message}");
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

                // ✅ NOT: Otomatik giriş App.OnStart içinde yapıldı
                // Burada sadece mevcut durumu kontrol ediyoruz
                
                var userId = Preferences.Get("current_user_id", string.Empty);

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
                                Console.WriteLine("⚠️ UserStateService'de kullanıcı yok - login ekranına yönlendiriliyor");
                                Preferences.Remove("current_user_id");
                                Preferences.Remove("current_user_email");
                                Preferences.Remove("firebase_token");
                                Preferences.Remove("remember_me");
                                Preferences.Remove("token_expiry");
                                
                                // Login sayfasında kal
                                await GoToAsync("//LoginPage");
                                return;
                            }
                            else
                            {
                                // Kullanıcı geçerli - ana ekrana yönlendir (eğer login sayfasındaysa)
                                if (CurrentState?.Location?.ToString().Contains("LoginPage") == true)
                                {
                                    Console.WriteLine("✅ Geçerli kullanıcı var - ana ekrana yönlendiriliyor");
                                    await GoToAsync("//MainApp");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Kullanıcı doğrulama hatası: {ex.Message}");
                        // Hata durumunda güvenli taraf: Login ekranında kal
                        await GoToAsync("//LoginPage");
                    }
                }
                else
                {
                    // userId yoksa login ekranında kal
                    Console.WriteLine("⏭️ userId yok, login ekranında kalınıyor");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AppShell.OnAppearing hatası: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }
}