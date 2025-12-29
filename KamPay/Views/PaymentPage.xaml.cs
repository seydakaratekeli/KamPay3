using KamPay.ViewModels;

namespace KamPay.Views;

public partial class PaymentPage : ContentPage
{
    public PaymentPage(PaymentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Giriþ animasyonlarýný baþlat
        await StartEntranceAnimations();
        
        // Background circle animasyonunu baþlat
        StartBackgroundAnimation();
    }

    private async Task StartEntranceAnimations()
    {
        // Tüm elementleri fade-in ile göster
        MainContent.Opacity = 0;
        MainContent.TranslationY = 30;

        await Task.WhenAll(
            MainContent.FadeTo(1, 500, Easing.CubicOut),
            MainContent.TranslateTo(0, 0, 500, Easing.CubicOut)
        );
    }

    private async void StartBackgroundAnimation()
    {
        while (true)
        {
            if (Circle1 != null)
            {
                await Circle1.RotateTo(360, 30000, Easing.Linear);
                Circle1.Rotation = 0;
            }

            if (!IsLoaded) break;
        }
    }
}