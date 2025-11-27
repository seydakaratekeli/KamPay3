using KamPay.ViewModels;
using Microsoft.Maui.Controls;

namespace KamPay.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage(RegisterViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
