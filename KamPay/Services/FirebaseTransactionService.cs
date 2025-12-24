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


        //  Sadece SATIŞ Modülü İçin Hızlı Ödeme Tamamlama
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

                // Güncelle
                transaction.ProposedPriceByBuyer = proposedPrice;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

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

                transaction.CounterOfferBySeller = counterOffer;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

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

                transaction.AdditionalCashByRequester = additionalCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

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

                transaction.CounterCashByOwner = counterCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

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
                transaction.NegotiationNotes = $"Anlaşılan tutar: {agreedAmount:N2}₺ - {DateTime.UtcNow:dd.MM.yyyy HH:mm}";
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

        #endregion
    }
}