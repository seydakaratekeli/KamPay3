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
            // ✅ İlk açılışta listener başlat
            SafeStartListening();
        }
        // ✅ Singleton sayfa: sonraki tab geçişlerinde SafeStartListening çağırma
        // Firebase listener zaten aktif (StartListeningForPosts guard ile korunuyor)
        // Sadece animasyonu yeniden başlat
        else
        {
            AnimateBackgroundCircle();
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
                KamPay.Helpers.AppLogger.DebugLog($"❌ GoodDeedBoardPage OnAppearing hatası: {ex.Message}");
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
        try { Circle1?.AbortAnimation("CircleRotation"); } catch { }
        if (Circle1 == null) return;
        var animation = new Animation(v => Circle1.Rotation = v, 0, 360);
        animation.Commit(
            owner: Circle1,
            name: "CircleRotation",
            length: 25000,
            easing: Easing.Linear,
            repeat: () => true
        );
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // ✅ Singleton sayfa: listener'ı durdurma, aktif kalsın
        // Sadece animasyonu durdur (kaynak tasarrufu)
        try { Circle1?.AbortAnimation("CircleRotation"); } catch { }
    }
}
