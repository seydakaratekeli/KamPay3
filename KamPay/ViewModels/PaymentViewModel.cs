using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using KamPay.Resources;
using System.Linq;

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
        [ObservableProperty] private string _simulatedOtp; // OTP to display to user for simulation

        private static LocalizationResourceManager Res => LocalizationResourceManager.Instance;

        public string PageTitle => "Ödeme";

        public PaymentViewModel(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        [RelayCommand]
        private async Task StartPaymentAsync(string method) // "cardsim" veya "banktransfersim"
        {
            // 1. Ağ Kontrolü
            if (!NetworkHelper.HasInternetConnection())
            {
                await Shell.Current.DisplayAlert(Res["Error"], "Ödeme için internet bağlantısı gereklidir.", Res["Ok"]);
                return;
            }

            // 2. Önceki seçimleri temizle
            IsCardSelected = false;
            IsEftSelected = false;
            SimulatedOtp = null;
            OtpCode = string.Empty;

            try
            {
                IsLoading = true;

                // 3. Ödemeyi Başlat (Rate Limiting servis içinde kontrol ediliyor)
                var result = await _transactionService.CreatePaymentSimulationAsync(Transaction.TransactionId, method);

                if (result.Success)
                {
                    PaymentDetails = result.Data;
                    IsCardSelected = method == "cardsim";
                    IsEftSelected = method == "banktransfersim";

                    if (IsCardSelected)
                    {
                        // Kart ödemesi için OTP'yi servisden al ve göster (SADECE SİMÜLASYON)
                        var otpResult = await _transactionService.GetSimulationOtpAsync(PaymentDetails.PaymentId);
                        
                        if (otpResult.Success && !string.IsNullOrEmpty(otpResult.Data))
                        {
                            SimulatedOtp = otpResult.Data;
                            await Shell.Current.DisplayAlert("Doğrulama Kodu (Simülasyon)", 
                                $"Simülasyon için OTP kodunuz:\n\n{SimulatedOtp}\n\nBu kodu aşağıdaki alana girin.", "Tamam");
                        }
                    }
                    else if (IsEftSelected)
                    {
                        await Shell.Current.DisplayAlert("EFT/Havale Bilgileri",
                            $"Banka: {PaymentDetails.BankName}\n" +
                            $"Referans Kodu: {PaymentDetails.BankReference}\n" +
                            $"Tutar: {PaymentDetails.Amount:N2} {PaymentDetails.Currency}\n\n" +
                            $"Ödeme açıklamasına mutlaka bu referans kodunu yazınız.", "Tamam");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert(Res["Error"], result.Message, Res["Ok"]);
                }
            }
            catch (Exception ex)
            {
                var error = NetworkHelper.GetUserFriendlyErrorMessage(ex);
                await Shell.Current.DisplayAlert(Res["Error"], error, Res["Ok"]);
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task ConfirmCardPaymentAsync()
        {
            // 1. Validasyonlar
            if (IsCardSelected)
            {
                if (string.IsNullOrWhiteSpace(OtpCode))
                {
                    await Shell.Current.DisplayAlert(Res["Error"], "Lütfen doğrulama kodunu giriniz.", Res["Ok"]);
                    return;
                }

                if (OtpCode.Length != 6 || !OtpCode.All(char.IsDigit))
                {
                    await Shell.Current.DisplayAlert(Res["Error"], "Doğrulama kodu 6 haneli bir sayı olmalıdır.", Res["Ok"]);
                    return;
                }
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
                    var successMessage = IsCardSelected 
                        ? "Kredi kartı ödemesi başarıyla onaylandı!" 
                        : "Havale/EFT ödemesi kaydedildi!";
                    
                    await Shell.Current.DisplayAlert(Res["Success"], successMessage, Res["Ok"]);

                    // 4. NAVIGATE BACK MANTIĞI:
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
                // 5. Ağ hatası veya beklenmedik hatalarda kullanıcı dostu mesaj ver
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