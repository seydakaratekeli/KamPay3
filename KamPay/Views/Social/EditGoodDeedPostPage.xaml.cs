using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class EditGoodDeedPostPage : ContentPage
    {
        public EditGoodDeedPostPage(EditGoodDeedPostViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
