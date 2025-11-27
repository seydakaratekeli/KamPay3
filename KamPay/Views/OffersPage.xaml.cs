using KamPay.ViewModels;
using System;

namespace KamPay.Views
{
    public partial class OffersPage : ContentPage
    {
        private readonly OffersViewModel _viewModel;

        public OffersPage(OffersViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
        }

        // ❌ YANLIŞ: OnDisappearing'de Dispose ÇAĞIRMAYIN!
        // Sebep: Geri dönünce listener yok oluyor ve yeniden başlıyor.

        // ✅ DOĞRU: Sadece sayfa tamamen bellekten kaldırılınca dispose et
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // 🔥 BURADA DİSPOSE ETME! Sadece log at
            System.Diagnostics.Debug.WriteLine("⏸️ OffersPage: Arka plana alındı (Listener DEVAM EDİYOR)");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("✅ OffersPage: Aktif (Listener zaten çalışıyor)");
        }

        // 🔥 Sayfa bellekten tamamen kaldırılınca otomatik çağrılır
        ~OffersPage()
        {
            _viewModel?.Dispose();
            System.Diagnostics.Debug.WriteLine("🗑️ OffersPage: Bellekten kaldırıldı, ViewModel dispose edildi");
        }
    }
}