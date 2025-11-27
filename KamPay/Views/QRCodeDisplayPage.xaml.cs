using KamPay.ViewModels;

namespace KamPay.Views;

public partial class QRCodeDisplayPage : ContentPage
{
    public QRCodeDisplayPage(QRCodeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}