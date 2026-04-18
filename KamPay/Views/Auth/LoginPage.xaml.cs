using KamPay.ViewModels;
using Microsoft.Maui.Controls;

namespace KamPay.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage(LoginViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Küçük bir gecikme ile animasyonlarý baþlat
            await Task.Delay(100);
            await AnimatePageAsync();
        }

        private async Task AnimatePageAsync()
        {
            // Reset initial states
            LogoSection.Opacity = 0;
            LogoSection.TranslationY = -50;
            LoginFormCard.Opacity = 0;
            LoginFormCard.TranslationY = 50;

            // Animate background circles (sürekli dönme)
            AnimateBackgroundCircles();

            // Animate logo section
            await Task.WhenAll(
                LogoSection.FadeTo(1, 800, Easing.CubicOut),
                LogoSection.TranslateTo(0, 0, 800, Easing.CubicOut)
            );

            await Task.Delay(200);

            // Animate login form card
            await Task.WhenAll(
                LoginFormCard.FadeTo(1, 600, Easing.CubicOut),
                LoginFormCard.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircles()
        {
            // Sürekli dönen animasyonlar için Task.Run kullan
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // MainThread'de animasyon çalýþtýr
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Task.WhenAll(
                                Circle1.RotateTo(360, 20000, Easing.Linear),
                                Circle2.RotateTo(-360, 25000, Easing.Linear)
                            );

                            Circle1.Rotation = 0;
                            Circle2.Rotation = 0;
                        });
                    }
                    catch
                    {
                        // Sayfa kapatýlýrsa veya hata olursa döngüyü kýr
                        break;
                    }
                }
            });
        }
    }
}