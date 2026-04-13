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
            Circle1.AbortAnimation("CircleRotation");
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