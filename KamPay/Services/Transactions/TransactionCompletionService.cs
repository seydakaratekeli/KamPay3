using CommunityToolkit.Mvvm.Input;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Payment; // âœ… EKLEME: Payment provider'larÄ± iÃ§in
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace KamPay.Services
{
    public partial class TransactionCompletionService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IUserProfileService _userProfileService;
        private readonly IPaymentProviderFactory _paymentProviderFactory; // âœ… EKLEME

        // âœ… YARDIMCI METOT: QR Kod nesnesini bellekte oluÅŸturur (DB'ye yazmaz)
        private DeliveryQRCode CreateDeliveryQRCodeModel(
            string transactionId,
            string productId,
            string productTitle,
            string giverId,
            string receiverId,
            int validityMinutes)
        {
            // GÃ¼venli rastgele kod Ã¼retimi
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            // Base64 string'i temizle ve kÄ±salt
            var secureCode = Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "")
                .Substring(0, 8).ToUpper();

            return new DeliveryQRCode
            {
                QRCodeId = Guid.NewGuid().ToString(),
                TransactionId = transactionId,
                ProductId = productId,
                ProductTitle = productTitle,
                QRCodeData = $"DELIVERY|{transactionId}|{productId}|{secureCode}", // QR iÃ§eriÄŸi formatÄ±
                VerificationPin = new Random().Next(100000, 999999).ToString(), // 6 haneli PIN
                SellerId = giverId,   // Teslim eden
                BuyerId = receiverId, // Teslim alan
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes),
                IsUsed = false,
                DeliveryStatus = DeliveryStatus.Pending
            };
        }
        // âœ… KRÄ°TÄ°K SABIT DEÄERLER
        private static class PaymentConstants
        {
            public const int OtpValidityMinutes = 2;
            public const int QRCodeValidityMinutes = 60;
            public const int OtpLength = 6;
            public const int OtpMinValue = 100000;
            public const int OtpMaxValue = 999999;
        }

        internal class TempOtpModel
        {
            public string Otp { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        // âœ… GÃœVENLÄ° OTP ÃœRETÄ°MÄ° - KRÄ°PTOGRAFÄ°K RANDOM
        private static string GenerateSecureOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            var otp = (randomNumber % 900000) + 100000; // 100000-999999 arasÄ±
            return otp.ToString("D6");
        }

        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";


        public TransactionCompletionService(
          INotificationService notificationService,
          IProductService productService,
          IQRCodeService qrCodeService,
          IUserProfileService userProfileService,
      FirebaseClient firebaseClient,
      IPaymentProviderFactory paymentProviderFactory) // âœ… EKLEME
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _productService = productService;
            _qrCodeService = qrCodeService;
            _userProfileService = userProfileService;
            _paymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory)); // âœ… EKLEME
        }


        public async Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept)
        {
            try
            {
                // Transaction verisini Ã§ek
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<Transaction>.SuccessResult(transaction, "Bu teklif zaten yanÄ±tlanmÄ±ÅŸ.");

                // 1. Transaction nesnesini gÃ¼ncelle (HENÃœZ KAYDETME!)
                transaction.Status = accept ? TransactionStatus.Accepted : TransactionStatus.Rejected;
                transaction.UpdatedAt = DateTime.UtcNow;

                // 2. REDDEDÄ°LDÄ°YSE: Tekli gÃ¼ncelleme yeterli (QR kod yok)
                if (!accept)
                {
                    await transactionNode.PutAsync(transaction);

                    // Bildirim gÃ¶nder (Kritik olmayan iÅŸlem, await ile beklenebilir veya fire-and-forget yapÄ±labilir)
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.BuyerId,
                        Type = NotificationType.OfferRejected,
                        Title = "Teklifin Reddedildi",
                        Message = $"'{transaction.SellerName}', teklifini reddetti.",
                        ActionUrl = nameof(Views.OffersPage)
                    });

                    return ServiceResult<Transaction>.SuccessResult(transaction, "Teklif reddedildi.");
                }

                // 3. KABUL EDÄ°LDÄ°YSE: ATOMÄ°K Ä°ÅLEM HAZIRLA
                // Firebase'e gÃ¶nderilecek tÃ¼m gÃ¼ncellemeleri tutacak sÃ¶zlÃ¼k
                var atomicUpdates = new Dictionary<string, object>();

                // a) Transaction gÃ¼ncellemesini ekle
                atomicUpdates[$"{Constants.TransactionsCollection}/{transactionId}"] = transaction;

                // b) ÃœrÃ¼nleri rezerve et (Bu kÄ±sÄ±m ProductService iÃ§inde olduÄŸu iÃ§in atomik yapÄ±ya dahil etmek zordur,
                // ancak Transaction Status 'Accepted' olduktan sonra UI zaten rezerve gÃ¶sterebilir.
                // Tam atomiklik iÃ§in ProductService mantÄ±ÄŸÄ±nÄ± buraya taÅŸÄ±manÄ±z gerekir ama ÅŸimdilik QR riskini Ã§Ã¶zÃ¼yoruz.)
                await _productService.MarkAsReservedAsync(transaction.ProductId, true);

                // c) TAKAS Ä°Ã‡Ä°N QR KODLARI OLUÅTUR
                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Takas iÃ§in Atomik QR kodlar hazÄ±rlanÄ±yor...");

                    // QR 1: SatÄ±cÄ± -> AlÄ±cÄ±
                    var qr1 = CreateDeliveryQRCodeModel(
                        transactionId,
                        transaction.ProductId,
                        transaction.ProductTitle ?? "Bilinmiyor",
                        transaction.SellerId,
                        transaction.BuyerId,
                        PaymentConstants.QRCodeValidityMinutes
                    );

                    // QR 2: AlÄ±cÄ± -> SatÄ±cÄ±
                    var qr2 = CreateDeliveryQRCodeModel(
                        transactionId,
                        transaction.OfferedProductId,
                        transaction.OfferedProductTitle ?? "Bilinmiyor",
                        transaction.BuyerId,
                        transaction.SellerId,
                        PaymentConstants.QRCodeValidityMinutes
                    );

                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr1.QRCodeId}"] = qr1;
                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr2.QRCodeId}"] = qr2;
                }
                // d) BAÄIÅ Ä°Ã‡Ä°N QR KOD OLUÅTUR
                else if (transaction.Type == ProductType.Bagis || transaction.Type == ProductType.Satis)
                {
                    var qr = CreateDeliveryQRCodeModel(
                        transactionId,
                        transaction.ProductId,
                        transaction.ProductTitle ?? "Bilinmiyor",
                        transaction.SellerId,
                        transaction.BuyerId,
                        PaymentConstants.QRCodeValidityMinutes
                    );

                    // âœ… DOÄRU KOD:
                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr.QRCodeId}"] = qr;
                }

                // 4. ğŸ”¥ KRÄ°TÄ°K NOKTA: TÃœM VERÄ°YÄ° TEK SEFERDE GÃ–NDER (PATCH)
                // Root dizine Patch atarak farklÄ± path'leri aynÄ± anda gÃ¼ncelleriz.
                await _firebaseClient.Child("/").PatchAsync(atomicUpdates);

                KamPay.Helpers.AppLogger.DebugLog("âœ… Atomik iÅŸlem baÅŸarÄ±yla tamamlandÄ± (Transaction + QR Kodlar).");

                // 5. Bildirimleri gÃ¶nder (Veri tutarlÄ±lÄ±ÄŸÄ±nÄ± etkilemediÄŸi iÃ§in iÅŸlemden sonra yapÄ±labilir)
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.OfferAccepted,
                    Title = "Teklifin Kabul Edildi!",
                    Message = $"'{transaction.SellerName}', teklifini kabul etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (transaction.Type == ProductType.Satis)
                {
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.BuyerId,
                        Type = NotificationType.OfferAccepted,
                        Title = "Ã–deme YapÄ±n",
                        Message = $"'{transaction.ProductTitle}' iÃ§in Ã¶deme yapabilirsiniz.",
                        ActionUrl = nameof(Views.PaymentPage)
                    });
                }

                return ServiceResult<Transaction>.SuccessResult(transaction, "Ä°ÅŸlem baÅŸarÄ±yla onaylandÄ±.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ RespondToOfferAsync Atomik Hata: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem sÄ±rasÄ±nda hata oluÅŸtu.", ex.Message);
            }
        }

        // SatÄ±ÅŸ iÅŸlemi iÃ§in simÃ¼lasyonlu Ã¶deme baÅŸlatma
        public async Task<ServiceResult<PaymentDto>> StartSalePaymentAsync(string transactionId, string method)
        {
            // 1. AÄŸ KontrolÃ¼ (DevOps StandartÄ±)
            if (!NetworkHelper.HasInternetConnection())
                return ServiceResult<PaymentDto>.FailureResult("Ä°nternet baÄŸlantÄ±sÄ± yok.", "LÃ¼tfen baÄŸlantÄ±nÄ±zÄ± kontrol edin.");

            // 2. HÄ±z SÄ±nÄ±rÄ± KontrolÃ¼ (Spam Engelleme)
            var limitCheck = RateLimiters.ApiCall.CheckLimit(transactionId);
            if (!limitCheck.IsAllowed)
                return ServiceResult<PaymentDto>.FailureResult(limitCheck.Message);

            // Mevcut CreatePaymentSimulationAsync metodunu Ã§aÄŸÄ±rarak Ã¶demeyi baÅŸlatÄ±r
            return await CreatePaymentSimulationAsync(transactionId, method);
        }

        /// <summary>
        /// Ã–deme simÃ¼lasyonunu baÅŸlatÄ±r
        /// Bu metod hem Ã¼rÃ¼n satÄ±ÅŸlarÄ± hem de hizmet Ã¶demeleri iÃ§in kullanÄ±lÄ±r
        /// </summary>
        /// <param name="transactionId">Ä°ÅŸlem ID'si</param>
        /// <param name="method">Ã–deme yÃ¶ntemi: "cardsim" veya "banktransfersim"</param>
        /// <returns>PaymentDto iÃ§eren ServiceResult</returns>
        public async Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ” CreatePaymentSimulationAsync baÅŸladÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   TransactionId: {transactionId}");
                KamPay.Helpers.AppLogger.DebugLog($"   Method: {method}");

                // âœ… GÃœVENLÄ°K: TransactionId kontrolÃ¼
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âŒ TransactionId boÅŸ!");
                    return ServiceResult<PaymentDto>.FailureResult("Ä°ÅŸlem ID'si bulunamadÄ±.");
                }

                var isServicePayment = transactionId.StartsWith("service_");
                KamPay.Helpers.AppLogger.DebugLog($"   Ä°ÅŸlem Tipi: {(isServicePayment ? "HÄ°ZMET" : "ÃœRÃœN")}");

                // 1. Transaction'Ä± al ve doÄŸrula
                ChildQuery transactionNode;
                object transaction;

                if (isServicePayment)
                {
                    var requestId = transactionId.Replace("service_", "");
                    transactionNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                    transaction = await transactionNode.OnceSingleAsync<ServiceRequest>();

                    if (transaction == null)
                        return ServiceResult<PaymentDto>.FailureResult("Hizmet talebi bulunamadÄ±.");
                }
                else
                {
                    transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                    transaction = await transactionNode.OnceSingleAsync<Transaction>();

                    if (transaction == null)
                        return ServiceResult<PaymentDto>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");
                        
                    var productTransaction = transaction as Transaction;

                    // GÃ¼venlik kontrolleri
                    if (productTransaction?.PaymentStatus != PaymentStatus.Pending)
                        return ServiceResult<PaymentDto>.FailureResult("Bu iÅŸlem iÃ§in Ã¶deme zaten baÅŸlatÄ±lmÄ±ÅŸ.");

                    if (productTransaction?.Status != TransactionStatus.Accepted)
                        return ServiceResult<PaymentDto>.FailureResult("Ä°ÅŸlem henÃ¼z satÄ±cÄ± tarafÄ±ndan onaylanmamÄ±ÅŸ.");

                    if (productTransaction?.IsNegotiating == true)
                        return ServiceResult<PaymentDto>.FailureResult("PazarlÄ±k devam ediyor. Ã–nce fiyat Ã¼zerinde anlaÅŸmanÄ±z gerekiyor.");
                }

                // 2. Ã–denecek tutarÄ± belirle
                decimal amount;
                if (isServicePayment)
                {
                    var serviceRequest = transaction as ServiceRequest;
                    if (serviceRequest?.PaymentStatus != ServicePaymentStatus.None &&
                        serviceRequest?.PaymentStatus != ServicePaymentStatus.Failed)
                        return ServiceResult<PaymentDto>.FailureResult("Bu talep iÃ§in Ã¶deme zaten baÅŸlatÄ±lmÄ±ÅŸ.");

                    amount = serviceRequest?.QuotedPrice ?? serviceRequest?.Price ?? 0;
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    amount = productTransaction?.QuotedPrice > 0 ? productTransaction.QuotedPrice : productTransaction?.Price ?? 0;
                }

                // âœ… 3. OCP: Factory'den doÄŸru provider'Ä± al (SWITCH YOK!)
                IPaymentProvider provider;
                try
                {
                    provider = _paymentProviderFactory.GetProvider(method);
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Provider seÃ§ildi: {provider.ProviderName}");
                }
                catch (NotSupportedException ex)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âŒ Desteklenmeyen Ã¶deme yÃ¶ntemi: {method}");
                    return ServiceResult<PaymentDto>.FailureResult(ex.Message);
                }

                // âœ… 4. Provider ile Ã¶deme baÅŸlat (Polimorfizm - OCP)
                var paymentResult = await provider.InitiatePaymentAsync(transactionId, amount);
                
                if (!paymentResult.Success || paymentResult.Data == null)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âŒ Ã–deme baÅŸlatÄ±lamadÄ±: {paymentResult.Message}");
                    return paymentResult;
                }

                var payment = paymentResult.Data;
                KamPay.Helpers.AppLogger.DebugLog($"âœ… PaymentDto oluÅŸturuldu:");
                KamPay.Helpers.AppLogger.DebugLog($"   PaymentId: {payment.PaymentId}");
                KamPay.Helpers.AppLogger.DebugLog($"   Method: {payment.Method}");
                KamPay.Helpers.AppLogger.DebugLog($"   Amount: {payment.Amount:N2}â‚º");

                // 5. Transaction'Ä± gÃ¼ncelle
                if (isServicePayment)
                {
                    var serviceRequest = transaction as ServiceRequest;
                    serviceRequest.PaymentMethod = payment.Method;
                    serviceRequest.PaymentSimulationId = payment.PaymentId;
                    serviceRequest.PaymentStatus = ServicePaymentStatus.Initiated;
                    await transactionNode.PutAsync(serviceRequest);
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    productTransaction.PaymentMethod = payment.Method;
                    productTransaction.PaymentSimulationId = payment.PaymentId;
                    productTransaction.PaymentStatus = PaymentStatus.Pending;
                    await transactionNode.PutAsync(productTransaction);
                }

                KamPay.Helpers.AppLogger.DebugLog($"âœ…âœ…âœ… CreatePaymentSimulationAsync BAÅARILI!");
                return ServiceResult<PaymentDto>.SuccessResult(payment, paymentResult.Message);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒâŒâŒ CreatePaymentSimulationAsync HATA: {ex.Message}");
                return ServiceResult<PaymentDto>.FailureResult("Ã–deme baÅŸlatÄ±lÄ±rken hata oluÅŸtu.", ex.Message);
            }
        }
        /// <summary>
        /// Ã–deme simÃ¼lasyonunu onaylar ve iÅŸlemi tamamlar
        /// </summary>
        /// <param name="transactionId">Ä°ÅŸlem ID'si</param>
        /// <param name="paymentId">Ã–deme ID'si</param>
        /// <param name="otp">OTP kodu (kart Ã¶demeleri iÃ§in zorunlu)</param>
        /// <returns>BaÅŸarÄ± durumunu iÃ§eren ServiceResult</returns>
        public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string transactionId, string paymentId, string? otp = null)
        {
            try
            {
                // âœ… DÃœZELTÄ°LDÄ°: Service prefix kontrolÃ¼nÃ¼ SADECE OKUMA iÃ§in kullan
                var isServicePayment = transactionId.StartsWith("service_");

                // 1. Transaction'Ä± al ve doÄŸrula
                ChildQuery transactionNode;
                object transaction;

                if (isServicePayment)
                {
                    // HÄ°ZMET iÃ§in ServiceRequest collection'Ä±ndan al
                    var requestId = transactionId.Replace("service_", "");
                    transactionNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                    transaction = await transactionNode.OnceSingleAsync<ServiceRequest>();

                    if (transaction == null)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ ServiceRequest bulunamadÄ±: {requestId}");
                        return ServiceResult<bool>.FailureResult("Hizmet talebi bulunamadÄ±.");
                    }
                }
                else
                {
                    // âœ… FIX: ÃœRÃœN iÃ§in Transactions collection'Ä±ndan al (service_ prefix EKLEME!)
                    transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                    transaction = await transactionNode.OnceSingleAsync<Transaction>();

                    if (transaction == null)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ Transaction bulunamadÄ±: {transactionId}");
                        return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");
                    }
                }

                // 2. âœ… OCP: Provider'Ä± bul ve doÄŸrulama yap
                PaymentMethodType paymentMethod = PaymentMethodType.None;
                
                if (isServicePayment)
                {
                    var serviceRequest = transaction as ServiceRequest;
                    paymentMethod = serviceRequest?.PaymentMethod ?? PaymentMethodType.None;
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    paymentMethod = productTransaction?.PaymentMethod ?? PaymentMethodType.None;
                }

                // âœ… OCP: Factory'den provider al
                IPaymentProvider provider;
                try
                {
                    provider = _paymentProviderFactory.GetProvider(paymentMethod.ToString());
                }
                catch (NotSupportedException)
                {
                    return ServiceResult<bool>.FailureResult($"Desteklenmeyen Ã¶deme yÃ¶ntemi: {paymentMethod}");
                }

                // âœ… OCP: Provider'Ä±n kendi doÄŸrulama metodunu Ã§aÄŸÄ±r (Polimorfizm)
                var validationResult = await provider.ConfirmPaymentAsync(paymentId, otp);
                
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // 3. Ã–deme durumunu gÃ¼ncelle
                // âœ… FIX: Type-safe property assignment
                if (isServicePayment)
                {
                    var serviceRequest = transaction as ServiceRequest;
                    if (serviceRequest != null)
                    {
                        serviceRequest.PaymentStatus = ServicePaymentStatus.Paid;
                        serviceRequest.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    if (productTransaction != null)
                    {
                        productTransaction.PaymentStatus = PaymentStatus.Paid;
                        productTransaction.PaymentCompletedAt = DateTime.UtcNow;
                    }
                }

                // 4. Ä°ÅLEM TÄ°PÄ°NE GÃ–RE TAMAMLAMA (Mevcut kod aynÄ± kalÄ±yor)
                
                // âœ… HÄ°ZMET Ã–DEMESÄ°: TransactionId "service_" ile baÅŸlÄ±yorsa
                if (transactionId.StartsWith("service_"))
                {
                    KamPay.Helpers.AppLogger.DebugLog($"ğŸ› ï¸ Hizmet Ã¶demesi tespit edildi: {transactionId}");
                    
                    // ServiceRequest ID'sini al (service_ prefix'ini kaldÄ±r)
                    var requestId = transactionId.Replace("service_", "");
                    
                    try
                    {
                        // ServiceRequest'i gÃ¼ncelle
                        var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                        var serviceRequest = await requestNode.OnceSingleAsync<ServiceRequest>();
                        
                        if (serviceRequest != null)
                        {
                            // ServiceRequest'i tamamla
                            serviceRequest.Status = ServiceRequestStatus.Completed;
                            serviceRequest.PaymentStatus = ServicePaymentStatus.Paid;
                            serviceRequest.UpdatedAt = DateTime.UtcNow;
                            
                            await requestNode.PutAsync(serviceRequest);
                            
                            KamPay.Helpers.AppLogger.DebugLog($"âœ… ServiceRequest tamamlandÄ±: {requestId}");
                            
                            // PuanlarÄ± ver
                            await _userProfileService.AddPointsForAction(serviceRequest.ProviderId, UserAction.ProvideService);
                            await _userProfileService.AddPointsForAction(serviceRequest.RequesterId, UserAction.ReceiveService);
                            
                            KamPay.Helpers.AppLogger.DebugLog($"âœ… Puanlar verildi");
                            
                            // Bildirimleri gÃ¶nder
                            await _notificationService.CreateNotificationAsync(new Notification
                            {
                                UserId = serviceRequest.ProviderId,
                                Type = NotificationType.ServiceCompleted,
                                Title = "Hizmet Ãœcreti AlÄ±ndÄ±!",
                                Message = $"{serviceRequest.RequesterName}, '{serviceRequest.ServiceTitle}' hizmeti iÃ§in Ã¶demeyi tamamladÄ±.",
                                ActionUrl = nameof(Views.ServiceRequestsPage)
                            });
                            
                            await _notificationService.CreateNotificationAsync(new Notification
                            {
                                UserId = serviceRequest.RequesterId,
                                Type = NotificationType.ServiceCompleted,
                                Title = "Hizmet TamamlandÄ±!",
                                Message = $"'{serviceRequest.ServiceTitle}' hizmeti iÃ§in Ã¶deme baÅŸarÄ±yla tamamlandÄ±.",
                                ActionUrl = nameof(Views.ServiceRequestsPage)
                            });
                            
                            KamPay.Helpers.AppLogger.DebugLog($"âœ… Bildirimler gÃ¶nderildi");
                        }
                        else
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ ServiceRequest bulunamadÄ±: {requestId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âŒ ServiceRequest gÃ¼ncelleme hatasÄ±: {ex.Message}");
                        // Hata olsa bile Ã¶deme tamamlanmÄ±ÅŸ sayÄ±lÄ±r
                    }
                    
                    // Transaction'Ä± da gÃ¼ncelle
                    await transactionNode.PutAsync(transaction);
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Hizmet Ã¶demesi tamamlandÄ±");
                }
                // SATIÅ: ÃœrÃ¼nÃ¼ kapat, puan ver, bildirim gÃ¶nder
                else
                {
                    var productTransaction = transaction as Transaction;
                    if (productTransaction != null && productTransaction.Type == ProductType.Satis)
                    {
                        var completeResult = await CompleteTransactionInternalAsync(productTransaction);
                        if (!completeResult.Success)
                            return ServiceResult<bool>.FailureResult("Ã–deme alÄ±ndÄ± ancak iÅŸlem tamamlanÄ±rken hata oluÅŸtu: " + completeResult.Message);
                        
                        KamPay.Helpers.AppLogger.DebugLog($"âœ… SatÄ±ÅŸ iÅŸlemi tamamlandÄ±. TransactionId: {transactionId}");
                    }
                    // DÄ°ÄER TÄ°PLER (Takas, BaÄŸÄ±ÅŸ): Sadece Ã¶deme durumunu gÃ¼ncelle
                    else if (productTransaction != null)
                    {
                        await transactionNode.PutAsync(productTransaction);
                        KamPay.Helpers.AppLogger.DebugLog($"âœ… Ã–deme tamamlandÄ±. TransactionId: {transactionId}, Type: {productTransaction.Type}");
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Ã–deme onaylandÄ± ve iÅŸlem tamamlandÄ±.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ ConfirmPaymentSimulationAsync hatasÄ±: {ex.Message}");
                // Teknik hatalarÄ± kullanÄ±cÄ± dostu mesajlara dÃ¶nÃ¼ÅŸtÃ¼r
                return ServiceResult<bool>.FailureResult("Ã–deme onayÄ±nda hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }
        // --- BU METOT HÄ°ZMET MODÃœLÃœ Ä°Ã‡Ä°NDÄ°R ---
        public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string transactionId)
        {
            // Bu metot, HÄ°ZMET MODÃœLÃœ'nÃ¼n kullandÄ±ÄŸÄ± karmaÅŸÄ±k simÃ¼lasyon akÄ±ÅŸÄ±dÄ±r.
            var payment = await CreatePaymentSimulationAsync(transactionId, "CardSim");
            if (!payment.Success) return ServiceResult<bool>.FailureResult(payment.Message);

            await Task.Delay(1500); // SimÃ¼lasyon gecikmesi

            string otp = null;
            if (payment.Data.Method == PaymentMethodType.CardSim)
            {
                var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(payment.Data.PaymentId);
                var savedOtp = await otpNode.OnceSingleAsync<TempOtpModel>();
                otp = savedOtp?.Otp;
            }

            var confirm = await ConfirmPaymentSimulationAsync(transactionId, payment.Data.PaymentId, otp: otp);

            // HÄ°ZMET modÃ¼lÃ¼ akÄ±ÅŸÄ± burada bitiyor (Ã¶deme tamamlandÄ±). 
            // 'ServiceRequest'in 'Completed' yapÄ±lmasÄ± 'FirebaseServiceSharingService' iÃ§inde yÃ¶netiliyor.
            return confirm;
        }


        [RelayCommand]
        private async Task CompletePaymentAsync(Transaction transaction)
        {
            if (transaction == null) return;

            // SatÄ±ÅŸ YÃ¶nlendirmesi
            if (transaction.Type == ProductType.Satis && transaction.Status == TransactionStatus.Accepted)
            {
                var navigationParameter = new Dictionary<string, object> { { "Transaction", transaction } };
                await Shell.Current.GoToAsync(nameof(PaymentPage), navigationParameter);
            }
            // âœ… TAKAS VE BAÄIÅ YÃ–NLENDÄ°RMESÄ° (EKLENMELÄ°)
            else if ((transaction.Type == ProductType.Takas || transaction.Type == ProductType.Bagis) &&
                      transaction.Status == TransactionStatus.Accepted)
            {
                // QR Kod SayfasÄ±na YÃ¶nlendir
                await Shell.Current.GoToAsync($"QRCodeDisplayPage?transactionId={transaction.TransactionId}");
            }
        }
        // âœ… ITransactionService interface'ini implement et
        public async Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");

                if (transaction.BuyerId != buyerId)
                    return ServiceResult<Transaction>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok.");

                if (transaction.PaymentStatus != PaymentStatus.Pending)
                    return ServiceResult<Transaction>.FailureResult("Ã–deme zaten tamamlanmÄ±ÅŸ veya beklemede deÄŸil.");

                // Ã–deme tamamlandÄ± olarak iÅŸaretle
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // Ä°ÅŸlemi tamamla (puan ver, Ã¼rÃ¼nÃ¼ kapat, bildirim gÃ¶nder)
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Ã–deme tamamlanamadÄ±.", ex.Message);
            }
        }

        // Ortak Tamamlama Ä°ÅŸlemleri (SatÄ±ÅŸ, BaÄŸÄ±ÅŸ, Takas iÃ§in) 
        private async Task<ServiceResult<Transaction>> CompleteTransactionInternalAsync(Transaction transaction)
        {
            try
            {
                // âœ… SATIÅ iÃ§in Ã¶deme kontrolÃ¼
                if (transaction.Type == ProductType.Satis)
                {
                    if (transaction.PaymentStatus != PaymentStatus.Paid)
                    {
                        return ServiceResult<Transaction>.FailureResult(
                            "Ã–deme Gerekli", 
                            "Bu satÄ±ÅŸ iÅŸlemini tamamlamak iÃ§in Ã¶nce Ã¶deme yapÄ±lmalÄ±dÄ±r."
                        );
                    }
                }

                // 1. Transaction durumunu 'Completed' yap
                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _firebaseClient.Child(Constants.TransactionsCollection).Child(transaction.TransactionId).PutAsync(transaction);

                // âœ… PERFORMANS: Paralel iÅŸlemler iÃ§in task listesi oluÅŸtur
                var parallelTasks = new List<Task>();

                // 2. ÃœrÃ¼nÃ¼ 'SatÄ±ldÄ±' olarak iÅŸaretle (IsActive=false yapar)
                parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.ProductId));

                // 3. EÄŸer Takas ise, teklif edilen Ã¼rÃ¼nÃ¼ de 'SatÄ±ldÄ±' yap
                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.OfferedProductId));
                }

                // 4. Bildirimleri paralel gÃ¶nder
                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "BaÄŸÄ±ÅŸ TamamlandÄ±!" : (transaction.Type == ProductType.Takas ? "Takas TamamlandÄ±!" : "ÃœrÃ¼nÃ¼n SatÄ±ldÄ±!"),
                    Message = $"'{transaction.ProductTitle}' iÃ§in '{transaction.BuyerName}' ile olan iÅŸleminiz tamamlandÄ±.",
                    ActionUrl = nameof(Views.OffersPage)
                }));

                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "BaÄŸÄ±ÅŸ Teslim AlÄ±ndÄ±!" : (transaction.Type == ProductType.Takas ? "Takas TamamlandÄ±!" : "SatÄ±n Alma TamamlandÄ±!"),
                    Message = $"'{transaction.ProductTitle}' Ã¼rÃ¼nÃ¼ iÃ§in '{transaction.SellerName}' ile olan iÅŸleminiz tamamlandÄ±.",
                    ActionUrl = nameof(Views.OffersPage)
                }));

                // 5. PuanlarÄ± paralel ekle
                if (transaction.Type == ProductType.Bagis)
                {
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation));
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation));
                }
                else // SatÄ±ÅŸ veya Takas
                {
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction));
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction));
                }

                // âœ… TÃœM Ä°ÅLEMLERÄ° PARALEL Ã‡ALIÅTIR
                await Task.WhenAll(parallelTasks);

                return ServiceResult<Transaction>.SuccessResult(transaction, "Ä°ÅŸlem baÅŸarÄ±yla tamamlandÄ±.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"Hata - CompleteTransactionInternalAsync: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem tamamlanÄ±rken bir hata oluÅŸtu.", ex.Message);
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
                    Type = product.Type,
                    SellerId = product.UserId,
                    SellerName = product.UserName,
                    BuyerId = buyer.UserId,
                    BuyerName = buyer.FullName,
                    Status = TransactionStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    Price = product.Price,
                    // âœ… KRÄ°TÄ°K FÄ°X: PazarlÄ±ksÄ±z satÄ±ÅŸ iÃ§in QuotedPrice'Ä± baÅŸlangÄ±Ã§ta set et
                    // EÄŸer alÄ±cÄ± pazarlÄ±k yapmadan direkt talep gÃ¶nderdiyse, bu fiyat kilitlenir
                    QuotedPrice = product.Price,
                    // âœ… PazarlÄ±k baÅŸlangÄ±Ã§ durumu: false (henÃ¼z teklif yok)
                    IsNegotiating = false,
                    NegotiationRoundCount = 0,
                    // âœ… DÃœZELTÄ°LDÄ°: Buyer ve Seller fotoÄŸraflarÄ± da eklenmeli
                    BuyerPhotoUrl = buyer.ProfileImageUrl ?? "default_avatar.png",
                    SellerPhotoUrl = product.UserPhotoUrl ?? "default_avatar.png"
                };

                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);

                // SatÄ±cÄ±ya bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = product.UserId,
                    Type = NotificationType.NewOffer,
                    Title = product.Type == ProductType.Bagis ? "Yeni BaÄŸÄ±ÅŸ Talebi!" : (product.Type == ProductType.Takas ? "Yeni Takas Teklifi!" : "Yeni SatÄ±ÅŸ Talebi!"),
                    Message = $"{buyer.FullName}, '{product.Title}' Ã¼rÃ¼nun iÃ§in bir {(product.Type == ProductType.Bagis ? "talep" : "teklif")} gÃ¶nderdi.",
                    ActionUrl = nameof(Views.OffersPage) // Gelen Teklifler sayfasÄ±
                });

                return ServiceResult<Transaction>.SuccessResult(transaction, "Ä°steÄŸiniz baÅŸarÄ±yla gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Ä°stek oluÅŸturulamadÄ±.", ex.Message);
            }
        }

        public async Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer)
        {
            try
            {
                // Teklif edilen Ã¼rÃ¼nÃ¼n bilgilerini al
                var offeredProductResult = await _productService.GetProductByIdAsync(offeredProductId);
                if (!offeredProductResult.Success || offeredProductResult.Data == null)
                {
                    return ServiceResult<Transaction>.FailureResult("Teklif edilen Ã¼rÃ¼n bulunamadÄ±.");
                }
                var offeredProduct = offeredProductResult.Data;


                // âœ… FIX: Takas iÃ§in tÃ¼m Ã¼rÃ¼n bilgilerini sakla
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
                    OfferedProductThumbnailUrl = offeredProduct.ThumbnailUrl, // âœ… EKLENDI
                    OfferMessage = message,
                    PaymentStatus = PaymentStatus.Pending, // Takasta Ã¶deme 'N/A' (Uygulanamaz) olabilir, ama 'Pending' kalmasÄ± da sorun yaratmaz.
                    BuyerPhotoUrl = buyer.ProfileImageUrl ?? "default_avatar.png",
                    SellerPhotoUrl = product.UserPhotoUrl ?? "default_avatar.png",
                    IsNegotiating = false,
                    NegotiationRoundCount = 0
                };

                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);


                // SatÄ±cÄ±ya bildirim gÃ¶nder
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = product.UserId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Bir Takas Teklifin Var!",
                    Message = $"{buyer.FullName}, '{product.Title}' Ã¼rÃ¼nÃ¼n iÃ§in '{offeredProduct.Title}' Ã¼rÃ¼nÃ¼nÃ¼ teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });


                return ServiceResult<Transaction>.SuccessResult(transaction, "Takas teklifiniz baÅŸarÄ±yla gÃ¶nderildi.");
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Teklif oluÅŸturulamadÄ±.", ex.Message);
            }
        }

        // BAÄIÅ Onaylama 
        public async Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                // Kontroller
                if (transaction == null) return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");
                if (transaction.BuyerId != buyerId) return ServiceResult<Transaction>.FailureResult("Bu iÅŸlemi yapmaya yetkiniz yok.");
                if (transaction.Status != TransactionStatus.Accepted) return ServiceResult<Transaction>.FailureResult("Bu iÅŸlem onaylanmamÄ±ÅŸ veya zaten tamamlanmÄ±ÅŸ.");
                if (transaction.Type != ProductType.Bagis) return ServiceResult<Transaction>.FailureResult("Bu iÅŸlem bir baÄŸÄ±ÅŸ iÅŸlemi deÄŸil.");

                // âœ… BaÄŸÄ±ÅŸta Ã¶deme olmadÄ±ÄŸÄ± iÃ§in PaymentStatus'Ã¼ 'Paid' yapmak,
                // Converter'Ä±n (SimulatePaymentButtonVisibilityConverter) butonu tekrar gÃ¶stermemesi iÃ§in Ã¶nemlidir.
                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                // âœ… SatÄ±ÅŸ modÃ¼lÃ¼ iÃ§in yazdÄ±ÄŸÄ±mÄ±z iÃ§ metodu TEKRAR KULLANIYORUZ.
                // CompleteTransactionInternalAsync iÃ§inde PaymentStatus kontrolÃ¼ var,
                // ama baÄŸÄ±ÅŸ iÃ§in zaten Paid yapÄ±ldÄ±, sorun Ã§Ä±kmaz.
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("BaÄŸÄ±ÅŸ onaylanÄ±rken hata oluÅŸtu.", ex.Message);
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
                KamPay.Helpers.AppLogger.DebugLog($"HATA - GetIncomingOffersAsync: {ex.Message}");
                return ServiceResult<List<Transaction>>.FailureResult("Gelen teklifler alÄ±namadÄ±.", ex.Message);
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
                KamPay.Helpers.AppLogger.DebugLog($"HATA - GetMyOffersAsync: {ex.Message}");
                return ServiceResult<List<Transaction>>.FailureResult("GÃ¶nderilen teklifler alÄ±namadÄ±.", ex.Message);
            }
        }

        #region ğŸ’° SATIÅ PAZARLIK METODLARI

        /// <summary>
        /// SatÄ±ÅŸ iÃ§in fiyat teklifi (AlÄ±cÄ±)
        /// </summary>
        public async Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId,
            bool isInitialRequest = false)
        {
            try
            {
                if (proposedPrice <= 0)
                    return ServiceResult<bool>.FailureResult("Fiyat 0'dan bÃ¼yÃ¼k olmalÄ±");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // Sadece alÄ±cÄ± teklif verebilir
                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                // Sadece satÄ±ÅŸ iÅŸlemlerinde
                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlem satÄ±ÅŸ deÄŸil");

                // Sadece Pending durumunda
                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem artÄ±k beklemede deÄŸil");

                // âœ… PazarlÄ±k devam edebilir mi kontrol et (detaylÄ± mesaj)
                var canContinueCheck = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinueCheck.IsValid)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ PazarlÄ±k limiti: {canContinueCheck.ErrorMessage}");
                    return ServiceResult<bool>.FailureResult(canContinueCheck.ErrorMessage);
                }

                // âœ… Teklif fiyatÄ±nÄ± doÄŸrula (orijinal fiyatÄ±n %50'sinden az olamaz)
                var priceCheck = NegotiationRules.ValidateProposedPrice(
                    proposedPrice, 
                    transaction.Price);
                
                if (!priceCheck.IsValid)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ GeÃ§ersiz teklif: {priceCheck.ErrorMessage}");
                    return ServiceResult<bool>.FailureResult(priceCheck.ErrorMessage);
                }

                // GÃ¼ncelle
                transaction.ProposedPriceByBuyer = proposedPrice;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // Ä°lk teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // âœ… PazarlÄ±k turu sayÄ±sÄ±nÄ± artÄ±r (SADECE ALICI TEKLÄ°F VERDÄ°ÄÄ°NDE)
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Fiyat teklifi kaydedildi: {proposedPrice:N2}â‚º (Tur: {transaction.NegotiationRoundCount})");

                // Bildirim baÅŸlÄ±ÄŸÄ± ve mesajÄ±nÄ± belirle
                string notificationTitle = isInitialRequest ? "ğŸ’° SatÄ±n Alma Ä°steÄŸi" : "ğŸ’° Yeni Fiyat Teklifi";
                string notificationMessage = isInitialRequest
                    ? $"{transaction.BuyerName}, '{transaction.ProductTitle}' Ã¼rÃ¼nÃ¼nÃ¼ satÄ±n almak istiyor. ({proposedPrice:N2}â‚º)"
                    : $"{transaction.BuyerName}, '{transaction.ProductTitle}' iÃ§in {proposedPrice:N2}â‚º teklif etti. (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})";

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.NewOffer,
                    Title = notificationTitle,
                    Message = notificationMessage,
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    string messageText = isInitialRequest
                        ? $"ğŸ’° [{transaction.ProductTitle} - SatÄ±ÅŸ]\nAlÄ±cÄ± bu Ã¼rÃ¼nÃ¼ liste fiyatÄ±ndan ({proposedPrice:N2}â‚º) satÄ±n almak istiyor."
                        : $"ğŸ’° [{transaction.ProductTitle} - SatÄ±ÅŸ]\nAlÄ±cÄ±: {proposedPrice:N2}â‚º teklif etti.";

                    await AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        messageText,
                        proposedPrice,
                        currentUserId,
                        transaction.BuyerName,
                        transaction.TransactionId,
                        "Propose"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat teklifiniz gÃ¶nderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ ProposePriceForSale hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gÃ¶nderilemedi", ex.Message);
            }
        }

       
        /// SatÄ±ÅŸ iÃ§in karÅŸÄ± teklif (SatÄ±cÄ±)
       
        public async Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(
            string transactionId,
            decimal counterOffer,
            string currentUserId)
        {
            try
            {
                if (counterOffer <= 0)
                    return ServiceResult<bool>.FailureResult("Fiyat 0'dan bÃ¼yÃ¼k olmalÄ±");

                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // Sadece satÄ±cÄ± karÅŸÄ± teklif verebilir
                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlem satÄ±ÅŸ deÄŸil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem artÄ±k beklemede deÄŸil");

                // âœ… PazarlÄ±k devam edebilir mi kontrol et (detaylÄ± mesaj)
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ PazarlÄ±k limiti: {canContinue.ErrorMessage}");
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);
                }

                // âœ… KarÅŸÄ± teklifi doÄŸrula (orijinal fiyattan yÃ¼ksek olamaz)
                var counterValidation = NegotiationRules.ValidateCounterOffer(
                    counterOffer, 
                    transaction.Price,
                    transaction.ProposedPriceByBuyer);
                
                if (!counterValidation.IsValid)
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ GeÃ§ersiz karÅŸÄ± teklif: {counterValidation.ErrorMessage}");
                    return ServiceResult<bool>.FailureResult(counterValidation.ErrorMessage);
                }

                transaction.CounterOfferBySeller = counterOffer;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // Ä°lk karÅŸÄ± teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // âœ… DÃœZELTÄ°LDÄ°: SatÄ±cÄ± karÅŸÄ± teklif verirken de tur sayÄ±sÄ±nÄ± artÄ±r (tutarlÄ±lÄ±k iÃ§in)
                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… KarÅŸÄ± teklif kaydedildi: {counterOffer:N2}â‚º (Tur: {transaction.NegotiationRoundCount})");

                // Bildirim
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.NewOffer,
                    Title = "ğŸ”„ KarÅŸÄ± Teklif AlÄ±ndÄ±",
                    Message = $"'{transaction.ProductTitle}' iÃ§in karÅŸÄ± teklif: {counterOffer:N2}â‚º (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        $"ğŸ”„ [{transaction.ProductTitle} - SatÄ±ÅŸ]\nSatÄ±cÄ±: {counterOffer:N2}â‚º karÅŸÄ± teklif etti.",
                        counterOffer,
                        currentUserId,
                        transaction.SellerName,
                        transaction.TransactionId,
                        "CounterOffer"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"KarÅŸÄ± teklifiniz gÃ¶nderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ SendCounterOfferForSale hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gÃ¶nderilemedi", ex.Message);
            }
        }

        #endregion

        #region ğŸ”„ TAKAS PAZARLIK METODLARI

       
        /// Takas iÃ§in ek nakit teklifi (Talep Eden)
       
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
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // Sadece talep eden
                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlem takas deÄŸil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem artÄ±k beklemede deÄŸil");

                // PazarlÄ±k devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Ek nakit teklifini doÄŸrula
                var cashValidation = NegotiationRules.ValidateAdditionalCash(additionalCash);
                
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.AdditionalCashByRequester = additionalCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // Ä°lk teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // PazarlÄ±k turu sayÄ±sÄ±nÄ± artÄ±r
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
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' takasÄ± iÃ§in {additionalCash:N2}â‚º ek Ã¶deme teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : ÃœrÃ¼n bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} â†” {transaction.ProductTitle})"
                        : "";
    
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"ğŸ”„ [{transaction.ProductTitle} - Takas]\nğŸ’° Ek Nakit Teklifi: {additionalCash:N2} â‚º{exchangeInfo}\n" +
                        $"ğŸ“Š PazarlÄ±k Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"Nakit teklifiniz gÃ¶nderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ ProposeAdditionalCash hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gÃ¶nderilemedi", ex.Message);
            }
        }

       
        /// Takas iÃ§in karÅŸÄ± nakit teklifi (Sahip)
       
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
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // Sadece sahip
                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu iÅŸlem takas deÄŸil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem artÄ±k beklemede deÄŸil");

                // PazarlÄ±k devam edebilir mi kontrol et
                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                // Ek nakit karÅŸÄ± teklifini doÄŸrula
                var cashValidation = NegotiationRules.ValidateAdditionalCash(counterCash);
                
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.CounterCashByOwner = counterCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;
                
                // Ä°lk karÅŸÄ± teklif ise baÅŸlangÄ±Ã§ tarihini ayarla
                if (!transaction.NegotiationStartedAt.HasValue)
                {
                    transaction.NegotiationStartedAt = DateTime.UtcNow;
                }
                
                // âœ… DÃœZELTÄ°LDÄ°: Takas iÃ§in de sahip karÅŸÄ± teklif verirken tur sayÄ±sÄ±nÄ± artÄ±r
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
                    Title = "KarÅŸÄ± Teklif AlÄ±ndÄ±",
                    Message = $"'{transaction.ProductTitle}' takasÄ± iÃ§in karÅŸÄ± teklif: {counterCash:N2}â‚º (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})",
                    ActionUrl = nameof(Views.OffersPage)
                });

                //  : ÃœrÃ¼n bilgisi eklendi
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var requesterOffer = transaction.AdditionalCashByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {transaction.AdditionalCashByRequester:N2} â‚º)" 
                        : "";
            
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} â†” {transaction.ProductTitle})"
                        : "";
            
                    await AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"ğŸ”„ [{transaction.ProductTitle} - Takas]\nğŸ’° KarÅŸÄ± Teklif: {counterCash:N2} â‚º{requesterOffer}\n" +
                        $"ğŸ“Š PazarlÄ±k Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"KarÅŸÄ± teklifiniz gÃ¶nderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ SendCounterCashOffer hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gÃ¶nderilemedi", ex.Message);
            }
        }

        #endregion

        #region ğŸ¤ ORTAK PAZARLIK METODLARI

       
        /// AnlaÅŸÄ±lan fiyat/tutarÄ± kabul et (Hem SatÄ±ÅŸ Hem Takas)
       
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
                    return ServiceResult<bool>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // Yetki: her iki taraf da kabul edebilir
                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (!transaction.IsNegotiating)
                    return ServiceResult<bool>.FailureResult("Aktif pazarlÄ±k yok");

                // AnlaÅŸÄ±lan tutarÄ± belirle
                decimal agreedAmount = transaction.AgreedAmount;

                // âœ… FÄ°X: QuotedPrice'Ä± gÃ¼ncelle AMA Price'Ä± (orijinal fiyatÄ±) KORUMA
                if (transaction.Type == ProductType.Satis)
                {
                    transaction.QuotedPrice = agreedAmount;
                    // âŒ YANLIÅ: transaction.Price = agreedAmount; 
                    // Price orijinal fiyatÄ± korumalÄ±, QuotedPrice anlaÅŸÄ±lan fiyatÄ± iÃ§ermeli
                }
                else if (transaction.Type == ProductType.Takas)
                {
                    transaction.QuotedPrice = agreedAmount;
                }

                // âœ… YENÄ°: PazarlÄ±ÄŸÄ± sonlandÄ±r ve otomatik olarak Status = Accepted yap
                // Ä°Å KURALI: SatÄ±cÄ± zaten karÅŸÄ± teklif yaptÄ±, alÄ±cÄ± kabul edince anlaÅŸma tamamlanÄ±r.
                // Her iki taraf da fiyat Ã¼zerinde anlaÅŸtÄ±ÄŸÄ± iÃ§in satÄ±cÄ±nÄ±n tekrar onayÄ±na gerek yoktur.
                transaction.IsNegotiating = false;
                
                // âœ… YENÄ°: AlÄ±cÄ± kabul edince otomatik olarak Status = Accepted yap
                transaction.Status = TransactionStatus.Accepted;
                
                // NegotiationRules helper'Ä±nÄ± kullanarak detaylÄ± Ã¶zet oluÅŸtur
                var acceptedBy = transaction.BuyerId == currentUserId ? "AlÄ±cÄ±" : "SatÄ±cÄ±";
                decimal? originalPrice = transaction.Type == ProductType.Satis 
                    ? transaction.Price 
                    : null;
                
                var negotiationSummary = NegotiationRules.GetNegotiationSummary(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt,
                    originalPrice,
                    agreedAmount);
                
                negotiationSummary += $"ğŸ‘¤ Kabul Eden: {acceptedBy}\n";
                negotiationSummary += $"âœ… Durum: OnaylandÄ± - Ã–deme yapÄ±labilir\n";
                
                transaction.NegotiationNotes += (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;
                transaction.UpdatedAt = DateTime.UtcNow;

                // âœ… Firebase'e kaydet
                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                // âœ… YENÄ°: SatÄ±cÄ±ya bildirim gÃ¶nder - Ã–deme bekleniyor
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.TransactionUpdate,
                    Title = "ğŸ’° PazarlÄ±k TamamlandÄ±",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' iÃ§in {transaction.QuotedPrice:N2}â‚º teklifinizi kabul etti. Ã–deme bekleniyor.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                // âœ… YENÄ°: AlÄ±cÄ±ya da bildirim - ArtÄ±k Ã¶deme yapabilir
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.TransactionUpdate,
                    Title = "âœ… Fiyat OnaylandÄ±",
                    Message = $"'{transaction.ProductTitle}' iÃ§in {transaction.QuotedPrice:N2}â‚º fiyatÄ±nda anlaÅŸtÄ±nÄ±z. ArtÄ±k Ã¶deme yapabilirsiniz.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                // KonuÅŸmaya sistem mesajÄ± ekle
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        $"âœ… [{transaction.ProductTitle} - SatÄ±ÅŸ]\nTeklifi kabul etti.\nAnlaÅŸÄ±lan Fiyat: {agreedAmount:N2} â‚º",
                        agreedAmount,
                        currentUserId,
                        currentUserId == transaction.BuyerId ? transaction.BuyerName : transaction.SellerName,
                        transaction.TransactionId,
                        "Accept"
                    );
                }

                return ServiceResult<bool>.SuccessResult(true, $"âœ… {agreedAmount:N2}â‚º fiyatÄ±nda anlaÅŸtÄ±nÄ±z! ArtÄ±k Ã¶deme yapabilirsiniz.");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ AcceptNegotiatedPrice hatasÄ±: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Kabul iÅŸlemi baÅŸarÄ±sÄ±z", ex.Message);
            }
        }

       
        /// Transaction iÃ§in konuÅŸma baÅŸlat
       
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
                    return ServiceResult<string>.FailureResult("Ä°ÅŸlem bulunamadÄ±");

                // KullanÄ±cÄ±nÄ±n iÅŸleme dahil olduÄŸunu doÄŸrula
                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<string>.FailureResult("Bu iÅŸleme eriÅŸim yetkiniz yok");

                //  Ã–NCELÄ°KLE: Mevcut ConversationId'yi kontrol et
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    // KonuÅŸmanÄ±n halaaktif olduÄŸunu doÄŸrula
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(transaction.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                        {
                            KamPay.Helpers.AppLogger.DebugLog($"âœ… Mevcut konuÅŸma bulundu: {transaction.ConversationId}");
                            return ServiceResult<string>.SuccessResult(
                                transaction.ConversationId,
                                "Mevcut konuÅŸma bulundu"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Mevcut konuÅŸma kontrol hatasÄ±: {ex.Message}");
                    }
                }

                //  KullanÄ±cÄ±lar arasÄ±nda mevcut konuÅŸma var mÄ± kontrol et
                var otherUserId = transaction.BuyerId == currentUserId 
                    ? transaction.SellerId 
                    : transaction.BuyerId;

                // Ã–nce User1Id ile kontrol et
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
                    // Mevcut konuÅŸma bulundu - transaction'a kaydet
                    transaction.ConversationId = existingWithOtherUser.Key;
                    transaction.HasActiveConversation = true;
                    
                    await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(transactionId)
                        .PutAsync(transaction);
                    
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Mevcut kullanÄ±cÄ± konuÅŸmasÄ± bulundu: {existingWithOtherUser.Key}");
                    return ServiceResult<string>.SuccessResult(
                        existingWithOtherUser.Key,
                        "Mevcut konuÅŸma bulundu"
                    );
                }

                // Yeni konuÅŸma oluÅŸtur
                var conversation = new Conversation
                {
                    ConversationId = Guid.NewGuid().ToString(),
                    User1Id = currentUserId,
                    User1Name = transaction.BuyerId == currentUserId ? transaction.BuyerName : transaction.SellerName,
                    User1PhotoUrl = transaction.BuyerId == currentUserId ? transaction.BuyerPhotoUrl : transaction.SellerPhotoUrl,
                    User2Id = otherUserId,
                    User2Name = transaction.BuyerId == currentUserId ? transaction.SellerName : transaction.BuyerName,
                    User2PhotoUrl = transaction.BuyerId == currentUserId ? transaction.SellerPhotoUrl : transaction.BuyerPhotoUrl,
                    LastMessage = "GÃ¶rÃ¼ÅŸme baÅŸlatÄ±ldÄ±",
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

                //  : Sistem mesajÄ±na Ã¼rÃ¼n bilgisi eklendi
                var typeIcon = transaction.Type == ProductType.Satis ? "ğŸ“¦" : 
                              transaction.Type == ProductType.Takas ? "ğŸ”„" : "ğŸ";
                var typeText = transaction.Type == ProductType.Satis ? "SatÄ±ÅŸ" : 
                              transaction.Type == ProductType.Takas ? "Takas" : "BaÄŸÄ±ÅŸ";
        
                var priceInfo = transaction.Type == ProductType.Satis 
                    ? $"\nFiyat: {transaction.Price:N2} â‚º" 
                    : "";
        
                var exchangeInfo = transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                    ? $"\n(Takas: {transaction.OfferedProductTitle} â†” {transaction.ProductTitle})"
                    : "";
        
                await AddSystemMessageAsync(
                    conversation.ConversationId,
                    $"{typeIcon} [{transaction.ProductTitle} - {typeText}]\nğŸ“ GÃ¶rÃ¼ÅŸme baÅŸlatÄ±ldÄ±{priceInfo}{exchangeInfo}"
                );

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Yeni konuÅŸma oluÅŸturuldu: {conversation.ConversationId}");
                return ServiceResult<string>.SuccessResult(
                    conversation.ConversationId,
                    "KonuÅŸma baÅŸlatÄ±ldÄ±"
                );
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ StartConversationForTransaction hatasÄ±: {ex.Message}");
                return ServiceResult<string>.FailureResult("KonuÅŸma baÅŸlatÄ±lamadÄ±", ex.Message);
            }
        }

        // YardÄ±mcÄ± metod
        private async Task AddSystemMessageAsync(string conversationId, string messageText)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“ AddSystemMessageAsync Ã§aÄŸrÄ±ldÄ±:");
                KamPay.Helpers.AppLogger.DebugLog($"   ConversationId: {conversationId}");
                KamPay.Helpers.AppLogger.DebugLog($"   Mesaj: {messageText}");

                var systemMessage = new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    SenderId = "system",
                    SenderName = "Sistem",
                    Content = messageText, // âœ… Text yerine Content
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsSystemMessage = true,
                    Type = MessageType.System // âœ… Type System olarak iÅŸaretlendi
                };

                KamPay.Helpers.AppLogger.DebugLog($"   MessageId: {systemMessage.MessageId}");
                KamPay.Helpers.AppLogger.DebugLog($"   Type: {systemMessage.Type}");
                KamPay.Helpers.AppLogger.DebugLog($"   IsSystemMessage: {systemMessage.IsSystemMessage}");

                //  Ã–NEMLÄ°: MesajlarÄ± ConversationId altÄ±nda saklÄ±yoruz
                var messagePath = $"{Constants.MessagesCollection}/{conversationId}/{systemMessage.MessageId}";
                KamPay.Helpers.AppLogger.DebugLog($"   Firebase Path: {messagePath}");

                await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId) // âœ… Conversation ID'ye gÃ¶re mesajlarÄ±grupla
                    .Child(systemMessage.MessageId)
                    .PutAsync(systemMessage);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Sistem mesajÄ± Firebase'e kaydedildi!");

                // KonuÅŸmanÄ±n son mesajÄ±nÄ± gÃ¼ncelle
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
                    
                    KamPay.Helpers.AppLogger.DebugLog($"âœ… Conversation LastMessage gÃ¼ncellendi: {messageText}");
                }
                else
                {
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Conversation bulunamadÄ±: {conversationId}");
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ AddSystemMessageAsync hatasÄ±: {ex.Message}");
                KamPay.Helpers.AppLogger.DebugLog($"   StackTrace: {ex.StackTrace}");
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ Sistem mesajÄ± eklenemedi: {ex.Message}");
            }
        }

        // PazarlÄ±k mesajÄ± eklemek iÃ§in yardÄ±mcÄ± metod
        private async Task AddNegotiationMessageAsync(string conversationId, string messageText, decimal proposedPrice, string senderId, string senderName, string transactionId, string action)
        {
            try
            {
                KamPay.Helpers.AppLogger.DebugLog($"ğŸ“ AddNegotiationMessageAsync Ã§aÄŸrÄ±ldÄ±:");
                
                var negotiationMessage = new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    SenderId = senderId,
                    SenderName = senderName,
                    Content = messageText,
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsSystemMessage = false,
                    Type = MessageType.Negotiation,
                    ProposedPrice = proposedPrice,
                    RelatedTransactionId = transactionId,
                    NegotiationAction = action
                };

                await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId)
                    .Child(negotiationMessage.MessageId)
                    .PutAsync(negotiationMessage);

                // KonuÅŸmanÄ±n son mesajÄ±nÄ± gÃ¼ncelle
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
                }
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ PazarlÄ±k mesajÄ± eklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// SimÃ¼lasyon iÃ§in OTP'yi Firebase'den alÄ±r
        /// Ã–NEMLÄ°: Bu metod sadece test/simÃ¼lasyon amaÃ§lÄ±dÄ±r!
        /// GerÃ§ek Ã¼retim ortamÄ±nda OTP'nin kullanÄ±cÄ±ya gÃ¶sterilmesi GÃœVENLÄ°K AÃ‡IÄI oluÅŸturur.
        /// GerÃ§ek sistemde OTP sadece SMS/Email ile gÃ¶nderilmeli, asla ekranda gÃ¶sterilmemelidir.
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
                    // Sadece simÃ¼lasyon iÃ§in - GerÃ§ek sistemde bunu YAPMAYIN!
                    KamPay.Helpers.AppLogger.DebugLog($"âš ï¸ SIMÃœLASYON: OTP alÄ±ndÄ± (PaymentId: {paymentId})");
                    return ServiceResult<string>.SuccessResult(otpNode.Otp, "OTP alÄ±ndÄ±");
                }
                
                return ServiceResult<string>.FailureResult("OTP bulunamadÄ±");
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ GetSimulationOtpAsync hatasÄ±: {ex.Message}");
                return ServiceResult<string>.FailureResult("OTP alÄ±namadÄ±", ex.Message);
            }
        }

        #endregion
    
        public async Task<ServiceResult<Transaction>> CompleteManualSaleAsync(string transactionId, string sellerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem bulunamadÄ±.");

                if (transaction.SellerId != sellerId)
                    return ServiceResult<Transaction>.FailureResult("Sadece satÄ±cÄ± iÅŸlemi tamamlayabilir.");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<Transaction>.FailureResult("Bu senaryo sadece satÄ±ÅŸ iÅŸlemleri iÃ§indir.");

                if (transaction.Status != TransactionStatus.Accepted && transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<Transaction>.FailureResult("Ä°ÅŸlem bu durumdayken tamamlanamaz.");

                // Ä°ÅŸlemi tamamla (puan ver, Ã¼rÃ¼nÃ¼ kapat, bildirim gÃ¶nder)
                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("SatÄ±ÅŸ tamamlanamadÄ±.", ex.Message);
            }
        }
}
}

