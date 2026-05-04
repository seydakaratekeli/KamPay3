using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class CampusGuidePage : ContentPage
{
    private readonly CampusGuideViewModel _viewModel;

    public CampusGuidePage(CampusGuideViewModel viewModel)
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
