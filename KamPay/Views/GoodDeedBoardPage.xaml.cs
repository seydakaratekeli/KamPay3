// KamPay/Views/GoodDeedBoardPage.xaml.cs
using KamPay.ViewModels;
using System.Diagnostics;

namespace KamPay.Views;

public partial class GoodDeedBoardPage : ContentPage
{
    private readonly GoodDeedBoardViewModel _viewModel;
    private bool _hasAnimated = false;

    public GoodDeedBoardPage(GoodDeedBoardViewModel vm)
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
            // UI Thread animasyon iÃ§in temiz bÄ±rakÄ±lÄ±yor
            await Task.Delay(350);
            await AnimatePageAsync();
            SafeStartListening();
        }
        else
        {
            SafeStartListening();
        }
    }

    private async void SafeStartListening()
    {
        if (_viewModel != null)
        {
            try
            {
                _viewModel.StartListeningForPosts();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GoodDeedBoardPage OnAppearing hatasÄ±: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Hata", "Sayfa yÃ¼klenirken bir hata oluÅŸtu.", "Tamam");
                });
            }
        }
    }

    private async Task AnimatePageAsync()
    {
        // Reset states
        HeaderSection.Opacity = 0;
        HeaderSection.TranslationY = -30;

        // Background animation
        AnimateBackgroundCircle();

        // Header animation
        await Task.WhenAll(
            HeaderSection.FadeTo(1, 600, Easing.CubicOut),
            HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
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
                        await Circle1.RotateTo(360, 25000, Easing.Linear);
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Sayfa gizlendiÄŸinde sadece dinleyicileri durdur, Dispose Ã§aÄŸÄ±rma.
        // Bu sayede sayfa tekrar gÃ¶rÃ¼ndÃ¼ÄŸÃ¼nde listener'lar yeniden baÅŸlatÄ±labilir.
        if (_viewModel != null)
        {
            _viewModel.StopListening();
        }
    }
}
