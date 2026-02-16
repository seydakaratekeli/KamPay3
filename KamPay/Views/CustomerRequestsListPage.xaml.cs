using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class CustomerRequestsListPage : ContentPage
    {
        public CustomerRequestsListPage(CustomerRequestsListViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
