// KamPay/Views/GoodDeedBoardPage.xaml.cs
using KamPay.ViewModels;
using System.Diagnostics;

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
            try
            {
                vm.StartListeningForPosts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GoodDeedBoardPage OnAppearing hatası: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Hata", "Sayfa yüklenirken bir hata oluştu.", "Tamam");
                });
            }
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