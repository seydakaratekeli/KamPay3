using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ServiceOfferDetailPage : ContentPage
    {
        public ServiceOfferDetailPage(ServiceOfferDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
