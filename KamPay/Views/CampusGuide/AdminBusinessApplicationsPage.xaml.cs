using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class AdminBusinessApplicationsPage : ContentPage
{
    private readonly AdminBusinessApplicationsViewModel _viewModel;

    public AdminBusinessApplicationsPage(AdminBusinessApplicationsViewModel viewModel)
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
