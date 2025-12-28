using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    public partial class FirebaseTransactionService : ITransactionService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IUserProfileService _userProfileService; // Puan için eklendi


       
        internal class TempOtpModel
        {
            public string Otp { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        private string GenerateOtp() => new Random().Next(100000, 999999).ToString();
        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";


        public FirebaseTransactionService(
          INotificationService notificationService,
          IProductService productService,
          IQRCodeService qrCodeService,
          IUserProfileService userProfileService) // UserProfileService eklendi
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
            _notificationService = notificationService;
            _productService = productService;
            _qrCodeService = qrCodeService;
            _userProfileService = userProfileService; // Atama yapıldı
        }


        public async Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null) return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");
                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<Transaction>.SuccessResult(transaction, "Bu teklif zaten yanıtlanmış.");

                transaction.Status = accept ? TransactionStatus.Accepted : TransactionStatus.Rejected;
                transaction.UpdatedAt = DateTime.UtcNow;

                await transactionNode.PutAsync(transaction);

                // Alıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = accept ? NotificationType.OfferAccepted : NotificationType.OfferRejected,
                    Title = accept ? "Teklifin Kabul Edildi!" : "Teklifin Reddedildi",
                    Message = $"'{transaction.SellerName}', '{transaction.ProductTitle}' ürünü için yaptığın teklifi {(accept ? "kabul etti." : "reddetti.")}",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (accept)
                {
                    // Ürünleri rezerve et
                    await _productService.MarkAsReservedAsync(transaction.ProductId, true);

                    if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                    {
                        await _productService.MarkAsReservedAsync(transaction.OfferedProductId, true);

                        //  KRİTİK: TAKAS İÇİN GÜVENLİ QR KODLARI OLUŞTUR
                        Console.WriteLine($"✅ Takas kabul edildi. Güvenli QR kodlar oluşturuluyor: {transactionId}");

                        // Satıcının ürünü için güvenli QR kod (60 dakika geçerli)
                        var qrCode1 = await _qrCodeService.GenerateSecureDeliveryQRCodeAsync(
                            transactionId,
                            transaction.ProductId,
                            transaction.ProductTitle,
                            transaction.SellerId,
                            transaction.BuyerId,
                            validityMinutes: 60, // 1 saat
                            meetingPointLatitude: null, // Şimdilik null, 3'te eklenecek
                            meetingPointLongitude: null,
                            meetingPointName: null
                        );

                        // Alıcının ürünü için güvenli QR kod (60 dakika geçerli)
                        var qrCode2 = await _qrCodeService.GenerateSecureDeliveryQRCodeAsync(
                            transactionId,
                            transaction.OfferedProductId,
                            transaction.OfferedProductTitle,
                            transaction.BuyerId, // Teklif veren alıcı, bu ürünün sahibi
                            transaction.SellerId, // Satıcı bu ürünü alacak
                            validityMinutes: 60, // 1 saat
                            meetingPointLatitude: null,
                            meetingPointLongitude: null,
                            meetingPointName: null
                        );

                        if (!qrCode1.Success || !qrCode2.Success)
                        {
                            Console.WriteLine($"❌ QR kod oluşturma hatası!");
                            return ServiceResult<Transaction>.FailureResult($"Takas kabul edildi ancak QR kodlar oluşturulamadı.");
                        }

                        Console.WriteLine($"✅ Güvenli QR kodlar başarıyla oluşturuldu!");
                    }
                }

                return ServiceResult<Transaction>.SuccessResult(transaction, "İşlem başarılı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RespondToOfferAsync hatası: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("Hata", ex.Message);
            }
        }
        // Satış işlemi için simülasyonlu ödeme başlatma
        public async Task<ServiceResult<PaymentDto>> StartSalePaymentAsync(string transactionId, string method)
        {
            // 1. Ağ Kontrolü (DevOps Standartı)
            if (!NetworkHelper.HasInternetConnection())
                return ServiceResult<PaymentDto>.FailureResult("İnternet bağlantısı yok.", "Lütfen bağlantınızı kontrol edin.");

            // 2. Hız Sınırı Kontrolü (Spam Engelleme)
            var limitCheck = RateLimiters.ApiCall.CheckLimit(transactionId);
            if (!limitCheck.IsAllowed)
                return ServiceResult<PaymentDto>.FailureResult(limitCheck.Message);

            // Mevcut CreatePaymentSimulationAsync metodunu çağırarak ödemeyi başlatır
            return await CreatePaymentSimulationAsync(transactionId, method);
        }
        
        /// <summary>
        /// Ödeme simülasyonunu başlatır
        /// Bu metod hem ürün satışları hem de hizmet ödemeleri için kullanılır
        /// </summary>
        /// <param name="transactionId">İşlem ID'si</param>
        /// <param name="method">Ödeme yöntemi: "cardsim" veya "banktransfersim"</param>
        /// <returns>PaymentDto içeren ServiceResult</returns>
        public async Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method)
        {
            try
            {
                // 1. Transaction'ı al ve doğrula
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();
                if (transaction == null) return ServiceResult<PaymentDto>.FailureResult("İşlem bulunamadı.");

                if (transaction.PaymentStatus != PaymentStatus.Pending)
                    return ServiceResult<PaymentDto>.FailureResult("Bu işlem için ödeme zaten başlatılmış.");

                // 2. Ödenecek tutarı belirle (Pazarlık varsa QuotedPrice, yoksa Price)
                var amount = transaction.QuotedPrice > 0 ? transaction.QuotedPrice : (transaction.Price > 0 ? transaction.Price : 0m);

                // 3. Payment DTO'sunu oluştur
                var payment = new PaymentDto
                {
                    Amount = amount,
                    Currency = "TRY",
                    Status = ServicePaymentStatus.Initiated,
                    Method = method?.ToLower() switch
                    {
                        "cardsim" => PaymentMethodType.CardSim,
                        "banktransfersim" or "eft" or "havale" => PaymentMethodType.BankTransferSim,
                        _ => PaymentMethodType.CardSim
                    }
                };

                // 4. KART ÖDEMESİ: OTP oluştur ve Firebase'e kaydet (2 dakika geçerli)
                if (payment.Method == PaymentMethodType.CardSim)
                {
                    var otp = GenerateOtp(); // 6 haneli rastgele sayı
                    await _firebaseClient
                        .Child(Constants.TempOtpsCollection)
                        .Child(payment.PaymentId)
                        .PutAsync(new TempOtpModel
                        {
                            Otp = otp,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(2) // OTP 2 dakika geçerli
                        });
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Kart ödemesi için OTP oluşturuldu (PaymentId: {payment.PaymentId})");
                }

                // 5. HAVALE/EFT: Banka bilgileri ve referans kodu oluştur
                if (payment.Method == PaymentMethodType.BankTransferSim)
                {
                    payment.BankName = "Ziraat Bankası";
                    payment.BankReference = GenerateBankReference(); // Benzersiz referans kodu
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Havale/EFT için referans oluşturuldu: {payment.BankReference}");
                }

                // 6. Transaction'ı güncelle
                transaction.PaymentMethod = payment.Method;
                transaction.PaymentSimulationId = payment.PaymentId;
                transaction.PaymentStatus = PaymentStatus.Pending;
                await transactionNode.PutAsync(transaction);

                return ServiceResult<PaymentDto>.SuccessResult(payment, "Ödeme simülasyonu başlatıldı.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CreatePaymentSimulationAsync hatası: {ex.Message}");
                return ServiceResult<PaymentDto>.FailureResult("Simülasyon başlatılırken hata.", ex.Message);
            }
        }

        /// <summary>
        /// Ödeme simülasyonunu onaylar ve işlemi tamamlar
        /// </summary>
        /// <param name="transactionId">İşlem ID'si</param>
        /// <param name="paymentId">Ödeme ID'si</param>
        /// <param name="otp">OTP kodu (kart ödemeleri için zorunlu)</param>
        /// <returns>Başarı durumunu içeren ServiceResult</returns>
        public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string transactionId, string paymentId, string? otp = null)
        {
            try
            {
                // 1. Transaction'ı al ve doğrula
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();
                if (transaction == null) return ServiceResult<bool>.FailureResult("İşlem bulunamadı.");

                // 2. KART ÖDEMESİ: OTP Doğrulaması (GÜVENLİK)
                if (transaction.PaymentMethod == PaymentMethodType.CardSim)
                {
                    // Kullanıcıdan gelen OTP'yi temizle (XSS/Injection saldırılarına karşı)
                    var sanitizedOtp = InputSanitizer.SanitizeText(otp ?? "");

                    // Firebase'den kaydedilmiş OTP'yi al
                    var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(paymentId);
                    var saved = await otpNode.OnceSingleAsync<TempOtpModel>();

                    // OTP doğrulama kontrolleri
                    if (saved == null) 
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ OTP bulunamadı. PaymentId: {paymentId}");
                        return ServiceResult<bool>.FailureResult("OTP bulunamadı.");
                    }
                    
                    if (DateTime.UtcNow > saved.ExpiresAt) 
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ OTP süresi doldu. ExpiresAt: {saved.ExpiresAt}");
                        return ServiceResult<bool>.FailureResult("OTP süresi doldu.");
                    }
                    
                    if (string.IsNullOrWhiteSpace(sanitizedOtp) || saved.Otp != sanitizedOtp)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ OTP geçersiz. PaymentId: {paymentId}");
                        return ServiceResult<bool>.FailureResult("OTP geçersiz.");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✅ OTP doğrulandı! PaymentId: {paymentId}");
                    
                    // OTP'yi kullanıldıktan sonra sil (tek kullanımlık)
                    await otpNode.DeleteAsync();
                }

                // 3. Ödeme durumunu güncelle
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;

                // 4. İŞLEM TİPİNE GÖRE TAMAMLAMA
                // SATIŞ: Ürünü kapat, puan ver, bildirim gönder
                if (transaction.Type == ProductType.Satis)
                {
                    var completeResult = await CompleteTransactionInternalAsync(transaction);
                    if (!completeResult.Success)
                        return ServiceResult<bool>.FailureResult("Ödeme alındı ancak işlem tamamlanırken hata oluştu: " + completeResult.Message);
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Satış işlemi tamamlandı. TransactionId: {transactionId}");
                }
                // DİĞER TİPLER (Takas, Bağış, Hizmet): Sadece ödeme durumunu güncelle
                else
                {
                    await transactionNode.PutAsync(transaction);
                    System.Diagnostics.Debug.WriteLine($"✅ Ödeme tamamlandı. TransactionId: {transactionId}, Type: {transaction.Type}");
                }

                return ServiceResult<bool>.SuccessResult(true, "Ödeme onaylandı ve işlem tamamlandı.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ConfirmPaymentSimulationAsync hatası: {ex.Message}");
                // Teknik hataları kullanıcı dostu mesajlara dönüştür
                return ServiceResult<bool>.FailureResult("Ödeme onayında hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        // --- BU METOT HİZMET MODÜLÜ İÇİNDİR ---
        public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string transactionId)
        {
            // Bu metot, HİZMET MODÜLÜ'nün kullandığı karmaşık simülasyon akışıdır.
            var payment = await CreatePaymentSimulationAsync(transactionId, "CardSim");
            if (!payment.Success) return ServiceResult<bool>.FailureResult(payment.Message);

            await Task.Delay(1500); // Simülasyon gecikmesi

            string otp = null;
            if (payment.Data.Method == PaymentMethodType.CardSim)
            {
                var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(payment.Data.PaymentId);
                var savedOtp = await otpNode.OnceSingleAsync<TempOtpModel>();
                otp = savedOtp?.Otp;
            }

            var confirm = await ConfirmPaymentSimulationAsync(transactionId, payment.Data.PaymentId, otp: otp);

            // HİZMET modülü akışı burada bitiyor (ödeme tamamlandı). 
            // 'ServiceRequest'in 'Completed' yapılması 'FirebaseServiceSharingService' içinde yönetiliyor.
            return confirm;
        }


        [RelayCommand]
        private async Task CompletePaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;

            // Sadece onaylanmış ve ödemesi bekleyen satış işlemleri için yönlendir
            if (transaction.Type == ProductType.Satis &&
                transaction.Status == TransactionStatus.Accepted &&
                transaction.PaymentStatus == PaymentStatus.Pending)
            {
                var navigationParameter = new Dictionary<string, object>
        {
            { "Transaction", transaction }
        };

                // Sadece navigasyon yapılır. İşlemin devamı PaymentPage'de gerçekleşir. [cite: 401]
                await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Bilgi", "Bu işlem için şu an ödeme yapılamaz.", "Tamam");
            }
        }

        // ✅ ITransactionService interface'ini implement et
        public async Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");

                if (transaction.BuyerId != buyerId)
                    return ServiceResult<Transaction>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");

                if (transaction.PaymentStatus != PaymentStatus.Pending)
                    return ServiceResult<Transaction>.FailureResult("Ödeme zaten tamamlanmış veya beklemede değil.");

                // Ödeme tamamlandı olarak işaretle
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // İşlemi tamamla (puan ver, ürünü kapat, bildirim gönder)
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Ödeme tamamlanamadı.", ex.Message);
            }
        }

        // Ortak Tamamlama İşlemleri (Satış, Bağış, Takas için) 
        private async Task<ServiceResult<Transaction>> CompleteTransactionInternalAsync(Transaction transaction)
        {
            try
            {
                // 1. Transaction durumunu 'Completed' yap
                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _firebaseClient.Child(Constants.TransactionsCollection).Child(transaction.TransactionId).PutAsync(transaction);

                // 2. Ürünü 'Satıldı' olarak işaretle (IsActive=false yapar)
                await _productService.MarkAsSoldAsync(transaction.ProductId);

                // 3. Eğer Takas ise, teklif edilen ürünü de 'Satıldı' yap
                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    await _productService.MarkAsSoldAsync(transaction.OfferedProductId);
                }

                // 4. Bildirimleri gönder
                // Satıcıya:
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "Bağış Tamamlandı!" : (transaction.Type == ProductType.Takas ? "Takas Tamamlandı!" : "Ürünün Satıldı!"),
                    Message = $"'{transaction.ProductTitle}' için '{transaction.BuyerName}' ile olan işleminiz tamamlandı.",
                    ActionUrl = nameof(Views.OffersPage)
                });
                // Alıcıya:
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "Bağış Teslim Alındı!" : (transaction.Type == ProductType.Takas ? "Takas Tamamlandı!" : "Satın Alma Tamamlandı!"),
                    Message = $"'{transaction.ProductTitle}' ürünü için '{transaction.SellerName}' ile olan işleminiz tamamlandı.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                // 5. Puanları ekle (Tipe göre ayrım)
                if (transaction.Type == ProductType.Bagis)
                {
                    await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation);
                    await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation);
                }
                else // Satış veya Takas
                {
                    await _userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction);
                    await _userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction);
                }

                return ServiceResult<Transaction>.SuccessResult(transaction, "İşlem başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata - CompleteTransactionInternalAsync: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("İşlem tamamlanırken bir hata oluştu.", ex.Message);
            }
        }


        public async Task<ServiceResult<Transaction>> CreateRequestAsync(Product product, User buyer)
        {
            try
            {
                var transaction = new Transaction
                {
                    ProductId = product.ProductId,
                    ProductTitle = product.Title,
                    ProductThumbnailUrl = product.ThumbnailUrl,
                    Type = product.Type, // Product'tan gelen tipi kullanıyoruz
                    SellerId = product.UserId,
                    SellerName = product.UserName,
                    BuyerId = buyer.UserId,
                    BuyerName = buyer.FullName,
                    Status = TransactionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending, // Başlangıçta ödeme bekliyor
                    Price = product.Price // Ürünün fiyatını da ekleyelim
                };

                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);

                // Satıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = product.UserId,
                    Type = NotificationType.NewOffer,
                    Title = product.Type == ProductType.Bagis ? "Yeni Bağış Talebi!" : (product.Type == ProductType.Takas ? "Yeni Takas Teklifi!" : "Yeni Satış Teklifi!"),
                    Message = $"{buyer.FullName}, '{product.Title}' ürünün için bir {(product.Type == ProductType.Bagis ? "talep" : "teklif")} gönderdi.",
                    ActionUrl = nameof(Views.OffersPage) // Gelen Teklifler sayfası
                });

                return ServiceResult<Transaction>.SuccessResult(transaction, "İsteğiniz başarıyla gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("İstek oluşturulamadı.", ex.Message);
            }
        }

        public async Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer)
        {
            try
            {
                // Teklif edilen ürünün bilgilerini al
                var offeredProductResult = await _productService.GetProductByIdAsync(offeredProductId);
                if (!offeredProductResult.Success || offeredProductResult.Data == null)
                {
                    return ServiceResult<Transaction>.FailureResult("Teklif edilen ürün bulunamadı.");
                }
                var offeredProduct = offeredProductResult.Data;


                var transaction = new Transaction
                {
                    ProductId = product.ProductId,
                    ProductTitle = product.Title,
                    ProductThumbnailUrl = product.ThumbnailUrl,
                    Type = ProductType.Takas, // Bu kesin Takas
                    SellerId = product.UserId,
                    SellerName = product.UserName,
                    BuyerId = buyer.UserId,
                    BuyerName = buyer.FullName,
                    Status = TransactionStatus.Pending,
                    OfferedProductId = offeredProductId,
                    OfferedProductTitle = offeredProduct.Title,
                    OfferMessage = message,
                    PaymentStatus = PaymentStatus.Pending // Takasta ödeme 'N/A' (Uygulanamaz) olabilir, ama 'Pending' kalması da sorun yaratmaz.
                };


                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);


                // Satıcıya bildirim gönder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = product.UserId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Bir Takas Teklifin Var!",
                    Message = $"{buyer.FullName}, '{product.Title}' ürünün için '{offeredProduct.Title}' ürününü teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });


                return ServiceResult<Transaction>.SuccessResult(transaction, "Takas teklifiniz başarıyla gönderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Teklif oluşturulamadı.", ex.Message);
            }
        }

        // BAĞIŞ Onaylama 
        public async Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                // Kontroller
                if (transaction == null) return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");
                if (transaction.BuyerId != buyerId) return ServiceResult<Transaction>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");
                if (transaction.Status != TransactionStatus.Accepted) return ServiceResult<Transaction>.FailureResult("Bu işlem onaylanmamış veya zaten tamamlanmış.");
                if (transaction.Type != ProductType.Bagis) return ServiceResult<Transaction>.FailureResult("Bu işlem bir bağış işlemi değil.");

                // Bağışta ödeme olmadığı için PaymentStatus'ü 'Paid' yapmak,
                // Converter'ın (SimulatePaymentButtonVisibilityConverter) butonu tekrar göstermemesi için önemlidir.
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // Satış modülü için yazdığımız iç metodu TEKRAR KULLANIYORUZ.
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Bağış onaylanırken hata oluştu.", ex.Message);
            }
        }

        public async Task<ServiceResult<List<Transaction>>> GetIncomingOffersAsync(string userId)
        {
            try
            {
                var allTransactions = await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .OrderBy("SellerId")
                       .EqualTo(userId)
                       .OnceAsync<Transaction>();

                var transactions = allTransactions.Select(t => {
                    var trans = t.Object;
                    trans.TransactionId = t.Key;
                    return trans;
                })
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                return ServiceResult<List<Transaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HATA - GetIncomingOffersAsync: {ex.Message}");
                return ServiceResult<List<Transaction>>.FailureResult("Gelen teklifler alınamadı.", ex.Message);
            }
        }


        public async Task<ServiceResult<List<Transaction>>> GetMyOffersAsync(string userId)
        {
            try
            {
                var allTransactions = await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .OrderBy("BuyerId")
                       .EqualTo(userId)
                       .OnceAsync<Transaction>();

                var transactions = allTransactions.Select(t => {
                    var trans = t.Object;
                    trans.TransactionId = t.Key;
                    return trans;
                })
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                return ServiceResult<List<Transaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HATA - GetMyOffersAsync: {ex.Message}");
                return ServiceResult<List<Transaction>>.FailureResult("Gönderilen teklifler alınamadı.", ex.Message);
            }
        }

        #region 💰 SATIŞ PAZARLIK METODLARI

       
        /// Satış için fiyat teklifi (Alıcı)
       
        public async Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId)
        {
            try
            {
                if (proposedPrice <= 0)
                    return ServiceResult<bool>.FailureResult("Fiyat 0'dan büyük olmalı");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı");

                // Sadece alıcı teklif verebilir
                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                // Sadece satış işlemlerinde
                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu işlem satış değil");

                // Sadece Pending durumunda
                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                // Pazarlık devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Teklif fiyatını doğrula
                var priceValidation = NegotiationRules.ValidateProposedPrice(
                    proposedPrice, 
                    transaction.Price);
                
                if (!priceValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(priceValidation.ErrorMessage);

                // Güncelle
                transaction.ProposedPriceByBuyer = proposedPrice;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk teklif ise başlangıç tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Fiyat Teklifi",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' için {proposedPrice:N2}₺ teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : Ürün bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"📦 [{transaction.ProductTitle} - Satış]\n💰 Fiyat Teklifi: {proposedPrice:N2} ₺\n(Orijinal fiyat: {transaction.Price:N2} ₺)"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, "Fiyat teklifiniz gönderildi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ProposePriceForSale hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

       
        /// Satış için karşı teklif (Satıcı)
       
        public async Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(
            string transactionId,
            decimal counterOffer,
            string currentUserId)
        {
            try
            {
                if (counterOffer <= 0)
                    return ServiceResult<bool>.FailureResult("Fiyat 0'dan büyük olmalı");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı");

                // Sadece satıcı karşı teklif verebilir
                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu işlem satış değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                // Pazarlık devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Karşı teklifi doğrula
                var counterValidation = NegotiationRules.ValidateCounterOffer(
                    counterOffer, 
                    transaction.Price,
                    transaction.ProposedPriceByBuyer);
                
                if (!counterValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(counterValidation.ErrorMessage);

                transaction.CounterOfferBySeller = counterOffer;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk karşı teklif ise başlangıç tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.NewOffer,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{transaction.ProductTitle}' için karşı teklif: {counterOffer:N2}₺",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : Ürün bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var buyerOffer = transaction.ProposedPriceByBuyer.HasValue 
                        ? $"\n(Alıcının teklifi: {transaction.ProposedPriceByBuyer:N2} ₺)" 
                        : "";
            
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"📦 [{transaction.ProductTitle} - Satış]\n💰 Karşı Teklif: {counterOffer:N2} ₺{buyerOffer}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SendCounterOfferForSale hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        #endregion

        #region 🔄 TAKAS PAZARLIK METODLARI

       
        /// Takas için ek nakit teklifi (Talep Eden)
       
        public async Task<ServiceResult<bool>> ProposeAdditionalCashAsync(
            string transactionId,
            decimal additionalCash,
            string currentUserId)
        {
            try
            {
                if (additionalCash < 0)
                    return ServiceResult<bool>.FailureResult("Tutar negatif olamaz");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı");

                // Sadece talep eden
                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu işlem takas değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                // Pazarlık devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Ek nakit teklifini doğrula
                var cashValidation = NegotiationRules.ValidateAdditionalCash(additionalCash);
                
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.AdditionalCashByRequester = additionalCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk teklif ise başlangıç tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Nakit Teklifi",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' takası için {additionalCash:N2}₺ ek ödeme teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : Ürün bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})"
                        : "";
                    
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"🔄 [{transaction.ProductTitle} - Takas]\n💰 Ek Nakit Teklifi: {additionalCash:N2} ₺{exchangeInfo}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, "Nakit teklifiniz gönderildi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ProposeAdditionalCash hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

       
        /// Takas için karşı nakit teklifi (Sahip)
       
        public async Task<ServiceResult<bool>> SendCounterCashOfferAsync(
            string transactionId,
            decimal counterCash,
            string currentUserId)
        {
            try
            {
                if (counterCash < 0)
                    return ServiceResult<bool>.FailureResult("Tutar negatif olamaz");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı");

                // Sadece sahip
                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu işlem takas değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                // Pazarlık devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Ek nakit karşı teklifini doğrula
                var cashValidation = NegotiationRules.ValidateAdditionalCash(counterCash);
                
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.CounterCashByOwner = counterCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // İlk karşı teklif ise başlangıç tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // Pazarlık turu sayısını artır
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.NewOffer,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{transaction.ProductTitle}' takası için karşı teklif: {counterCash:N2}₺",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : Ürün bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var requesterOffer = transaction.AdditionalCashByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {transaction.AdditionalCashByRequester:N2} ₺)"
                        : "";
            
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})"
                        : "";
            
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"🔄 [{transaction.ProductTitle} - Takas]\n💰 Karşı Teklif: {counterCash:N2} ₺{requesterOffer}{exchangeInfo}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, "Karşı teklifiniz gönderildi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SendCounterCashOffer hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        #endregion

        #region 🤝 ORTAK PAZARLIK METODLARI

       
        /// Anlaşılan fiyat/tutarı kabul et (Hem Satış Hem Takas)
       
        public async Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(
            string transactionId,
            string currentUserId)
        {
            try
            {
                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı");

                // Yetki: her iki taraf da kabul edebilir
                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (!transaction.IsNegotiating)
                    return ServiceResult<bool>.FailureResult("Aktif pazarlık yok");

                // Anlaşılan tutarı belirle
                decimal agreedAmount = transaction.AgreedAmount;

                // Güncelle
                if (transaction.Type == ProductType.Satis)
                {
                    transaction.QuotedPrice = agreedAmount;
                    transaction.Price = agreedAmount;
                }
                else if (transaction.Type == ProductType.Takas)
                {
                    transaction.QuotedPrice = agreedAmount;
                }

                transaction.IsNegotiating = false;
                
                // NegotiationRules helper'ını kullanarak detaylı özet oluştur
                var acceptedBy = transaction.BuyerId == currentUserId ? "Alıcı" : "Satıcı";
                decimal? originalPrice = transaction.Type == ProductType.Satis 
                    ? transaction.Price 
                    : null; // Takas için orijinal fiyat kavramı yok
                
                var negotiationSummary = NegotiationRules.GetNegotiationSummary(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt,
                    originalPrice,
                    agreedAmount);
                
                negotiationSummary += $"👤 Kabul Eden: {acceptedBy}\n";
                
                transaction.NegotiationNotes += (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;
                transaction.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // Diğer tarafa bildirim
                var otherUserId = transaction.BuyerId == currentUserId 
                    ? transaction.SellerId 
                    : transaction.BuyerId;

                var notificationMessage = transaction.Type == ProductType.Satis
                    ? $"'{transaction.ProductTitle}' için {agreedAmount:N2}₺ fiyatı kabul edildi."
                    : $"'{transaction.ProductTitle}' takası için {agreedAmount:N2}₺ ek ödeme kabul edildi.";

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = otherUserId,
                    Type = NotificationType.OfferAccepted,
                    Title = "Fiyat Anlaşması",
                    Message = notificationMessage,
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : Ürün bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var typeIcon = transaction.Type == ProductType.Satis ? "📦" : "🔄";
                    var typeText = transaction.Type == ProductType.Satis ? "Satış" : "Takas";
                    
                    var exchangeInfo = transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})"
                        : "";
            
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"{typeIcon} [{transaction.ProductTitle} - {typeText}]\n✅ Anlaşma Sağlandı: {agreedAmount:N2} ₺{exchangeInfo}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"Anlaşılan tutar: {agreedAmount:N2}₺");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ AcceptNegotiatedPrice hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Kabul işlemi başarısız", ex.Message);
            }
        }

       
        /// Transaction için konuşma başlat
       
        public async Task<ServiceResult<string>> StartConversationForTransactionAsync(
            string transactionId,
            string currentUserId)
        {
            try
            {
                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<string>.FailureResult("İşlem bulunamadı");

                // Kullanıcının işleme dahil olduğunu doğrula
                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<string>.FailureResult("Bu işleme erişim yetkiniz yok");

                //  ÖNCELİKLE: Mevcut ConversationId'yi kontrol et
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    // Konuşmanın hala aktif olduğunu doğrula
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(transaction.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                        {
                            Console.WriteLine($"✅ Mevcut konuşma bulundu: {transaction.ConversationId}");
                            return ServiceResult<string>.SuccessResult(
                                transaction.ConversationId,
                                "Mevcut konuşma bulundu"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Mevcut konuşma kontrol hatası: {ex.Message}");
                    }
                }

                //  Kullanıcılar arasında mevcut konuşma var mı kontrol et
                var otherUserId = transaction.BuyerId == currentUserId 
                    ? transaction.SellerId 
                    : transaction.BuyerId;

                // Önce User1Id ile kontrol et
                var existingConversations1 = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OrderBy("User1Id")
                    .EqualTo(currentUserId)
                    .OnceAsync<Conversation>();

                var existingWithOtherUser = existingConversations1
                    .FirstOrDefault(c => c.Object != null && 
                                        c.Object.IsActive &&
                                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId));

                if (existingWithOtherUser == null)
                {
                    // User2Id ile de kontrol et
                    var existingConversations2 = await _firebaseClient
                        .Child(Constants.ConversationsCollection)
                        .OrderBy("User2Id")
                        .EqualTo(currentUserId)
                        .OnceAsync<Conversation>();

                    existingWithOtherUser = existingConversations2
                        .FirstOrDefault(c => c.Object != null && 
                                            c.Object.IsActive &&
                                            (c.Object.User1Id == otherUserId || c.Object.User2Id == otherUserId));
                }

                if (existingWithOtherUser != null)
                {
                    // Mevcut konuşma bulundu - transaction'a kaydet
                    transaction.ConversationId = existingWithOtherUser.Key;
                    transaction.HasActiveConversation = true;
                    
                    await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(transactionId)
                        .PutAsync(transaction);
                    
                    Console.WriteLine($"✅ Mevcut kullanıcı konuşması bulundu: {existingWithOtherUser.Key}");
                    return ServiceResult<string>.SuccessResult(
                        existingWithOtherUser.Key,
                        "Mevcut konuşma bulundu"
                    );
                }

                // Yeni konuşma oluştur
                var conversation = new Conversation
                {
                    ConversationId = Guid.NewGuid().ToString(),
                    User1Id = currentUserId,
                    User1Name = transaction.BuyerId == currentUserId ? transaction.BuyerName : transaction.SellerName,
                    User1PhotoUrl = transaction.BuyerId == currentUserId ? transaction.BuyerPhotoUrl : transaction.SellerPhotoUrl,
                    User2Id = otherUserId,
                    User2Name = transaction.BuyerId == currentUserId ? transaction.SellerName : transaction.BuyerName,
                    User2PhotoUrl = transaction.BuyerId == currentUserId ? transaction.SellerPhotoUrl : transaction.BuyerPhotoUrl,
                    LastMessage = "Görüşme başlatıldı",
                    LastMessageTime = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversation.ConversationId)
                    .PutAsync(conversation);

                // Transaction'a conversation ID'sini eklendi
                transaction.ConversationId = conversation.ConversationId;
                transaction.HasActiveConversation = true;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                //  : Sistem mesajına ürün bilgisi eklendi
                var typeIcon = transaction.Type == ProductType.Satis ? "📦" : 
                              transaction.Type == ProductType.Takas ? "🔄" : "🎁";
                var typeText = transaction.Type == ProductType.Satis ? "Satış" : 
                              transaction.Type == ProductType.Takas ? "Takas" : "Bağış";
        
                var priceInfo = transaction.Type == ProductType.Satis 
                    ? $"\nFiyat: {transaction.Price:N2} ₺" 
                    : "";
        
                var exchangeInfo = transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                    ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})"
                    : "";
        
                await AddSystemMessageAsync(
                    conversation.ConversationId,
                    $"{typeIcon} [{transaction.ProductTitle} - {typeText}]\n📝 Görüşme başlatıldı{priceInfo}{exchangeInfo}"
                );

                Console.WriteLine($"✅ Yeni konuşma oluşturuldu: {conversation.ConversationId}");
                return ServiceResult<string>.SuccessResult(
                    conversation.ConversationId,
                    "Konuşma başlatıldı"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ StartConversationForTransaction hatası: {ex.Message}");
                return ServiceResult<string>.FailureResult("Konuşma başlatılamadı", ex.Message);
            }
        }

        // Yardımcı metod
        private async Task AddSystemMessageAsync(string conversationId, string messageText)
        {
            try
            {
                Console.WriteLine($"📝 AddSystemMessageAsync çağrıldı:");
                Console.WriteLine($"   ConversationId: {conversationId}");
                Console.WriteLine($"   Mesaj: {messageText}");

                var systemMessage = new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    SenderId = "system",
                    SenderName = "Sistem",
                    Content = messageText, // ✅ Text yerine Content
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsSystemMessage = true,
                    Type = MessageType.System // ✅ Type System olarak işaretlendi
                };

                Console.WriteLine($"   MessageId: {systemMessage.MessageId}");
                Console.WriteLine($"   Type: {systemMessage.Type}");
                Console.WriteLine($"   IsSystemMessage: {systemMessage.IsSystemMessage}");

                //  ÖNEMLİ: Mesajları ConversationId altında saklıyoruz
                var messagePath = $"{Constants.MessagesCollection}/{conversationId}/{systemMessage.MessageId}";
                Console.WriteLine($"   Firebase Path: {messagePath}");

                await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId) // ✅ Conversation ID'ye göre mesajları grupla
                    .Child(systemMessage.MessageId)
                    .PutAsync(systemMessage);

                Console.WriteLine($"✅ Sistem mesajı Firebase'e kaydedildi!");

                // Konuşmanın son mesajını güncelle
                var conversationRef = _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .Child(conversationId);

                var conversation = await conversationRef.OnceSingleAsync<Conversation>();
                if (conversation != null)
                {
                    conversation.LastMessage = messageText;
                    conversation.LastMessageTime = DateTime.UtcNow;
                    conversation.UpdatedAt = DateTime.UtcNow;
                    await conversationRef.PutAsync(conversation);
                    
                    Console.WriteLine($"✅ Conversation LastMessage güncellendi: {messageText}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Conversation bulunamadı: {conversationId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddSystemMessageAsync hatası: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"⚠️ Sistem mesajı eklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Simülasyon için OTP'yi Firebase'den alır
        /// ÖNEMLİ: Bu metod sadece test/simülasyon amaçlıdır!
        /// Gerçek üretim ortamında OTP'nin kullanıcıya gösterilmesi GÜVENLİK AÇIĞI oluşturur.
        /// Gerçek sistemde OTP sadece SMS/Email ile gönderilmeli, asla ekranda gösterilmemelidir.
        /// </summary>
        public async Task<ServiceResult<string>> GetSimulationOtpAsync(string paymentId)
        {
            try
            {
                var otpNode = await _firebaseClient
                    .Child(Constants.TempOtpsCollection)
                    .Child(paymentId)
                    .OnceSingleAsync<TempOtpModel>();

                if (otpNode != null && !string.IsNullOrEmpty(otpNode.Otp))
                {
                    // Sadece simülasyon için - Gerçek sistemde bunu YAPMAYIN!
                    System.Diagnostics.Debug.WriteLine($"⚠️ SIMÜLASYON: OTP alındı (PaymentId: {paymentId})");
                    return ServiceResult<string>.SuccessResult(otpNode.Otp, "OTP alındı");
                }
                
                return ServiceResult<string>.FailureResult("OTP bulunamadı");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetSimulationOtpAsync hatası: {ex.Message}");
                return ServiceResult<string>.FailureResult("OTP alınamadı", ex.Message);
            }
        }

        #endregion
    }
}