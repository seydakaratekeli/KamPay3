using KamPay.ViewModels;

namespace KamPay.Views;

public partial class PriceQuotesPage : ContentPage
{
    public PriceQuotesPage(PriceQuotesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is PriceQuotesViewModel vm)
        {
            await vm.LoadQuotesCommand.ExecuteAsync(null);
        }
    }

    private void OnReceivedTabTapped(object sender, EventArgs e)
    {
        ReceivedQuotesView.IsVisible = true;
        SentQuotesView.IsVisible = false;
        if (BindingContext is PriceQuotesViewModel vm)
        {
            vm.SelectedTabIndex = 0;
        }
    }

    private void OnSentTabTapped(object sender, EventArgs e)
    {
        ReceivedQuotesView.IsVisible = false;
        SentQuotesView.IsVisible = true;
        if (BindingContext is PriceQuotesViewModel vm)
        {
            vm.SelectedTabIndex = 1;
        }
    }
}
