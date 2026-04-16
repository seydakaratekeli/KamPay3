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
            // UI Thread animasyon için temiz bırakılıyor
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
                System.Diagnostics.Debug.WriteLine($"❌ GoodDeedBoardPage OnAppearing hatası: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Hata", "Sayfa yüklenirken bir hata oluştu.", "Tamam");
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
        // Sayfa gizlendiğinde sadece dinleyicileri durdur, Dispose çağırma.
        // Bu sayede sayfa tekrar göründüğünde listener'lar yeniden başlatılabilir.
        if (_viewModel != null)
        {
            _viewModel.StopListening();
        }
    }
}