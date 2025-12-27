using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using KamPay.Resources;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(Transaction), "Transaction")]
    public partial class PaymentViewModel : ObservableObject
    {
        private readonly ITransactionService _transactionService;

        [ObservableProperty] private Transaction _transaction;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _otpCode;
        [ObservableProperty] private PaymentDto _paymentDetails;
        [ObservableProperty] private bool _isCardSelected;
        [ObservableProperty] private bool _isEftSelected;

        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        public PaymentViewModel(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        [RelayCommand]
        private async Task StartPaymentAsync(string method) // "cardsim" veya "eft"
        {
            // 1. Ağ Kontrolü
            if (!NetworkHelper.HasInternetConnection())
            {
                await Shell.Current.DisplayAlert(Res["Error"], "Ödeme için internet bağlantısı gereklidir.", Res["Ok"]);
                return;
            }

            try
            {
                IsLoading = true;

                // 2. Ödemeyi Başlat (Rate Limiting servis içinde kontrol ediliyor)
                var result = await _transactionService.CreatePaymentSimulationAsync(Transaction.TransactionId, method);

                if (result.Success)
                {
                    PaymentDetails = result.Data;
                    IsCardSelected = method == "cardsim";
                    IsEftSelected = method == "banktransfersim";

                    if (IsEftSelected)
                    {
                        await Shell.Current.DisplayAlert("EFT Bilgileri",
                            $"Banka: {PaymentDetails.BankName}\nReferans: {PaymentDetails.BankReference}\nTutar: {PaymentDetails.Amount} {PaymentDetails.Currency}", "Tamam");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ConfirmCardPaymentAsync()
        {
            // 1. Kart ödemesi seçiliyse OTP zorunluluğunu kontrol et
            if (IsCardSelected && string.IsNullOrWhiteSpace(OtpCode))
            {
                await Shell.Current.DisplayAlert(Res["Error"], "Lütfen doğrulama kodunu giriniz.", Res["Ok"]);
                return;
            }

            // 2. Girdi Temizleme (Sanitization)
            // EFT/Havale durumunda OtpCode boş gelebilir, SanitizeText buna göre güvenli çalışır.
            var sanitizedOtp = string.IsNullOrWhiteSpace(OtpCode) ? null : InputSanitizer.SanitizeText(OtpCode);

            try
            {
                IsLoading = true;

                // 3. Ödeme Onayı ve Otomatik İşlem Tamamlama
                // Bu servis metodu (FirebaseTransactionService içinde):
                // - Kart ise OTP doğrulaması yapar.
                // - Ürün satışı ise ürünü 'Satıldı' olarak işaretleyip işlemi bitirir.
                // - Hizmet ise sadece ödeme durumunu 'Paid' yapar.
                var result = await _transactionService.ConfirmPaymentSimulationAsync(
                    Transaction.TransactionId,
                    PaymentDetails.PaymentId,
                    sanitizedOtp);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert(Res["Success"], "Ödemeniz başarıyla onaylandı.", Res["Ok"]);

                    // 3. NAVIGATE BACK MANTIĞI:
                    // "//" kullanarak mutlak yönlendirme yaparız; böylece navigasyon yığını temizlenir 
                    // ve kullanıcı doğrudan 'Gelen/Giden Teklifler' (OffersPage) ekranına düşer.
                    await Shell.Current.GoToAsync($"//{nameof(Views.OffersPage)}");
                }
                else
                {
                    // Servis tarafından dönen özel hata mesajlarını (örn: "OTP Geçersiz") göster
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                // 4. Ağ hatası veya beklenmedik hatalarda kullanıcı dostu mesaj ver
                var error = NetworkHelper.GetUserFriendlyErrorMessage(ex);
                await Shell.Current.DisplayAlert(Res["Error"], error, Res["Ok"]);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    }