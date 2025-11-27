using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfileViewModel _viewModel;

        public ProfilePage(ProfileViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        // 🔥 Sayfa her göründüğünde SADECE cache kontrolü yap
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // InitializeAsync cache kontrolü yapar, gerekirse yükler
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Dispose etme - cache'i koruyalım
            System.Diagnostics.Debug.WriteLine("⏸️ ProfilePage: Arka plana alındı");
        }
    }
}