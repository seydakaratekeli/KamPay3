using KamPay.ViewModels;

namespace KamPay.Views;

public partial class SurpriseBoxPage : ContentPage
{
    private readonly SurpriseBoxViewModel _viewModel;

    public SurpriseBoxPage(SurpriseBoxViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;

        _viewModel.RedemptionCompleted += OnRedemptionCompleted;
    }

    // 🔥 Sayfa her göründüğünde puanları yenile
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }

    private async void OnRedemptionCompleted(object sender, bool success)
    {
        if (success)
        {
            // 🔥 Kutu animasyonu
            await BoxImage.ScaleTo(1.2, 100);
            await BoxImage.RotateTo(-15, 50);
            await BoxImage.RotateTo(15, 100);
            await BoxImage.RotateTo(-10, 100);
            await BoxImage.RotateTo(10, 100);
            await BoxImage.RotateTo(0, 100);
            await BoxImage.ScaleTo(1, 100);

            // 🔥 Sonuçları göster (fade in)
            ResultFrame.IsVisible = true;
            await ResultFrame.FadeTo(1, 500, Easing.CubicOut);
        }
    }

    private async void CloseResult_Clicked(object sender, EventArgs e)
    {
        // 🔥 Sonuç frame'ini kapat (fade out)
        await ResultFrame.FadeTo(0, 300);
        ResultFrame.IsVisible = false;

        // 🔥 Kazanılan ürünün detay sayfasına git
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