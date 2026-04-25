using KamPay.ViewModels;
using Microsoft.Maui.Controls;

namespace KamPay.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _vm;

        public LoginPage(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;

            // ✅ EmailEntry Return tuşuna basınca PasswordEntry'ye odaklan
            // XAML'deki FocusPasswordCommand binding yerine code-behind çözümü
            EmailEntry.Completed += (s, e) => PasswordEntry.Focus();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Küçük gecikme ile animasyonları başlat
            await Task.Delay(100);
            await AnimatePageAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Sayfa kapanınca animasyonu durdur (bellek sızıntısını önle)
            this.AbortAnimation("Circle1Rotation");
            this.AbortAnimation("Circle2Rotation");
        }

        private async Task AnimatePageAsync()
        {
            // Başlangıç durumlarını sıfırla
            LogoSection.Opacity = 0;
            LogoSection.TranslationY = -50;
            LoginFormCard.Opacity = 0;
            LoginFormCard.TranslationY = 50;

            // Arka plan dairelerini döndür
            AnimateBackgroundCircles();

            // Logo animasyonu
            await Task.WhenAll(
                LogoSection.FadeTo(1, 800, Easing.CubicOut),
                LogoSection.TranslateTo(0, 0, 800, Easing.CubicOut)
            );

            await Task.Delay(200);

            // Form kartı animasyonu
            await Task.WhenAll(
                LoginFormCard.FadeTo(1, 600, Easing.CubicOut),
                LoginFormCard.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircles()
        {
            // ✅ Task.Run + while(true) yerine Animation.Commit kullan
            // Bu yöntem UI thread'i bloke etmez, sayfa kapanınca da temiz durur
            var animation1 = new Animation(v => Circle1.Rotation = v, 0, 360);
            animation1.Commit(
                owner: Circle1,
                name: "Circle1Rotation",
                length: 20000,
                easing: Easing.Linear,
                repeat: () => true
            );

            var animation2 = new Animation(v => Circle2.Rotation = v, 0, -360);
            animation2.Commit(
                owner: Circle2,
                name: "Circle2Rotation",
                length: 25000,
                easing: Easing.Linear,
                repeat: () => true
            );
        }
    }
}