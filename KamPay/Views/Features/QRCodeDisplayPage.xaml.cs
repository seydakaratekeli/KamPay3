using KamPay.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace KamPay.Views;

public partial class QRCodeDisplayPage : ContentPage
{
    public QRCodeDisplayPage(QRCodeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Giriþ animasyonlarýný baþlat
        await StartEntranceAnimations();
        
        // Background circle animasyonlarýný baþlat
        StartBackgroundAnimations();
    }

    private async Task StartEntranceAnimations()
    {
        // Tüm ana elementleri fade-in ile göster
        var elements = this.GetVisualTreeDescendants()
            .OfType<Border>()
            .Where(b => b.Parent is VerticalStackLayout)
            .ToList();

        foreach (var element in elements)
        {
            element.Opacity = 0;
            element.TranslationY = 20;
        }

        foreach (var element in elements)
        {
            var fadeIn = element.FadeTo(1, 300, Easing.CubicOut);
            var slideUp = element.TranslateTo(0, 0, 300, Easing.CubicOut);
            await Task.WhenAll(fadeIn, slideUp);
            await Task.Delay(50);
        }
    }

    private async void StartBackgroundAnimations()
    {
        // Circle animasyonlarý
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Circle1 != null)
                    {
                        var rotate = Circle1.RotateTo(360, 20000, Easing.Linear);
                        var scale = ScaleCircle(Circle1);
                        await Task.WhenAll(rotate, scale);
                        Circle1.Rotation = 0;
                    }
                });

                if (!IsLoaded) break;
            }
        });

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Circle2 != null)
                    {
                        var rotate = Circle2.RotateTo(-360, 25000, Easing.Linear);
                        var scale = ScaleCircle(Circle2);
                        await Task.WhenAll(rotate, scale);
                        Circle2.Rotation = 0;
                    }
                });

                if (!IsLoaded) break;
            }
        });
    }

    private async Task ScaleCircle(Ellipse circle)
    {
        await circle.ScaleTo(1.2, 8000, Easing.SinInOut);
        await circle.ScaleTo(1.0, 8000, Easing.SinInOut);
    }
}