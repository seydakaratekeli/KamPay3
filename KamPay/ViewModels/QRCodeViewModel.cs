using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firebase.Database.Query;
using KamPay.Models;
using CommunityToolkit.Mvvm.Messaging;
using KamPay.Services;
using KamPay.Models.Messages;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(TransactionId), "transactionId")]
    public partial class QRCodeViewModel : ObservableObject, IRecipient<QRCodeScannedMessage> 
    { 
        private readonly IQRCodeService _qrCodeService;
        private readonly IAuthenticationService _authService;
        private readonly IProductService _productService; // 🔹 YENİ EKLENEN

        private readonly Firebase.Database.FirebaseClient _firebaseClient;

        [ObservableProperty]
        private string transactionId;

        // Kendi ürünümüzün teslimat bilgisi
        [ObservableProperty]
        private DeliveryQRCode? myDelivery;

        // Karşı tarafın ürününün teslimat bilgisi
        [ObservableProperty]
        private DeliveryQRCode? otherUserDelivery;

        [ObservableProperty]
        private Transaction? currentTransaction;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string pageTitle = "Teslimat Onayı";

        [ObservableProperty]
        private string instructionText = "Teslimatı başlatmak için QR kodunuzu diğer kullanıcıya okutun veya onun kodunu tarayın.";

        public QRCodeViewModel(
            IQRCodeService qrCodeService,
            IAuthenticationService authService,
            IProductService productService) // 🔹 YENİ PARAMETRE
        {
            _qrCodeService = qrCodeService;
            _authService = authService;
            _productService = productService; // 🔹 ATAMA
            _firebaseClient = new Firebase.Database.FirebaseClient(Helpers.Constants.FirebaseRealtimeDbUrl);

            WeakReferenceMessenger.Default.Register<QRCodeScannedMessage>(this);
        }

        // Bu metot, WeakReferenceMessenger tarafından bir mesaj geldiğinde OTOMATİK olarak çağrılır
        public async void Receive(QRCodeScannedMessage message)
        {
            // Gelen mesajın içindeki QR kod verisini al ve işle
            await ProcessScannedQRCode(message.Value);
        }

        async partial void OnTransactionIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                await LoadTransactionAndQRCodesAsync();
            }
        }

        public async Task ProcessScannedQRCode(string qrCodeData)
        {
            IsLoading = true;

            if (OtherUserDelivery == null || qrCodeData != OtherUserDelivery.QRCodeData)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Geçersiz veya bu takasa ait olmayan bir QR kod okuttunuz.", "Tamam");
                IsLoading = false;
                return;
            }

            if (OtherUserDelivery.IsUsed)
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", "Bu ürünün teslimatı zaten onaylanmış.", "Tamam");
                IsLoading = false;
                return;
            }

            var result = await _qrCodeService.CompleteDeliveryAsync(OtherUserDelivery.QRCodeId);
            if (result.Success)
            {
                // 🔹 YENİ: Her iki teslimat da tamamlandıysa ürünleri "TAKAS YAPILDI" olarak işaretle
                if (MyDelivery?.IsUsed == true && OtherUserDelivery?.IsUsed == true && CurrentTransaction != null)
                {
                    // Her iki ürünü de takas yapıldı olarak işaretle
                    await _productService.MarkAsExchangedAsync(CurrentTransaction.ProductId);
                    await _productService.MarkAsExchangedAsync(CurrentTransaction.OfferedProductId);
                }

                await Application.Current.MainPage.DisplayAlert("Başarılı",
                    $"'{OtherUserDelivery.ProductTitle}' ürününü teslim aldığınız onaylandı.", "Harika!");

                // Durumu yenilemek için verileri tekrar yükle
                await LoadTransactionAndQRCodesAsync();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
            }

            IsLoading = false;
        }

        private async Task LoadTransactionAndQRCodesAsync()
        {
            IsLoading = true;
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                IsLoading = false;
                await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı bulunamadı.", "Tamam");
                return;
            }

            CurrentTransaction = await _firebaseClient
                .Child("transactions")
                .Child(TransactionId)
                .OnceSingleAsync<Transaction>();

            if (CurrentTransaction == null)
            {
                IsLoading = false;
                await Application.Current.MainPage.DisplayAlert("Hata", "İşlem detayı bulunamadı.", "Tamam");
                return;
            }

            var qrCodesResult = await _qrCodeService.GetQRCodesForTransactionAsync(TransactionId);
            if (!qrCodesResult.Success || qrCodesResult.Data == null)
            {
                IsLoading = false;
                await Application.Current.MainPage.DisplayAlert("Hata", "Teslimat bilgileri alınamadı.", "Tamam");
                return;
            }

            var allCodes = qrCodesResult.Data;

            if (CurrentTransaction.SellerId == currentUser.UserId) // Eğer ben satıcıysam
            {
                MyDelivery = allCodes.FirstOrDefault(c => c.ProductId == CurrentTransaction.ProductId);
                OtherUserDelivery = allCodes.FirstOrDefault(c => c.ProductId == CurrentTransaction.OfferedProductId);
            }
            else // Eğer ben alıcıysam (teklifi yapan)
            {
                MyDelivery = allCodes.FirstOrDefault(c => c.ProductId == CurrentTransaction.OfferedProductId);
                OtherUserDelivery = allCodes.FirstOrDefault(c => c.ProductId == CurrentTransaction.ProductId);
            }

            UpdateUIState();
            IsLoading = false;
        }

        [RelayCommand]
        private async Task ScanQRCodeAsync()
        {
            await Shell.Current.GoToAsync("qrscanner");
        }

        private void UpdateUIState()
        {
            bool myDeliveryCompleted = MyDelivery?.IsUsed ?? false;
            bool otherDeliveryCompleted = OtherUserDelivery?.IsUsed ?? false;

            if (myDeliveryCompleted && (OtherUserDelivery == null || otherDeliveryCompleted))
            {
                PageTitle = "İşlem Tamamlandı!";
                InstructionText = "Puanlarınız eklendi! 3 saniye içinde yönlendirileceksiniz...";

                Task.Run(async () => {
                    await Task.Delay(3000);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Shell.Current.GoToAsync("..")
                    );
                });
            }
            else if (otherDeliveryCompleted)
            {
                PageTitle = "Şimdi Sıra Sizde";
                InstructionText = "Karşı tarafın ürününü teslim aldınız. Şimdi takası tamamlamak için kendi QR kodunuzu diğer kullanıcıya okutun.";
            }
            else if (myDeliveryCompleted)
            {
                PageTitle = "Onay Bekleniyor";
                InstructionText = "Kendi ürününüzü teslim ettiniz. Şimdi karşı tarafın ürününü teslim almak için onun QR kodunu okutun.";
            }
            else
            {
                PageTitle = "Teslimat Onayı";
                InstructionText = "Takası başlatmak için QR kodunuzu diğer kullanıcıya okutun veya onun kodunu tarayın.";
            }
        }
    }
}