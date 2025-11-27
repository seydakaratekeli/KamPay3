using KamPay.ViewModels;

namespace KamPay.Views;

public partial class ServiceSharingPage : ContentPage
{
    public ServiceSharingPage(ServiceSharingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}