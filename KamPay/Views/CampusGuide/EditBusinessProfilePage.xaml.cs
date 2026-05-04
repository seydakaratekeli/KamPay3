using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class EditBusinessProfilePage : ContentPage
{
    public EditBusinessProfilePage(EditBusinessProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
