using KamPay.ViewModels;
using Mapsui.UI.Maui;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace KamPay.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfileViewModel _viewModel;
        private bool _hasAnimated = false;
        private CancellationTokenSource? _animationCts;

        public ProfilePage(ProfileViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Arka plan animasyonunu başlat
            StartBackgroundRotation();

            // İlk açılış animasyonları
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                // UI donmasını önlemek için sekme geçişine zaman tanı
                await Task.Delay(350);
                await AnimatePageAsync();

                // Animasyonlar bittikten sonra veriyi beklemeden (async ateşle unut) tetikle
                _ = _viewModel.InitializeAsync();
            }
            else
            {
                // Daha önce animasyon yapıldıysa doğrudan çek
                _ = _viewModel.InitializeAsync();
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

            // Header ve Profil resmi
            await HeaderSection.FadeTo(1, 400, Easing.CubicOut);
            await Task.WhenAll(
                ProfileImageSection.ScaleTo(1, 600, Easing.SpringOut),
                ProfileImageSection.FadeTo(1, 600, Easing.CubicOut)
            );

            // Yazılar
            await Task.WhenAll(
                NameLabel.FadeTo(1, 500, Easing.CubicOut),
                EmailLabel.FadeTo(1, 500, Easing.CubicOut)
            );

            // İstatistikler ve İçerik
            await Task.WhenAll(
                StatsSection.FadeTo(1, 600, Easing.CubicOut),
                StatsSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
            await Task.WhenAll(
                ContentSection.FadeTo(1, 700, Easing.CubicOut),
                ContentSection.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private void StartBackgroundRotation()
        {
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (Circle1 != null)
                            {
                                await Circle1.RotateTo(360, 30000, Easing.Linear);
                                Circle1.Rotation = 0;
                            }
                        });
                    }
                    catch { break; }
                }
            }, token);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Sayfa kapandığında animasyon döngüsünü durdur
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = null;
        }
    }
}