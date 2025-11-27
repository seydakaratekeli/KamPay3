using KamPay.ViewModels;

namespace KamPay.Views;

public partial class EditProductPage : ContentPage
{
    public EditProductPage(EditProductViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}