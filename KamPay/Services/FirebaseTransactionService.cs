using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamPay.Services;

namespace KamPay.Services
{
    public class FirebaseTransactionService : ITransactionService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IUserProfileService _userProfileService; // Puan için eklendi


        // Üst kısıma ekle (FirebaseTransactionService sınıfı içinde, constructor'dan önce)
        internal class TempOtpModel
        {
            public string Otp { get; set; }
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

                        // 🔥 KRİTİK: TAKAS İÇİN QR KODLARI OLUŞTUR
                        Console.WriteLine($"✅ Takas kabul edildi.  QR kodlar oluşturuluyor: {transactionId}");

                        // Satıcının ürünü için QR kod
                        var qrCode1 = await _qrCodeService.GenerateDeliveryQRCodeAsync(
                            transactionId,
                            transaction.ProductId,
                            transaction.ProductTitle,
                            transaction.SellerId,
                            transaction.BuyerId
                        );

                        // Alıcının ürünü için QR kod
                        var qrCode2 = await _qrCodeService.GenerateDeliveryQRCodeAsync(
                            transactionId,
                            transaction.OfferedProductId,
                            transaction.OfferedProductTitle,
                            transaction.BuyerId, // Teklif veren alıcı, bu ürünün sahibi
                            transaction.SellerId // Satıcı bu ürünü alacak
                        );

                        if (!qrCode1.Success || !qrCode2.Success)
                        {
                            Console.WriteLine($"❌ QR kod oluşturma hatası!");
                            return ServiceResult<Transaction>.FailureResult($"Takas kabul edildi ancak QR kodlar oluşturulamadı.");
                        }

                        Console.WriteLine($"✅ QR kodlar başarıyla oluşturuldu!");
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

        // --- BU METOTLAR HİZMET MODÜLÜ İÇİN KULLANILIR ---
        public async Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();
                if (transaction == null) return ServiceResult<PaymentDto>.FailureResult("İşlem bulunamadı.");

                if (transaction.PaymentStatus != PaymentStatus.Pending)
                    return ServiceResult<PaymentDto>.FailureResult("Bu işlem için ödeme zaten başlatılmış.");

                // Hizmet bedeli veya ürün bedeli (Hizmet için 'Price' kullanılıyor olabilir)
                var amount = transaction.QuotedPrice > 0 ? transaction.QuotedPrice : (transaction.Price > 0 ? transaction.Price : 0m);

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

                // Kart ödemesi ise OTP oluştur ve Firebase'e kaydet
                if (payment.Method == PaymentMethodType.CardSim)
                {
                    var otp = GenerateOtp();
                    await _firebaseClient
                        .Child(Constants.TempOtpsCollection)
                        .Child(payment.PaymentId)
                        .PutAsync(new TempOtpModel
                        {
                            Otp = otp,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(2)
                        });
                }

                // EFT ise banka referansı oluştur
                if (payment.Method == PaymentMethodType.BankTransferSim)
                {
                    payment.BankName = "Ziraat Bankası";
                    payment.BankReference = GenerateBankReference();
                }

                // İşlemi güncelle
                transaction.PaymentMethod = payment.Method;
                transaction.PaymentSimulationId = payment.PaymentId;
                transaction.PaymentStatus = PaymentStatus.Pending;
                await transactionNode.PutAsync(transaction);

                return ServiceResult<PaymentDto>.SuccessResult(payment, "Ödeme simülasyonu başlatıldı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<PaymentDto>.FailureResult("Simülasyon başlatılırken hata.", ex.Message);
            }
        }

        // --- BU METOTLAR HİZMET MODÜLÜ İÇİN KULLANILIR ---
        public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string transactionId, string paymentId, string? otp = null)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();
                if (transaction == null) return ServiceResult<bool>.FailureResult("İşlem bulunamadı.");

                if (transaction.PaymentSimulationId != paymentId)
                    return ServiceResult<bool>.FailureResult("Geçersiz ödeme kimliği.");

                if (transaction.PaymentMethod == PaymentMethodType.CardSim)
                {
                    var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(paymentId);
                    var saved = await otpNode.OnceSingleAsync<TempOtpModel>();
                    if (saved == null) return ServiceResult<bool>.FailureResult("OTP bulunamadı.");
                    if (DateTime.UtcNow > saved.ExpiresAt)
                        return ServiceResult<bool>.FailureResult("OTP süresi doldu.");
                    if (string.IsNullOrWhiteSpace(otp) || saved.Otp != otp)
                        return ServiceResult<bool>.FailureResult("OTP geçersiz.");
                }

                // Başarılı ödeme
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;

                // DİKKAT: Bu metot sadece ödemeyi onaylar.
                // Hizmet modülü, kendi akışında 'CompleteRequest' adımında
                // transaction.Status'ü 'Completed' yapmalıdır.
                // Eğer bu metot SATIŞ için kullanılsaydı, 'CompleteTransactionInternalAsync'i çağırmalıydı.
                // Ama HİZMET için kullanıldığından, sadece ödemeyi 'Paid' yapıyor.

                await transactionNode.PutAsync(transaction);

                // Bildirim gönder (Örn: Hizmet Sağlayıcıya)
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Title = "Ödeme Alındı!",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' hizmeti/ürünü için ödemesini tamamladı.",
                    Type = NotificationType.ProductSold, // Veya PaymentReceived
                    ActionUrl = nameof(Views.ServiceRequestsPage) // Veya OffersPage
                });

                return ServiceResult<bool>.SuccessResult(true, "Ödeme onaylandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ödeme onayında hata.", ex.Message);
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


        // --- YENİ METOT: Sadece SATIŞ Modülü İçin Hızlı Ödeme Tamamlama ---
        public async Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                // Kontroller
                if (transaction == null) return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");
                if (transaction.BuyerId != buyerId) return ServiceResult<Transaction>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");
                if (transaction.Status != TransactionStatus.Accepted) return ServiceResult<Transaction>.FailureResult("Bu işlem onaylanmamış veya zaten tamamlanmış.");
                if (transaction.PaymentStatus != PaymentStatus.Pending) return ServiceResult<Transaction>.FailureResult("Bu işlemin ödemesi zaten yapılmış veya başarısız olmuş.");

                // *** EN ÖNEMLİ KONTROL: Hizmet ile karışmaması için ***
                if (transaction.Type != ProductType.Satis) return ServiceResult<Transaction>.FailureResult("Bu işlem bir satış işlemi değil.");


                // Simülasyon: Ödeme başarılı kabul ediliyor. (Hızlı simülasyon)
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentMethod = PaymentMethodType.CardSim; // Hangi yöntemle olduğunu belirtelim
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // İşlemi tamamla (Internal metodu çağır: Ürünü satıldı yap, bildirim gönder, puan ekle)
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                // Hata durumunda ödemeyi 'Failed' olarak işaretle
                try
                {
                    var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                    var transaction = await transactionNode.OnceSingleAsync<Transaction>();
                    if (transaction != null && transaction.PaymentStatus == PaymentStatus.Pending)
                    {
                        transaction.PaymentStatus = PaymentStatus.Failed;
                        await transactionNode.PutAsync(transaction);
                    }
                }
                catch { /* Loglama */ }

                return ServiceResult<Transaction>.FailureResult("Ödeme tamamlanırken hata oluştu.", ex.Message);
            }
        }

        // --- YENİ PRIVATE METOT: Ortak Tamamlama İşlemleri (Satış, Bağış, Takas için) ---
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

        // --- YENİ METOT: BAĞIŞ Onaylama ---
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
    }
}