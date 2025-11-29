// KamPay/Views/GoodDeedBoardPage.xaml.cs
using KamPay.ViewModels;

namespace KamPay.Views;

public partial class GoodDeedBoardPage : ContentPage
{
    public GoodDeedBoardPage(GoodDeedBoardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Sayfa her görüntülendiğinde, ViewModel'deki dinleyiciyi güvenli şekilde başlat.
        if (BindingContext is GoodDeedBoardViewModel vm)
        {
            vm.StartListeningForPosts();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Sayfa gizlendiğinde sadece dinleyicileri durdur, Dispose çağırma.
        // Bu sayede sayfa tekrar göründüğünde listener'lar yeniden başlatılabilir.
        if (BindingContext is GoodDeedBoardViewModel vm)
        {
            vm.StopListening();
        }
    }
}