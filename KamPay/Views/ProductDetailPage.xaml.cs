using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.ViewModels;
using System; // Bu using ifadesini ekleyin

namespace KamPay.Views;

public partial class ProductDetailPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;

    public ProductDetailPage(ProductDetailViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _serviceProvider = serviceProvider; // DI Container'ý alýyoruz

        WeakReferenceMessenger.Default.Register<ShowTradeOfferPopupMessage>(this, async (r, m) =>
        {
            // --- DÜZELTÝLMÝÞ KISIM ---
            // Artýk 'new' anahtar kelimesiyle nesne oluþturmuyoruz.
            // Bunun yerine, DI container'dan bir 'TradeOfferView' örneði talep ediyoruz.
            var tradePopup = _serviceProvider.GetRequiredService<TradeOfferView>();

            // Popup'ýn ViewModel'ine eriþip, takas yapýlacak ürünün ID'sini atýyoruz.
            if (tradePopup.BindingContext is TradeOfferViewModel vm)
            {
                vm.ProductId = m.TargetProduct.ProductId;
            }

            // Popup'ý kullanýcýya gösteriyoruz.
            await this.ShowPopupAsync(tradePopup);
        });
    }

    // Sayfadan ayrýlýrken (örneðin geri tuþuna basýldýðýnda) mesaj dinleyicisini kapatýyoruz.
    // Bu, bellek sýzýntýlarýný (memory leaks) önlemek için kritik bir adýmdýr.
    // protected override void OnDisappearing()
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<ShowTradeOfferPopupMessage>(this);
    }
}