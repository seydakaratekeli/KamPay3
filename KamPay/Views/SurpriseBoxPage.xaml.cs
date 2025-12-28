using KamPay.ViewModels;

namespace KamPay.Views;

public partial class SurpriseBoxPage : ContentPage
{
    private readonly SurpriseBoxViewModel _viewModel;
    private bool _hasAnimated = false;

    public SurpriseBoxPage(SurpriseBoxViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;

        _viewModel.RedemptionCompleted += OnRedemptionCompleted;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Puanları yenile
        await _viewModel.RefreshAsync();

        // Animasyon
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
        ContentSection.Opacity = 0;
        ContentSection.TranslationY = 50;

        // Background animation
        AnimateBackgroundCircles();

        // Header animation
        await Task.WhenAll(
            HeaderSection.FadeTo(1, 600, Easing.CubicOut),
            HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
        );

        await Task.Delay(150);

        // Content animation
        await Task.WhenAll(
            ContentSection.FadeTo(1, 700, Easing.CubicOut),
            ContentSection.TranslateTo(0, 0, 700, Easing.CubicOut)
        );
    }

    private void AnimateBackgroundCircles()
    {
        // Circle 1 - Slow rotation
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Circle1.RotateTo(360, 35000, Easing.Linear);
                        Circle1.Rotation = 0;
                    });
                }
                catch
                {
                    break;
                }
            }
        });

        // Circle 2 - Faster rotation (opposite direction)
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Circle2.RotateTo(-360, 25000, Easing.Linear);
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

    private async void OnRedemptionCompleted(object? sender, bool success)
    {
        if (success)
        {
            // 🎁 Enhanced Gift Box Animation
            await BoxImage.ScaleTo(1.3, 150, Easing.CubicIn);
            
            // Shake animation
            await BoxImage.RotateTo(-20, 80);
            await BoxImage.RotateTo(20, 160);
            await BoxImage.RotateTo(-15, 120);
            await BoxImage.RotateTo(15, 120);
            await BoxImage.RotateTo(-10, 100);
            await BoxImage.RotateTo(10, 100);
            await BoxImage.RotateTo(0, 80);
            
            // Scale back with bounce
            await BoxImage.ScaleTo(1, 200, Easing.BounceOut);

            // Show result with fade and scale animation
            ResultFrame.IsVisible = true;
            ResultFrame.Scale = 0.8;
            ResultFrame.Opacity = 0;
            
            await Task.WhenAll(
                ResultFrame.FadeTo(1, 500, Easing.CubicOut),
                ResultFrame.ScaleTo(1, 500, Easing.CubicOut)
            );
        }
    }

    private async void CloseResult_Clicked(object? sender, EventArgs e)
    {
        // Sonuç frame'ini kapat (fade out with scale)
        await Task.WhenAll(
            ResultFrame.FadeTo(0, 300, Easing.CubicIn),
            ResultFrame.ScaleTo(0.8, 300, Easing.CubicIn)
        );
        
        ResultFrame.IsVisible = false;

        // Kazanılan ürünün detay sayfasına git
        if (_viewModel.RedemptionResult != null)
        {
            await Shell.Current.GoToAsync($"ProductDetailPage?productId={_viewModel.RedemptionResult.ProductId}");
        }

        _viewModel.ResetCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.RedemptionCompleted -= OnRedemptionCompleted;
    }
}