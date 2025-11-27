using KamPay.ViewModels;

namespace KamPay.Views;

public partial class AddProductPage : ContentPage
{
    private readonly AddProductViewModel _viewModel;

    public AddProductPage(AddProductViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // 🔥 Sayfa açıldığında kategorileri yükle
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Kategoriler cache'de yoksa yükle
        await _viewModel.LoadCategoriesCommand.ExecuteAsync(null);
    }
}