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

            // ?? Artýk manuel doðrulama yok, PropertyChanged dinlemeye gerek yok
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
            try
            {
                // Baþlangýç durumlarýný ayarla
                HeaderSection.Opacity = 0;
                HeaderSection.TranslationY = -30;
                RegisterFormCard.Opacity = 0;
                RegisterFormCard.TranslationY = 50;

                // Arka plan animasyonunu baþlat
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

        private void StartBackgroundAnimations()
        {
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
            // Sayfadan çýkýldýðýnda animasyonlarý durdur
            this.AbortAnimation("Circle1Rotation");
            this.AbortAnimation("Circle2Rotation");
        }
    }
}