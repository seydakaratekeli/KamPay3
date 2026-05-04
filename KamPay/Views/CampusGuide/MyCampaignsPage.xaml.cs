using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class MyCampaignsPage : ContentPage
{
    private readonly MyCampaignsViewModel _viewModel;

    public MyCampaignsPage(MyCampaignsViewModel viewModel)
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
