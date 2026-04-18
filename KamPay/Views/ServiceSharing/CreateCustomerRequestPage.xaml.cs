using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class CreateCustomerRequestPage : ContentPage
    {
        public CreateCustomerRequestPage(CreateCustomerRequestViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
