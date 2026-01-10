using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class OffersPage : ContentPage
    {
        private readonly OffersViewModel _viewModel;
        private bool _hasAnimated = false;

        public OffersPage(OffersViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("✅ OffersPage: Aktif (Listener zaten çalışıyor)");

            // Animasyonları çalıştır
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }
        }

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            TabSection.Opacity = 0;
            TabSection.TranslationY = 20;
            ContentSection.Opacity = 0;
            ContentSection.TranslationY = 30;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await HeaderSection.FadeTo(1, 400, Easing.CubicOut);

            await Task.Delay(100);

            // Tab section animation
            await Task.WhenAll(
                TabSection.FadeTo(1, 500, Easing.CubicOut),
                TabSection.TranslateTo(0, 0, 500, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Content section animation
            await Task.WhenAll(
                ContentSection.FadeTo(1, 600, Easing.CubicOut),
                ContentSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
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
            if (BindingContext is OffersViewModel vm)
            {
                vm.Dispose();
            }
            
            System.Diagnostics.Debug.WriteLine("✅ OffersPage: Listener cleanup yapıldı");
        }
    }
}
