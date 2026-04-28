using KamPay.ViewModels;

namespace KamPay.Views;

public partial class ServiceSharingPage : ContentPage
{
    private readonly ServiceSharingViewModel _viewModel;
    private bool _hasAnimated = false;
    private CancellationTokenSource? _animationCts;

    public ServiceSharingPage(ServiceSharingViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { Circle1?.AbortAnimation("CircleRotation"); } catch { }
    }

    private async Task AnimatePageAsync()
    {
        if (HeaderSection == null || FilterSection == null || Circle1 == null) return;

        // Reset states
        HeaderSection.Opacity = 0;
        HeaderSection.TranslationY = -30;
        FilterSection.Opacity = 0;
        FilterSection.TranslationY = 30;

        // Background animation
        AnimateBackgroundCircle();

        // Header animation
        await Task.WhenAll(
            HeaderSection.FadeTo(1, 600, Easing.CubicOut),
            HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
        );

        await Task.Delay(150);

        // Filter section animation
        await Task.WhenAll(
            FilterSection.FadeTo(1, 700, Easing.CubicOut),
            FilterSection.TranslateTo(0, 0, 700, Easing.CubicOut)
        );
    }

    private void AnimateBackgroundCircle()
    {
        var animation = new Animation(v => Circle1.Rotation = v, 0, 360);
        animation.Commit(
            owner: Circle1,
            name: "CircleRotation",
            length: 30000,
            easing: Easing.Linear,
            repeat: () => true
        );
    }
}