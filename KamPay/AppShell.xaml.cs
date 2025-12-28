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

                // ✅ CRITICAL FIX: Kullanıcı ID kontrolü - Logout yapılmışsa burada login ekranına yönlendir
                var userId = Preferences.Get("current_user_id", string.Empty);

                if (!string.IsNullOrEmpty(userId))
                {
                    // ✅ KONTROL: Kullanıcı bilgisi gerçekten geçerli mi?
                    // Eğer UserStateService'de kullanıcı yoksa, Preferences'ı temizle
                    try
                    {
                        var userStateService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUserStateService>();
                        if (userStateService != null)
                        {
                            var currentUser = userStateService.CurrentUser;
                            
                            // Eğer UserStateService'de kullanıcı yoksa, logout yapılmış demektir
                            if (currentUser == null)
                            {
                                Console.WriteLine("⚠️ Preferences'ta userId var ama UserStateService'de kullanıcı yok - temizleniyor");
                                Preferences.Remove("current_user_id");
                                Preferences.Remove("current_user_email");
                                
                                // Login sayfasında kal
                                await GoToAsync("//LoginPage");
                                return;
                            }
                        }
                        
                        // Kullanıcı geçerliyse ana ekrana yönlendir
                        await GoToAsync("//MainApp");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Kullanıcı doğrulama hatası: {ex.Message}");
                        // Hata durumunda güvenli taraf: Preferences'ı temizle ve login'de kal
                        Preferences.Remove("current_user_id");
                        Preferences.Remove("current_user_email");
                        await GoToAsync("//LoginPage");
                    }
                }
                else
                {
                    // userId yoksa zaten login ekranındayız, hiçbir şey yapma
                    Console.WriteLine("✅ userId yok, login ekranında kalınıyor");
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