using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ServiceRequestsPage : ContentPage
    {
        private readonly ServiceRequestsViewModel _viewModel;
        private bool _isFirstLoad = true; // 🔥 YENİ: İlk yüklenme kontrolü

        public ServiceRequestsPage(ServiceRequestsViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
        }

        // ✅ Picker event handler - güvenli null check
        private void OnPaymentMethodSelected(object sender, EventArgs e)
        {
            if (sender is Picker picker &&
                picker.SelectedItem is ServiceRequestsViewModel.PaymentOption option)
            {
                if (_viewModel != null)
                {
                    _viewModel.SelectedPaymentMethod = option.Method;
                    System.Diagnostics.Debug.WriteLine($"💳 Ödeme yöntemi seçildi: {option.DisplayName}");
                }
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // 🔥 Sadece ilk kez yükle, sonraki gelişlerde real-time listener zaten çalışıyor
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                System.Diagnostics.Debug.WriteLine("✅ ServiceRequestsPage: İlk yükleme (Real-time listener aktif)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✅ ServiceRequestsPage: Cache'den gösterildi (Listener zaten aktif)");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // 🔥 Dispose ETME - Listener çalışmaya devam etsin
            System.Diagnostics.Debug.WriteLine("⏸️ ServiceRequestsPage: Arka plana alındı (Listener aktif)");
        }

        // 🔥 Sayfa bellekten tamamen kaldırılınca otomatik çağrılır
        ~ServiceRequestsPage()
        {
            _viewModel?.Dispose();
            System.Diagnostics.Debug.WriteLine("🗑️ ServiceRequestsPage: Dispose edildi");
        }
    }
}