using KamPay.ViewModels;

namespace KamPay.Views.CampusGuide;

public partial class BusinessDetailPage : ContentPage
{
    public BusinessDetailPage(BusinessDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
