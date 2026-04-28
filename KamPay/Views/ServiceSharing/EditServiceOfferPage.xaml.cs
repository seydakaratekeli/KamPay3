using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class EditServiceOfferPage : ContentPage
    {
        public EditServiceOfferPage(EditServiceOfferViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
