using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class CustomerRequestDetailsPage : ContentPage
    {
        public CustomerRequestDetailsPage(CustomerRequestDetailsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
