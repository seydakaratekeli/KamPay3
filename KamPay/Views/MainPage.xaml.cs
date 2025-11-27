using KamPay.ViewModels;

namespace KamPay.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage()
    {
        InitializeComponent();
        _viewModel = App.Current.Handler.MauiContext.Services.GetService<MainViewModel>();
        BindingContext = _viewModel;
    }

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
