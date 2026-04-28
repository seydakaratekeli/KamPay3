using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class GoodDeedPostDetailPage : ContentPage
    {
        public GoodDeedPostDetailPage(GoodDeedPostDetailViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
