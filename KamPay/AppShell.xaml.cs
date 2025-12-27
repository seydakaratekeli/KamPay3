// KamPay/AppShell.xaml.cs

using KamPay.Views;
using KamPay.ViewModels;

namespace KamPay
{
    public partial class AppShell : Shell
    {
        public AppShell(AppShellViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            // Rota Kayıtları (Mevcut kodun aynısı)
            RegisterRoutes();
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
            Routing.RegisterRoute(nameof(SurpriseBoxPage), typeof(SurpriseBoxPage));
            Routing.RegisterRoute(nameof(ImageViewerPage), typeof(ImageViewerPage));
            Routing.RegisterRoute("myproducts", typeof(ProductListPage));
        }

        // Shell ilk göründüğünde giriş kontrolünü yap
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Kullanıcı ID kontrolü
            var userId = Preferences.Get("current_user_id", string.Empty);

            if (!string.IsNullOrEmpty(userId))
            {
                // Shell nesnesi artık hazır olduğu için yarış riski olmadan yönlendir
                await GoToAsync("//MainApp");
            }
        }
    }
}