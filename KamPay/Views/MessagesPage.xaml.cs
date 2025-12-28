using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class MessagesPage : ContentPage
    {
        private readonly MessagesViewModel _viewModel;
        private bool _hasAnimated = false;

        public MessagesPage(MessagesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        //  Sayfa her göründüğünde çağrılır
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ViewModel'in initialize metodunu çağır
            await _viewModel.InitializeAsync();

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
            SearchSection.Opacity = 0;
            SearchSection.TranslationY = 30;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Search section animation
            await Task.WhenAll(
                SearchSection.FadeTo(1, 700, Easing.CubicOut),
                SearchSection.TranslateTo(0, 0, 700, Easing.CubicOut)
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

        //  Sayfa kaybolduğunda listener'ları temizle
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Dispose otomatik çağrılır, ekstra birşey yapma
        }
    }
}

