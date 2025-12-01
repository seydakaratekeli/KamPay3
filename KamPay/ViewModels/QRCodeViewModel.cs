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
        private readonly IProductService _productService;
        private readonly IStorageService _storageService;

        private readonly Firebase.Database.FirebaseClient _firebaseClient;
        
        // 📌 Güvenlik sabitleri
        private const int ExtendTimeThresholdMinutes = 15;

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

        // 🔒 Yeni Güvenlik Özellikleri
        [ObservableProperty]
        private string? verificationPin;

        [ObservableProperty]
        private double currentLatitude;

        [ObservableProperty]
        private double currentLongitude;

        [ObservableProperty]
        private string timeRemaining = "";

        [ObservableProperty]
        private bool canExtendTime;

        [ObservableProperty]
        private DeliveryQRCode? currentQRCode;

        // FAZ 2: Fotoğraf özellikleri
        [ObservableProperty]
        private bool photoRequired;

        [ObservableProperty]
        private ImageSource? deliveryPhotoSource;

        [ObservableProperty]
        private bool isPhotoUploaded;

        private IDispatcherTimer? _expirationTimer;

        public QRCodeViewModel(
            IQRCodeService qrCodeService,
            IAuthenticationService authService,
            IProductService productService,
            IStorageService storageService)
        {
            _qrCodeService = qrCodeService;
            _authService = authService;
            _productService = productService;
            _storageService = storageService;
            _firebaseClient = new Firebase.Database.FirebaseClient(Helpers.Constants.FirebaseRealtimeDbUrl);

            WeakReferenceMessenger.Default.Register<QRCodeScannedMessage>(this);
        }

        // Bu metot, WeakReferenceMessenger tarafından bir mesaj geldiğinde OTOMATİK olarak çağrılır
        public async void Receive(QRCodeScannedMessage message)
        {
            // Gelen mesajın içindeki QR kod verisini al ve işle
            await ProcessScannedQRCodeAsync(message.Value);
        }

        async partial void OnTransactionIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                await LoadTransactionAndQRCodesAsync();
            }
        }

        /// <summary>
        /// Güvenli tarama ile QR kodu işler (konum ve PIN doğrulaması dahil)
        /// </summary>
        public async Task ProcessScannedQRCodeAsync(string qrCodeData)
        {
            IsLoading = true;

            try
            {
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

                // 1. Konum al
                try
                {
                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best,
                        Timeout = TimeSpan.FromSeconds(10)
                    });

                    if (location != null)
                    {
                        CurrentLatitude = location.Latitude;
                        CurrentLongitude = location.Longitude;
                    }
                    else
                    {
                        // Konum alınamadıysa kullanıcıyı uyar ama devam et (backward compatibility)
                        CurrentLatitude = 0;
                        CurrentLongitude = 0;
                    }
                }
                catch (Exception)
                {
                    // Konum izni yoksa veya hata varsa, 0,0 kullan (backward compatibility)
                    CurrentLatitude = 0;
                    CurrentLongitude = 0;
                }

                // 2. PIN iste (eğer QR kodda PIN varsa)
                if (!string.IsNullOrEmpty(OtherUserDelivery.VerificationPin) && string.IsNullOrEmpty(VerificationPin))
                {
                    VerificationPin = await Application.Current.MainPage.DisplayPromptAsync(
                        "PIN Doğrulama",
                        "6 haneli PIN kodunu girin:",
                        maxLength: 6,
                        keyboard: Keyboard.Numeric);
                    
                    if (string.IsNullOrEmpty(VerificationPin))
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", "PIN kodu gereklidir.", "Tamam");
                        IsLoading = false;
                        return;
                    }
                }

                // 3. Güvenli tarama yap veya eski yöntemle devam et
                if (!string.IsNullOrEmpty(OtherUserDelivery.VerificationPin))
                {
                    // Yeni güvenli QR kod
                    var result = await _qrCodeService.ScanQRCodeWithLocationAsync(
                        OtherUserDelivery.QRCodeId,
                        CurrentLatitude,
                        CurrentLongitude,
                        VerificationPin);

                    if (result.Success)
                    {
                        // Takas tamamlandıysa ürünleri işaretle
                        await CheckAndMarkExchangeComplete();
                        
                        await Application.Current.MainPage.DisplayAlert("Başarılı", result.Message, "Tamam");
                        await LoadTransactionAndQRCodesAsync();
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }
                else
                {
                    // Eski QR kod (backward compatibility)
                    var result = await _qrCodeService.CompleteDeliveryAsync(OtherUserDelivery.QRCodeId);
                    if (result.Success)
                    {
                        await CheckAndMarkExchangeComplete();

                        await Application.Current.MainPage.DisplayAlert("Başarılı",
                            $"'{OtherUserDelivery.ProductTitle}' ürününü teslim aldığınız onaylandı.", "Harika!");

                        await LoadTransactionAndQRCodesAsync();
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }

                // PIN'i temizle
                VerificationPin = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Eski ProcessScannedQRCode metodu için backward compatibility
        /// </summary>
        public async Task ProcessScannedQRCode(string qrCodeData)
        {
            await ProcessScannedQRCodeAsync(qrCodeData);
        }

        private async Task CheckAndMarkExchangeComplete()
        {
            // Her iki teslimat da tamamlandıysa ürünleri "TAKAS YAPILDI" olarak işaretle
            if (MyDelivery?.IsUsed == true && OtherUserDelivery?.IsUsed == true && CurrentTransaction != null)
            {
                await _productService.MarkAsExchangedAsync(CurrentTransaction.ProductId);
                await _productService.MarkAsExchangedAsync(CurrentTransaction.OfferedProductId);
            }
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

            CurrentQRCode = MyDelivery;
            
            // Süre sayacını başlat
            StartExpirationTimer();

            UpdateUIState();
            IsLoading = false;
        }

        /// <summary>
        /// QR kodun süre dolum sayacını başlatır (IDispatcherTimer ile)
        /// </summary>
        private void StartExpirationTimer()
        {
            // Önceki timer'ı durdur
            StopExpirationTimer();

            // Yeni timer oluştur
            _expirationTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_expirationTimer == null) return;

            _expirationTimer.Interval = TimeSpan.FromSeconds(1);
            _expirationTimer.Tick += (s, e) => UpdateTimeRemaining();
            _expirationTimer.Start();
        }

        /// <summary>
        /// Timer'ı durdurur
        /// </summary>
        private void StopExpirationTimer()
        {
            _expirationTimer?.Stop();
            _expirationTimer = null;
        }

        /// <summary>
        /// Kalan süreyi günceller
        /// </summary>
        private void UpdateTimeRemaining()
        {
            if (CurrentQRCode == null)
            {
                TimeRemaining = "";
                CanExtendTime = false;
                return;
            }

            if (CurrentQRCode.IsExpired)
            {
                TimeRemaining = "Süresi doldu";
                CanExtendTime = false;
                StopExpirationTimer();
                return;
            }

            var remaining = CurrentQRCode.ExpiresAt - DateTime.UtcNow;
            
            if (remaining.TotalSeconds <= 0)
            {
                TimeRemaining = "Süresi doldu";
                CanExtendTime = false;
                StopExpirationTimer();
            }
            else if (remaining.TotalMinutes > 1)
            {
                TimeRemaining = $"{(int)remaining.TotalMinutes} dakika";
                CanExtendTime = remaining.TotalMinutes < ExtendTimeThresholdMinutes && !CurrentQRCode.HasBeenExtended;
            }
            else
            {
                TimeRemaining = $"{(int)remaining.TotalSeconds} saniye";
                CanExtendTime = !CurrentQRCode.HasBeenExtended;
            }
        }

        [RelayCommand]
        private async Task ScanQRCodeAsync()
        {
            await Shell.Current.GoToAsync("qrscanner");
        }

        [RelayCommand]
        private async Task ExtendTimeAsync()
        {
            if (CurrentQRCode == null) return;

            var minutes = await Application.Current.MainPage.DisplayPromptAsync(
                "Süre Uzat",
                "Kaç dakika uzatmak istersiniz? (Max 30)",
                maxLength: 2,
                keyboard: Keyboard.Numeric);

            if (int.TryParse(minutes, out int value))
            {
                var result = await _qrCodeService.ExtendQRCodeValidityAsync(
                    CurrentQRCode.QRCodeId, value);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı",
                        $"Süre {value} dakika uzatıldı. Yeni bitiş: {result.Data:HH:mm}", "Tamam");
                    await LoadTransactionAndQRCodesAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
        }

        [RelayCommand]
        private async Task CancelDeliveryAsync()
        {
            if (CurrentQRCode == null) return;

            var reason = await Application.Current.MainPage.DisplayActionSheet(
                "İptal Nedeni",
                "Vazgeç",
                null,
                "Randevuya gelemiyorum",
                "Ürünü bulamadım",
                "Fikrim değişti",
                "Diğer");

            if (reason != "Vazgeç" && !string.IsNullOrEmpty(reason))
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı bulunamadı.", "Tamam");
                    return;
                }

                var result = await _qrCodeService.CancelDeliveryQRCodeAsync(
                    CurrentQRCode.QRCodeId,
                    currentUser.UserId,
                    reason);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Bilgi", "Teslimat iptal edildi.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
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

            // FAZ 2: Fotoğraf durumunu güncelle
            PhotoRequired = MyDelivery?.PhotoRequired ?? false;
            IsPhotoUploaded = !string.IsNullOrEmpty(MyDelivery?.DeliveryPhotoUrl);
            if (IsPhotoUploaded && !string.IsNullOrEmpty(MyDelivery?.DeliveryPhotoThumbnailUrl))
            {
                DeliveryPhotoSource = ImageSource.FromUri(new Uri(MyDelivery.DeliveryPhotoThumbnailUrl));
            }
        }

        // FAZ 2: Fotoğraf komutları

        [RelayCommand]
        private async Task TakeDeliveryPhotoAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await Application.Current.MainPage.DisplayAlert("İzin Gerekli", 
                        "Fotoğraf çekmek için kamera iznine ihtiyaç var.", "Tamam");
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    await ProcessAndUploadPhotoAsync(photo);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf çekerken hata oluştu: {ex.Message}", "Tamam");
            }
        }

        [RelayCommand]
        private async Task PickPhotoFromGalleryAsync()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo != null)
                {
                    await ProcessAndUploadPhotoAsync(photo);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf seçerken hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async Task ProcessAndUploadPhotoAsync(FileResult photo)
        {
            IsLoading = true;
            
            try
            {
                using var stream = await photo.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı bulunamadı.", "Tamam");
                    return;
                }

                if (MyDelivery == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "QR kod bilgisi bulunamadı.", "Tamam");
                    return;
                }

                var result = await _qrCodeService.UploadDeliveryPhotoAsync(
                    MyDelivery.QRCodeId, 
                    ms.ToArray(), 
                    currentUser.UserId);

                if (result.Success)
                {
                    await LoadTransactionAndQRCodesAsync();
                    await Application.Current.MainPage.DisplayAlert("Başarılı", 
                        "Fotoğraf yüklendi! Teslimat tamamlandı.", "Tamam");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf yüklenirken hata oluştu: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ViewFullPhotoAsync()
        {
            var photoUrl = MyDelivery?.DeliveryPhotoUrl ?? OtherUserDelivery?.DeliveryPhotoUrl;
            
            if (string.IsNullOrEmpty(photoUrl))
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", 
                    "Görüntülenecek fotoğraf bulunamadı.", "Tamam");
                return;
            }

            await Shell.Current.GoToAsync($"ImageViewerPage?photoUrl={Uri.EscapeDataString(photoUrl)}");
        }
    }
}