using KamPay.ViewModels;

namespace KamPay.Views
{
    public partial class ServiceRequestsPage : ContentPage
    {
        private readonly ServiceRequestsViewModel _viewModel;
        private bool _isFirstLoad = true; // / İlk yüklenme kontrolü
        private bool _hasAnimated = false;

        public ServiceRequestsPage(ServiceRequestsViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
        }

        //  Picker event handler - güvenli null check
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Animasyon
            if (!_hasAnimated)
            {
                _hasAnimated = true;
                await Task.Delay(100);
                await AnimatePageAsync();
            }

            //  Sadece ilk kez yükle, sonraki gelişlerde real-time listener zaten çalışıyor
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

        private async Task AnimatePageAsync()
        {
            // Reset states
            HeaderSection.Opacity = 0;
            HeaderSection.TranslationY = -30;
            TabSection.Opacity = 0;
            TabSection.TranslationY = -20;
            ContentSection.Opacity = 0;
            ContentSection.TranslationY = 50;

            // Background animation
            AnimateBackgroundCircle();

            // Header animation
            await Task.WhenAll(
                HeaderSection.FadeTo(1, 600, Easing.CubicOut),
                HeaderSection.TranslateTo(0, 0, 600, Easing.CubicOut)
            );

            await Task.Delay(100);

            // Tab section animation
            await Task.WhenAll(
                TabSection.FadeTo(1, 500, Easing.CubicOut),
                TabSection.TranslateTo(0, 0, 500, Easing.CubicOut)
            );

            await Task.Delay(100);

            // Content animation
            await Task.WhenAll(
                ContentSection.FadeTo(1, 700, Easing.CubicOut),
                ContentSection.TranslateTo(0, 0, 700, Easing.CubicOut)
            );
        }

        private void AnimateBackgroundCircle()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Circle1.RotateTo(360, 28000, Easing.Linear);
                            Circle1.Rotation = 0;
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            //  Dispose ETME - Listener çalışmaya devam etsin
            System.Diagnostics.Debug.WriteLine("⏸️ ServiceRequestsPage: Arka plana alındı (Listener aktif)");
        }

        //  Sayfa bellekten tamamen kaldırılınca otomatik çağrılır
        ~ServiceRequestsPage()
        {
            _viewModel?.Dispose();
            System.Diagnostics.Debug.WriteLine("🗑️ ServiceRequestsPage: Dispose edildi");
        }
    }
}