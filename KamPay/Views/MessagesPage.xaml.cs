using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class MessagesPage : ContentPage
    {
        private readonly MessagesViewModel _viewModel;

        public MessagesPage(MessagesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        // 🔥 Sayfa her göründüğünde çağrılır
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ViewModel'in initialize metodunu çağır
            await _viewModel.InitializeAsync();
        }

        // 🔥 Sayfa kaybolduğunda listener'ları temizle
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Dispose otomatik çağrılır, ekstra birşey yapma

        }
    }
}

