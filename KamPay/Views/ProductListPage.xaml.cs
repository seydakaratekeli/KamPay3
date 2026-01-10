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
                await AnimatePageAsync();
            }
        }

        private async Task AnimatePageAsync()
        {
            // Arka plan animasyonu
            AnimateBackgroundCircle();
        }

        private void AnimateBackgroundCircle()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle1.RotateTo(360, 30000, Easing.Linear);
                            Circle1.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // ✅ Dispose ViewModel to cleanup listeners
            if (BindingContext is ProductListViewModel vm)
            {
                vm.Dispose();
            }
            
            System.Diagnostics.Debug.WriteLine("✅ ProductListPage: Listener cleanup yapıldı");
        }
    }
}