using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class BusinessDashboardPage : ContentPage
{
    private readonly BusinessDashboardViewModel _viewModel;

    public BusinessDashboardPage(BusinessDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
