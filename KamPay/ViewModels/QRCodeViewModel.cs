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
        private const int MAX_PIN_ATTEMPTS = 3;

        [ObservableProperty]
        private string transactionId = string.Empty;

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

        //  Yeni Güvenlik Özellikleri
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

        //  Fotoğraf özellikleri
        [ObservableProperty]
        private bool photoRequired;

        [ObservableProperty]
        private ImageSource? deliveryPhotoSource;

        [ObservableProperty]
        private bool isPhotoUploaded;

        // ✅ YENİ: Sıralı teslimat kontrolü
        [ObservableProperty]
        private bool canScanOtherQR = true; // QR tarama butonunun aktif olup olmadığını belirler

        [ObservableProperty]
        private string scanButtonText = "QR Kodunu Tarat"; // Buton metni

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

        async partial void OnTransactionIdChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                await LoadTransactionAndQRCodesAsync();
            }
        }

       
        /// <summary>
        /// Güvenli tarama ile QR kodu işler (konum ve PIN doğrulaması dahil)
        /// TAKAS AKIŞI: Sıralı teslimat sistemi (ilk PIN+fotoğraf, ikinci sadece onay)
        /// </summary>
        public async Task ProcessScannedQRCodeAsync(string qrCodeData)
        {
            IsLoading = true;

            try
            {
                // 1️⃣ QR Kod Doğrulama
                if (OtherUserDelivery == null || qrCodeData != OtherUserDelivery.QRCodeData)
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert("Hata", "Geçersiz veya bu takasa ait olmayan bir QR kod okuttunuz.", "Tamam");
                    IsLoading = false;
                    return;
                }

                if (OtherUserDelivery.IsUsed)
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.DisplayAlert("Bilgi", "Bu ürünün teslimatı zaten onaylanmış.", "Tamam");
                    IsLoading = false;
                    return;
                }

                // 2️⃣ ✅ KRİTİK: SIRAYLA TESLİMAT KONTROLÜ
                bool myDeliveryCompleted = MyDelivery?.IsUsed ?? false;
                bool otherDeliveryCompleted = OtherUserDelivery?.IsUsed ?? false;
                
                bool isFirstDelivery = !myDeliveryCompleted && !otherDeliveryCompleted;
                bool isSecondDelivery = myDeliveryCompleted && !otherDeliveryCompleted;

                System.Diagnostics.Debug.WriteLine($"🔍 [TAKAS AKIŞ] Teslimat Durumu:");
                System.Diagnostics.Debug.WriteLine($"   MyDelivery.IsUsed: {myDeliveryCompleted}");
                System.Diagnostics.Debug.WriteLine($"   OtherDelivery.IsUsed: {otherDeliveryCompleted}");
                System.Diagnostics.Debug.WriteLine($"   İlk Teslimat: {isFirstDelivery}");
                System.Diagnostics.Debug.WriteLine($"   İkinci Teslimat: {isSecondDelivery}");

                // 3️⃣ ✅ ROL BELİRLEME
                bool isSeller = CurrentTransaction?.SellerId == MyDelivery?.SellerId;
                string myRole = isSeller ? "SATICI" : "ALICI";
                System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] Rol: {myRole}");
                System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] Ben Satıcıyım: {isSeller}");

                // 4️⃣ ✅ SADECE ALICI İÇİN KONTROL: Satıcı henüz almadıysa ALICI taratamaz
                if (!isSeller && !myDeliveryCompleted && Application.Current?.MainPage != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] ❌ ALICI henüz taratamaz - Satıcı önce almalı");
                    System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] Benim sıram: Hayır (Önce satıcı teslim almalı)");
                    
                    await Application.Current.MainPage.DisplayAlert(
                        "⚠️ Satıcı Henüz Teslim Almadı", 
                        "Takas sürecinde önce satıcı sizin ürününüzü almalıdır.\n\n" +
                        "👉 Satıcıya kendi QR kodunuzu gösterin ve okutun.\n" +
                        "👉 Satıcı PIN girip teslim aldıktan sonra,\n" +
                        "👉 Siz de satıcının QR kodunu taratabilirsiniz.",
                        "Anladım");
                    IsLoading = false;
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] ✅ Sıra kontrolü geçti - Tarama işlemine devam");
                System.Diagnostics.Debug.WriteLine($"   [TAKAS AKIŞ] Benim sıram: Evet");

                // 5️⃣ İLK TESLİMAT: Konum + PIN + Fotoğraf Gerekli (SATICI TARAR)
                if (isFirstDelivery)
                {
                    System.Diagnostics.Debug.WriteLine("✅ İLK TESLİMAT: PIN ve konum doğrulaması ile tarama (SATICI)");

                    // Konum al
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
                            CurrentLatitude = 0;
                            CurrentLongitude = 0;
                        }
                    }
                    catch (Exception)
                    {
                        CurrentLatitude = 0;
                        CurrentLongitude = 0;
                    }

                    // PIN iste ve doğrula (maksimum 3 deneme hakkı ile)
                    if (!string.IsNullOrEmpty(OtherUserDelivery.VerificationPin))
                    {
                        int pinAttempts = 0;
                        bool pinVerified = false;
                        ServiceResult<bool>? scanResult = null;

                        while (pinAttempts < MAX_PIN_ATTEMPTS && !pinVerified)
                        {
                            if (Application.Current?.MainPage != null)
                            {
                                string promptMessage = pinAttempts == 0
                                    ? "Karşı tarafın size verdiği 6 haneli PIN kodunu girin:"
                                    : $"Yanlış PIN! Kalan deneme: {MAX_PIN_ATTEMPTS - pinAttempts}\n\nLütfen doğru PIN kodunu girin:";

                                System.Diagnostics.Debug.WriteLine($"[PIN DOĞRULAMA] Deneme: {pinAttempts + 1}/{MAX_PIN_ATTEMPTS}");

                                VerificationPin = await Application.Current.MainPage.DisplayPromptAsync(
                                    "🔐 PIN Doğrulama (İlk Teslimat)",
                                    promptMessage,
                                    maxLength: 6,
                                    keyboard: Keyboard.Numeric);

                                if (string.IsNullOrEmpty(VerificationPin))
                                {
                                    // Kullanıcı iptal etti
                                    await Application.Current.MainPage.DisplayAlert(
                                        "İptal Edildi",
                                        "PIN girişi iptal edildi. Teslimat işlemi tamamlanamadı.",
                                        "Tamam");
                                    IsLoading = false;
                                    return;
                                }

                                // PIN'i backend'de doğrula
                                scanResult = await _qrCodeService.ScanQRCodeWithLocationAsync(
                                    OtherUserDelivery.QRCodeId,
                                    CurrentLatitude,
                                    CurrentLongitude,
                                    VerificationPin);

                                pinAttempts++;

                                if (scanResult.Success)
                                {
                                    pinVerified = true;
                                    System.Diagnostics.Debug.WriteLine($"[PIN DOĞRULAMA] ✅ Başarılı - Deneme: {pinAttempts}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[PIN DOĞRULAMA] ❌ Başarısız - Deneme: {pinAttempts}, Hata: {scanResult.Message}");
                                    
                                    // Eğer backend QR kodu iptal ettiyse (max attempt aşıldı), döngüyü kır
                                    if (scanResult.Message.Contains("iptal edildi") || scanResult.Message.Contains("Çok fazla"))
                                    {
                                        await Application.Current.MainPage.DisplayAlert(
                                            "❌ QR Kod İptal Edildi",
                                            "Çok fazla yanlış PIN denemesi yapıldı.\n\n" +
                                            "QR kod güvenlik nedeniyle iptal edildi.\n" +
                                            "Lütfen yeni bir teslimat QR kodu oluşturun.",
                                            "Tamam");
                                        IsLoading = false;
                                        return;
                                    }

                                    // Son deneme hakkı kullanıldıysa
                                    if (pinAttempts >= MAX_PIN_ATTEMPTS)
                                    {
                                        await Application.Current.MainPage.DisplayAlert(
                                            "❌ Maksimum Deneme Sayısı Aşıldı",
                                            $"PIN doğrulaması {MAX_PIN_ATTEMPTS} kez başarısız oldu.\n\n" +
                                            "Teslimat işlemi iptal edildi.\n" +
                                            "Lütfen doğru PIN kodunu karşı taraftan alın ve tekrar deneyin.",
                                            "Tamam");
                                        IsLoading = false;
                                        return;
                                    }
                                }
                            }
                        }

                        // PIN doğrulandıysa sonuçları işle
                        if (pinVerified && scanResult != null)
                        {
                            await CheckAndMarkExchangeComplete();

                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.DisplayAlert(
                                    "✅ İlk Teslimat Tamam",
                                    $"'{OtherUserDelivery.ProductTitle}' ürününü teslim aldınız!\n\n" +
                                    "👉 Şimdi sıra karşı tarafta.\n" +
                                    "👉 Karşı tarafa kendi QR kodunuzu okutun.",
                                    "Tamam");

                            await LoadTransactionAndQRCodesAsync();
                        }
                    }
                    else
                    {
                        // PIN olmayan eski QR kodlar için backward compatibility
                        var result = await _qrCodeService.ScanQRCodeWithLocationAsync(
                            OtherUserDelivery.QRCodeId,
                            CurrentLatitude,
                            CurrentLongitude,
                            null);

                        if (result.Success)
                        {
                            await CheckAndMarkExchangeComplete();

                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.DisplayAlert(
                                    "✅ İlk Teslimat Tamam",
                                    $"'{OtherUserDelivery.ProductTitle}' ürününü teslim aldınız!\n\n" +
                                    "👉 Şimdi sıra karşı tarafta.\n" +
                                    "👉 Karşı tarafa kendi QR kodunuzu okutun.",
                                    "Tamam");

                            await LoadTransactionAndQRCodesAsync();
                        }
                        else
                        {
                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                        }
                    }
                }
                // 5️⃣ İKİNCİ TESLİMAT: PIN ve Fotoğraf Olmadan Direkt Onay
                else if (isSecondDelivery)
                {
                    System.Diagnostics.Debug.WriteLine("✅ İKİNCİ TESLİMAT: PIN ve fotoğraf gerektirmeden onaylama");
                    
                    var result = await _qrCodeService.CompleteDeliveryAsync(OtherUserDelivery.QRCodeId);
                    if (result.Success)
                    {
                        await CheckAndMarkExchangeComplete();

                        if (Application.Current?.MainPage != null)
                            await Application.Current.MainPage.DisplayAlert(
                                "✅ Takas Tamamlandı!",
                                $"'{OtherUserDelivery.ProductTitle}' teslimatı onaylandı!\n\n" +
                                "🎉 Takas başarıyla tamamlandı. Puanlarınız eklendi.", 
                                "Harika!");

                        await LoadTransactionAndQRCodesAsync();
                        
                        // 3 saniye sonra geri dön
                        await Task.Delay(3000);
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        if (Application.Current?.MainPage != null)
                            await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                    }
                }

                // PIN'i temizle
                VerificationPin = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ QR tarama hatası: {ex.Message}");
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Hata", $"QR kod işlenirken hata oluştu: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

       
        /// Eski ProcessScannedQRCode metodu için backward compatibility
       
        public async Task ProcessScannedQRCode(string qrCodeData)
        {
            await ProcessScannedQRCodeAsync(qrCodeData);
        }

        private async Task CheckAndMarkExchangeComplete()
        {
            // Her iki teslimat da tamamlandıysa ürünleri "TAKAS YAPILDI" olarak işaretle
            if (MyDelivery?.IsUsed == true && OtherUserDelivery?.IsUsed == true && CurrentTransaction != null)
            {
                if (!string.IsNullOrEmpty(CurrentTransaction.ProductId))
                    await _productService.MarkAsExchangedAsync(CurrentTransaction.ProductId);
                
                if (!string.IsNullOrEmpty(CurrentTransaction.OfferedProductId))
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
                if (Application.Current?.MainPage != null)
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
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Hata", "İşlem detayı bulunamadı.", "Tamam");
                return;
            }

            var qrCodesResult = await _qrCodeService.GetQRCodesForTransactionAsync(TransactionId);
            if (!qrCodesResult.Success || qrCodesResult.Data == null)
            {
                IsLoading = false;
                if (Application.Current?.MainPage != null)
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

       
        /// QR kodun süre dolum sayacını başlatır (IDispatcherTimer ile)
       
        private void StartExpirationTimer()
        {
            // Önceki timer'ı durdur
            StopExpirationTimer();

            // Yeni timer oluştur - .NET MAUI için doğru kullanım
            _expirationTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_expirationTimer == null) return;

            _expirationTimer.Interval = TimeSpan.FromSeconds(1);
            _expirationTimer.Tick += (s, e) => UpdateTimeRemaining();
            _expirationTimer.Start();
        }

       
        /// Timer'ı durdurur
       
        private void StopExpirationTimer()
        {
            _expirationTimer?.Stop();
            _expirationTimer = null;
        }

       
        /// Kalan süreyi günceller
       
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
            if (Application.Current?.MainPage == null) return;

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
            if (Application.Current?.MainPage == null) return;

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
            System.Diagnostics.Debug.WriteLine($"🔄 [UI STATE UPDATE] Başlatılıyor...");
            
            bool myDeliveryCompleted = MyDelivery?.IsUsed ?? false;
            bool otherDeliveryCompleted = OtherUserDelivery?.IsUsed ?? false;

            // ✅ KİM SATICI, KİM ALICI?
            // CurrentTransaction.SellerId = Ürünü satan kişi (ilk QR'ı taratan)
            // CurrentTransaction.BuyerId = Teklif yapan kişi (ikinci QR'ı taratan)
            bool isSeller = CurrentTransaction?.SellerId == MyDelivery?.SellerId;
            string myRole = isSeller ? "SATICI" : "ALICI";
            
            // ✅ SIRAYLA TESLİMAT KONTROLÜ
            bool isFirstDelivery = !myDeliveryCompleted && !otherDeliveryCompleted;
            bool myTurnToDeliver = !myDeliveryCompleted; // Benim teslim sıram var mı?
            bool canReceive = myDeliveryCompleted && !otherDeliveryCompleted; // Karşı taraftan alabilir miyim?
            bool bothCompleted = myDeliveryCompleted && otherDeliveryCompleted;

            System.Diagnostics.Debug.WriteLine($"🔄 [UI STATE UPDATE] Durum Analizi:");
            System.Diagnostics.Debug.WriteLine($"   Rol: {myRole}");
            System.Diagnostics.Debug.WriteLine($"   Ben Satıcıyım: {isSeller}");
            System.Diagnostics.Debug.WriteLine($"   MyDelivery Tamamlandı: {myDeliveryCompleted}");
            System.Diagnostics.Debug.WriteLine($"   OtherDelivery Tamamlandı: {otherDeliveryCompleted}");
            System.Diagnostics.Debug.WriteLine($"   Benim Sıram: {myTurnToDeliver}");
            System.Diagnostics.Debug.WriteLine($"   Alabilirim: {canReceive}");
            System.Diagnostics.Debug.WriteLine($"   Her İkisi Tamamlandı: {bothCompleted}");
            
            // ✅ YENİ: MyDelivery ve OtherUserDelivery bilgilerini de logla
            System.Diagnostics.Debug.WriteLine($"   MyDelivery.ProductId: {MyDelivery?.ProductId ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   MyDelivery.ProductTitle: {MyDelivery?.ProductTitle ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   OtherDelivery.ProductId: {OtherUserDelivery?.ProductId ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   OtherDelivery.ProductTitle: {OtherUserDelivery?.ProductTitle ?? "NULL"}");

            // ✅ KRİTİK FİX: QR tarama butonu kontrolü
            // SATICI: İlk başta tarayabilir (isFirstDelivery && isSeller)
            // ALICI: Sadece satıcı teslim ettikten sonra tarayabilir (canReceive && !isSeller)
            if (isSeller)
            {
                // SATICI: İlk teslimat aşamasında veya karşı taraftan alma aşamasında tarayabilir
                CanScanOtherQR = isFirstDelivery || canReceive;
            }
            else
            {
                // ALICI: Sadece satıcı teslim ettikten sonra tarayabilir
                CanScanOtherQR = canReceive;
            }
            
            System.Diagnostics.Debug.WriteLine($"   ✅ CanScanOtherQR SET EDİLDİ: {CanScanOtherQR}");

            if (bothCompleted)
            {
                PageTitle = "🎉 Takas Tamamlandı!";
                InstructionText = "Her iki ürün de teslim edildi. Puanlarınız eklendi!";
                ScanButtonText = "✅ Takas Tamamlandı";
                CanScanOtherQR = false;
            }
            else if (canReceive)
            {
                PageTitle = "✅ Şimdi Sıra Karşı Tarafta";
                InstructionText = $"Kendi ürününüzü ({MyDelivery?.ProductTitle}) teslim ettiniz!\n\n" +
                                 $"👉 Karşı taraftan '{OtherUserDelivery?.ProductTitle}' ürününü almak için onun QR kodunu taratın.";
                ScanButtonText = "📸 Karşı Tarafın QR Kodunu Tarat";
            }
            else if (myTurnToDeliver)
            {
                if (isSeller)
                {
                    // SATICI: İlk başta QR tarayabilir
                    PageTitle = "📦 Takası Başlat (Satıcı)";
                    InstructionText = $"Siz SATICI olarak takası başlatıyorsunuz:\n\n" +
                                     $"1️⃣ Alıcının '{OtherUserDelivery?.ProductTitle}' ürününün QR kodunu taratın\n" +
                                     $"2️⃣ Alıcıdan PIN kodunu alın ve girin\n" +
                                     $"3️⃣ Teslimat fotoğrafı çekin\n" +
                                     $"4️⃣ Sonra alıcıya kendi QR kodunuzu gösterin";
                    ScanButtonText = "📸 Alıcının QR Kodunu Tarat";
                }
                else
                {
                    // ALICI: Önce satıcının taramasını beklemeli
                    PageTitle = "📦 Satıcının Teslim Almasını Bekleyin";
                    InstructionText = $"Siz ALICI olarak önce satıcıya teslim edin:\n\n" +
                                     $"1️⃣ Satıcıya kendi QR kodunuzu ({MyDelivery?.ProductTitle}) gösterin\n" +
                                     $"2️⃣ Satıcıya PIN kodunuzu söyleyin\n" +
                                     $"3️⃣ Satıcı teslim aldıktan sonra,\n" +
                                     $"4️⃣ Satıcının '{OtherUserDelivery?.ProductTitle}' QR kodunu taratın";
                    ScanButtonText = "⏳ Henüz Taratılamaz (Satıcı Almadı)";
                }
            }
            else
            {
                PageTitle = "⏳ Teslimat Süreci";
                InstructionText = "Lütfen bekleyin...";
                ScanButtonText = "📸 QR Kodunu Tarat";
                CanScanOtherQR = false;
            }
            
            System.Diagnostics.Debug.WriteLine($"   📋 [UI STATE UPDATE] SON DURUM:");
            System.Diagnostics.Debug.WriteLine($"      PageTitle: {PageTitle}");
            System.Diagnostics.Debug.WriteLine($"      ScanButtonText: {ScanButtonText}");
            System.Diagnostics.Debug.WriteLine($"      CanScanOtherQR: {CanScanOtherQR}");

            // Fotoğraf durumunu güncelle
            PhotoRequired = MyDelivery?.PhotoRequired ?? false;
            IsPhotoUploaded = !string.IsNullOrEmpty(MyDelivery?.DeliveryPhotoUrl);
            
            System.Diagnostics.Debug.WriteLine($"      PhotoRequired: {PhotoRequired}");
            System.Diagnostics.Debug.WriteLine($"      IsPhotoUploaded: {IsPhotoUploaded}");
            
            if (!string.IsNullOrEmpty(MyDelivery?.DeliveryPhotoThumbnailUrl))
            {
                DeliveryPhotoSource = ImageSource.FromUri(new Uri(MyDelivery.DeliveryPhotoThumbnailUrl));
                System.Diagnostics.Debug.WriteLine($"      DeliveryPhotoSource: {MyDelivery.DeliveryPhotoThumbnailUrl}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"      DeliveryPhotoSource: NULL");
            }
            
            System.Diagnostics.Debug.WriteLine($"🔄 [UI STATE UPDATE] Tamamlandı");
        }

        // : Fotoğraf komutları

        [RelayCommand]
        private async Task TakeDeliveryPhotoAsync()
        {
            if (Application.Current?.MainPage == null) return;

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
            if (Application.Current?.MainPage == null) return;

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
            if (Application.Current?.MainPage == null) return;

            IsLoading = true;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] Başlatılıyor...");
                
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

                System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] QR Kod ID: {MyDelivery.QRCodeId}");
                System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] Fotoğraf boyutu: {ms.Length} bytes");

                var result = await _qrCodeService.UploadDeliveryPhotoAsync(
                    MyDelivery.QRCodeId, 
                    ms.ToArray(), 
                    currentUser.UserId);

                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] ✅ Başarılı - URL: {result.Data}");
                    
                    // 1. Backend verilerini yeniden yükle
                    await LoadTransactionAndQRCodesAsync();
                    
                    // 2. UI state'i güncelle (LoadTransactionAndQRCodesAsync içinde UpdateUIState çağrılıyor)
                    // Ek doğrulama için manuel kontrol
                    System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] UI Durumu Güncellendi:");
                    System.Diagnostics.Debug.WriteLine($"   PhotoRequired: {PhotoRequired}");
                    System.Diagnostics.Debug.WriteLine($"   IsPhotoUploaded: {IsPhotoUploaded}");
                    System.Diagnostics.Debug.WriteLine($"   DeliveryPhotoSource: {(DeliveryPhotoSource != null ? "SET" : "NULL")}");
                    
                    // 3. Kullanıcıya başarı mesajı göster
                    await Application.Current.MainPage.DisplayAlert("✅ Başarılı", 
                        "Fotoğraf başarıyla yüklendi!\n\nTeslimat tamamlandı.", "Tamam");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] ❌ Başarısız - Hata: {result.Message}");
                    await Application.Current.MainPage.DisplayAlert("Hata", 
                        $"Fotoğraf yüklenemedi.\n\n{result.Message}\n\nLütfen tekrar deneyin.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FOTOĞRAF YÜKLEME] ❌ İstisna: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", 
                    $"Fotoğraf yüklenirken hata oluştu:\n\n{ex.Message}", "Tamam");
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
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.DisplayAlert("Bilgi", 
                        "Görüntülenecek fotoğraf bulunamadı.", "Tamam");
                return;
            }

            await Shell.Current.GoToAsync($"ImageViewerPage?photoUrl={Uri.EscapeDataString(photoUrl)}");
        }
    }
}