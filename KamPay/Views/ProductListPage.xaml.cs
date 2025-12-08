using KamPay.ViewModels;
using KamPay.Services; // LocalizationResourceManager için

namespace KamPay.Views;

public partial class ProductListPage : ContentPage
{
    private readonly ProductListViewModel _viewModel;

    public ProductListPage(ProductListViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    ~ProductListPage()
    {
        _viewModel?.Dispose();
    }
}