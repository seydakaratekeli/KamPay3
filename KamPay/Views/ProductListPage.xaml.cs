using KamPay.ViewModels;

namespace KamPay.Views;

public partial class ProductListPage : ContentPage
{
    private bool _isInitialized = false;

    public ProductListPage(ProductListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ProductListViewModel vm)
        {
            // Bu kontrol, sayfa ilk oluþturulduðunda metodun iki kez
            // (hem constructor hem de OnAppearing tarafýndan) çaðrýlmasýný önler.
            // Sayfaya geri dönüldüðünde ise dinleyiciyi yeniden baþlatýr.
            if (_isInitialized)
            {
                vm.InitializeViewModel();
            }
            _isInitialized = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    // Varsa bu metodu silebilir veya tutabilirsiniz, artýk gerekli deðil.
    // private async void OnBackClicked(object sender, EventArgs e)
    // {
    //     await Shell.Current.GoToAsync("..");
    // }
}