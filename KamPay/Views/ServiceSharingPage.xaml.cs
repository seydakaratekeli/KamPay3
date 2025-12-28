using KamPay.ViewModels;

namespace KamPay.Views;

public partial class ServiceSharingPage : ContentPage
{
    private readonly ServiceSharingViewModel _viewModel;
    private bool _hasAnimated = false;

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

    private async Task AnimatePageAsync()
    {
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
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Circle1.RotateTo(360, 30000, Easing.Linear);
                        Circle1.Rotation = 0;
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