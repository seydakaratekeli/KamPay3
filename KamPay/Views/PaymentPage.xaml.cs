using KamPay.ViewModels;

namespace KamPay.Views;

public partial class PaymentPage : ContentPage
{
    public PaymentPage(PaymentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}