using KamPay.ViewModels;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace KamPay.Views
{
    public partial class RegisterPage : ContentPage
    {
        private bool _hasAnimated = false;

        public RegisterPage(RegisterViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            // ViewModel deðiþikliklerini güvenli bir þekilde dinle
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is RegisterViewModel vm && e.PropertyName == nameof(vm.ShowVerificationSection))
            {
                // UI güncellemelerini ana iþ parçacýðýnda yap
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (vm.ShowVerificationSection)
                    {
                        await AnimateVerificationCardAsync();
                    }
                });
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_hasAnimated)
            {
                _hasAnimated = true;
                // Sayfa yüklendikten hemen sonra animasyonu baþlat
                await Task.Delay(100);
                await AnimatePageAsync();
            }
        }

        private async Task AnimatePageAsync()
        {
            try
            {
                // Baþlangýç durumlarýný ayarla
                HeaderSection.Opacity = 0;
                HeaderSection.TranslationY = -30;
                RegisterFormCard.Opacity = 0;
                RegisterFormCard.TranslationY = 50;

                // Arka plan animasyonunu baþlat (Artýk thread kilitlemez)
                StartBackgroundAnimations();

                // Header animasyonu
                await Task.WhenAll(
                    HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                    HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
                );

                await Task.Delay(150);

                // Kayýt formu animasyonu
                await Task.WhenAll(
                    RegisterFormCard.FadeTo(1, 700, Easing.CubicOut),
                    RegisterFormCard.TranslateTo(0, 0, 700, Easing.CubicOut)
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initial Animation Error: {ex.Message}");
            }
        }

        private async Task AnimateVerificationCardAsync()
        {
            if (RegisterFormCard == null || VerificationCard == null) return;

            try
            {
                // 1. Kayýt formunu fade-out ile gizle
                await RegisterFormCard.FadeTo(0, 250, Easing.CubicIn);
                RegisterFormCard.IsVisible = false; // XAML binding'i sildiðimiz için manuel yönetiyoruz

                // 2. Doðrulama kartýný hazýrla
                VerificationCard.Opacity = 0;
                VerificationCard.TranslationY = 40;
                VerificationCard.Scale = 0.95;
                VerificationCard.IsVisible = true;

                // 3. Doðrulama kartýný içeri al
                await Task.WhenAll(
                    VerificationCard.FadeTo(1, 500, Easing.CubicOut),
                    VerificationCard.TranslateTo(0, 0, 500, Easing.CubicOut),
                    VerificationCard.ScaleTo(1, 500, Easing.CubicOut)
                );

                // Küçük bir onay "pop" efekti
                await VerificationCard.ScaleTo(1.03, 100, Easing.BounceOut);
                await VerificationCard.ScaleTo(1.0, 100, Easing.BounceIn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Transition Animation Error: {ex.Message}");
                // Hata olsa bile kartlarý göster ki kullanýcý bloke olmasýn
                RegisterFormCard.IsVisible = false;
                VerificationCard.IsVisible = true;
                VerificationCard.Opacity = 1;
            }
        }

        private void StartBackgroundAnimations()
        {
            // Eski 'while(true)' mantýðý yerine MAUI'nin yerleþik Animation sistemini kullanýyoruz.
            // Bu yöntem çok daha az CPU harcar ve MainThread'i kilitlemez.

            // Circle 1: Saat yönünde sürekli dönüþ
            var animation1 = new Animation(v => Circle1.Rotation = v, 0, 360);
            animation1.Commit(this, "Circle1Rotation", 16, 25000, Easing.Linear, repeat: () => true);

            // Circle 2: Saat yönü tersine sürekli dönüþ
            var animation2 = new Animation(v => Circle2.Rotation = v, 0, -360);
            animation2.Commit(this, "Circle2Rotation", 16, 20000, Easing.Linear, repeat: () => true);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Sayfadan çýkýldýðýnda animasyonlarý durdur (Memory leak önleme)
            this.AbortAnimation("Circle1Rotation");
            this.AbortAnimation("Circle2Rotation");
        }
    }
}