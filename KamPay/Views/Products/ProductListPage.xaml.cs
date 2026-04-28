using KamPay.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.UI.Maui;

namespace KamPay.Views
{
    public partial class ProductListPage : ContentPage
    {
        private readonly ProductListViewModel _viewModel;
        private bool _hasAnimated = false;

        public ProductListPage(ProductListViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = vm;

            // Faz 4: ViewModel scroll-to-top isteği gönderince SfListView'ı en başa konumla
            _viewModel.ScrollToTopRequested += OnScrollToTopRequested;
        }

        private void OnScrollToTopRequested(object? sender, EventArgs e)
        {
            if (ProductsListView?.DataSource?.DisplayItems?.Count > 0)
            {
                ProductsListView.ScrollTo(0);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                AnimateBackgroundCircle();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Sayfa kapanınca animasyonu durdur (kaynak sızıntısını önle)
            try { Circle1?.AbortAnimation("CircleRotation"); } catch { }
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            // Sayfa dispose edilince event'i temizle (memory leak önlemi)
            if (Handler == null)
            {
                _viewModel.ScrollToTopRequested -= OnScrollToTopRequested;
            }
        }

        /// <summary>
        /// ✅ DÜZELTME: MAUI'nin native Animation API'si ile tekrarlayan animasyon.
        /// Task.Run + while(true) yerine Animation.Commit kullanılıyor.
        /// Bu yöntem UI thread'i bloke etmez ve scroll performansını etkilemez.
        /// </summary>
        private void AnimateBackgroundCircle()
        {
            var animation = new Animation(v => Circle1.Rotation = v, 0, 360);
            animation.Commit(
                owner: Circle1,
                name: "CircleRotation",
                length: 30000,
                easing: Easing.Linear,
                repeat: () => true  // Sonsuz tekrar — ama UI thread'i bloke etmeden!
            );
        }
    }
}