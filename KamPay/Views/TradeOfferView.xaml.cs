using CommunityToolkit.Maui.Views;
using KamPay.ViewModels;

namespace KamPay.Views;

public partial class TradeOfferView : Popup
{
    public TradeOfferView(TradeOfferViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Pop-up'ýn sonucunu ViewModel'e bildirmek için
     //   vm.ClosePopupAction = async () => await CloseAsync();
    }
}