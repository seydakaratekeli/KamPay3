using KamPay.ViewModels;

namespace KamPay.Views;

public partial class FavoritesPage : ContentPage
{
    private readonly FavoritesViewModel _viewModel;
    private bool _isFirstLoad = true; // 🔥 YENİ: İlk yüklenme kontrolü

    public FavoritesPage(FavoritesViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    // 🔥 Sayfa her göründüğünde çağrılır
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 🔥 Sadece ilk kez yükle, sonraki gelişlerde real-time listener zaten çalışıyor
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
    }

    // 🔥 DÜZELTİLDİ: base.OnDisappearing() çağrısı
    protected override void OnDisappearing()
    {
        base.OnDisappearing(); // 🔥 DOĞRU METHOD!
        // 🔥 Dispose ETME - Listener çalışmaya devam etsin
        System.Diagnostics.Debug.WriteLine("⏸️ FavoritesPage: Arka plana alındı (Listener aktif)");
    }

    // 🔥 Sayfa bellekten tamamen kaldırılınca otomatik çağrılır
    ~FavoritesPage()
    {
        _viewModel?.Dispose();
        System.Diagnostics.Debug.WriteLine("🗑️ FavoritesPage: Dispose edildi");
    }
}