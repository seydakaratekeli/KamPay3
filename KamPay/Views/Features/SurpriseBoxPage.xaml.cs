using KamPay.ViewModels;

namespace KamPay.Views;

public partial class SurpriseBoxPage : ContentPage
{
    private readonly SurpriseBoxViewModel _viewModel;
    private bool _hasAnimated = false;
    private CancellationTokenSource? _animationCts;

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
        else
        {
            _animationCts = new CancellationTokenSource();
          //  AnimateBackgroundCircles(_animationCts.Token);
        }
    }

    private async Task AnimatePageAsync()
    {
        if (HeaderSection == null || ContentSection == null) return;

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
        var anim1 = new Animation(v => Circle1.Rotation = v, 0, 360);
        anim1.Commit(owner: Circle1, name: "Circle1Rotation", length: 35000, easing: Easing.Linear, repeat: () => true);

        var anim2 = new Animation(v => Circle2.Rotation = v, 0, -360);
        anim2.Commit(owner: Circle2, name: "Circle2Rotation", length: 25000, easing: Easing.Linear, repeat: () => true);
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
        
        Circle1.AbortAnimation("Circle1Rotation");
        Circle2.AbortAnimation("Circle2Rotation");

        _viewModel.RedemptionCompleted -= OnRedemptionCompleted;
    }
}