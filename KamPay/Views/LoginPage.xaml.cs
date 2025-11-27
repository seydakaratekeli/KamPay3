using KamPay.ViewModels;

namespace KamPay.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Sayfa her göründüðünde ViewModel'deki metodu çaðýrarak
        // email ve þifre kutularýný temizle.
        if (BindingContext is LoginViewModel vm)
        {
            vm.ClearCredentials();
        }
    }
}