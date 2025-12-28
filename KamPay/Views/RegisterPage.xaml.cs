using KamPay.ViewModels;
using Microsoft.Maui.Controls;

namespace KamPay.Views
{
    public partial class RegisterPage : ContentPage
    {
        private bool _hasAnimated = false;

        public RegisterPage(RegisterViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            // ViewModel deðiþikliklerini dinle
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.ShowVerificationSection))
                {
                    if (vm.ShowVerificationSection)
                    {
                        _ = AnimateVerificationCardAsync();
                    }
                }
            };
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
            // Reset initial states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            RegisterFormCard.Opacity = 0;
            RegisterFormCard.TranslationY = 50;

            // Animate background circles
            AnimateBackgroundCircles();

            // Animate header
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(150);

            // Animate form card
            await Task.WhenAll(
                RegisterFormCard.FadeTo(1, 700, Easing.CubicOut),
                RegisterFormCard.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private async Task AnimateVerificationCardAsync()
        {
            // Kayýt formunu gizle
            await RegisterFormCard.FadeTo(0, 300);

            // Doðrulama kartýný göster
            VerificationCard.Opacity = 0;
            VerificationCard.TranslationY = 50;
            VerificationCard.Scale = 0.9;

            await Task.WhenAll(
                VerificationCard.FadeTo(1, 600, Easing.CubicOut),
                VerificationCard.TranslateTo(0, 0, 600, Easing.CubicOut),
                VerificationCard.ScaleTo(1, 600, Easing.CubicOut)
            );

            // Küçük bir titreme efekti
            await VerificationCard.ScaleTo(1.05, 150, Easing.CubicOut);
            await VerificationCard.ScaleTo(1, 150, Easing.CubicOut);
        }

        private void AnimateBackgroundCircles()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Task.WhenAll(
                                Circle1.RotateTo(360, 25000, Easing.Linear),
                                Circle2.RotateTo(-360, 20000, Easing.Linear)
                            );

                            Circle1.Rotation = 0;
                            Circle2.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }
    }
}
