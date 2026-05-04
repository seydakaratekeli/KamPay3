using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class BusinessRegistrationPage : ContentPage
{
    public BusinessRegistrationPage(BusinessRegistrationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
