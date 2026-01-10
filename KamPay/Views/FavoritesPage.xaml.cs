using KamPay.ViewModels;

namespace KamPay.Views;

public partial class FavoritesPage : ContentPage
{
    private readonly FavoritesViewModel _viewModel;
    private bool _isFirstLoad = true; // : İlk yüklenme kontrolü
    private bool _hasAnimated = false;

    public FavoritesPage(FavoritesViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    //  Sayfa her göründüğünde çağrılır
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        //  Sadece ilk kez yükle, sonraki gelişlerde real-time listener zaten çalışıyor
        if (_isFirstLoad)
        {
            await _viewModel.InitializeAsync();
            _isFirstLoad = false;
            System.Diagnostics.Debug.WriteLine("✅ FavoritesPage: İlk yükleme (Real-time listener aktif)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("✅ FavoritesPage: Cache'den gösterildi (Listener zaten aktif)");
        }

        // Animasyonları çalıştır
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
        ContentSection.TranslationY = 40;

        // Background animation
        AnimateBackgroundCircle();

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

    //  Sayfa bellekten tamamen kaldırılınca otomatik çağrılır
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // ✅ Dispose ViewModel to cleanup listeners
        if (BindingContext is FavoritesViewModel vm)
        {
            vm.Dispose();
        }
        
        System.Diagnostics.Debug.WriteLine("✅ FavoritesPage: Listener cleanup yapıldı");
    }
}