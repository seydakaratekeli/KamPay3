using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfileViewModel _viewModel;
        private bool _hasAnimated = false;

        public ProfilePage(ProfileViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        //  Sayfa her göründüğünde SADECE cache kontrolü yap
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // InitializeAsync cache kontrolü yapar, gerekirse yükler
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
            ProfileImageSection.Scale = 0.5;
            ProfileImageSection.Opacity = 0;
            NameLabel.Opacity = 0;
            EmailLabel.Opacity = 0;
            StatsSection.Opacity = 0;
            StatsSection.TranslationY = 20;
            ContentSection.Opacity = 0;
            ContentSection.TranslationY = 30;

            // Background animation
            AnimateBackgroundCircle();

            // Header background animation
            await HeaderSection.FadeTo(1, 400, Easing.CubicOut);

            await Task.Delay(100);

            // Profile image scale animation
            await Task.WhenAll(
                ProfileImageSection.ScaleTo(1, 600, Easing.SpringOut),
                ProfileImageSection.FadeTo(1, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Name and email animation
            await Task.WhenAll(
                NameLabel.FadeTo(1, 500, Easing.CubicOut),
                EmailLabel.FadeTo(1, 500, Easing.CubicOut)
            );

            await Task.Delay(100);

            // Stats section animation
            await Task.WhenAll(
                StatsSection.FadeTo(1, 600, Easing.CubicOut),
                StatsSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Content section animation
            await Task.WhenAll(
                ContentSection.FadeTo(1, 700, Easing.CubicOut),
                ContentSection.TranslateTo(0, 0, 700, Easing.CubicOut)
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
            // Dispose etme - cache'i koruyalım
            System.Diagnostics.Debug.WriteLine("⏸️ ProfilePage: Arka plana alındı");
        }
    }
}