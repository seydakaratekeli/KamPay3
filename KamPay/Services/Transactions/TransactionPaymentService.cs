using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Payment;
using KamPay.Views;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Sorumluluk: Ödeme simülasyonu başlatma, onaylama ve OTP yönetimi.
    /// Hem ürün hem de hizmet ödemelerini destekler.
    /// </summary>
    public partial class TransactionPaymentService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IUserProfileService _userProfileService;
        private readonly IPaymentProviderFactory _paymentProviderFactory;
        private readonly TransactionCompletionService _completionService;

        // ─────────────────────────────────────────────
        //  SABİT DEĞERLER
        // ─────────────────────────────────────────────
        private static class PaymentConstants
        {
            public const int OtpValidityMinutes = 2;
        }

        internal class TempOtpModel
        {
            public string Otp { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        // ─────────────────────────────────────────────
        //  CRYPTOGRAPHIC HELPERS
        // ─────────────────────────────────────────────
        private static string GenerateSecureOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            return ((randomNumber % 900000) + 100000).ToString("D6");
        }

        private string GenerateBankReference() => $"BTX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}";

        public TransactionPaymentService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IUserProfileService userProfileService,
            IPaymentProviderFactory paymentProviderFactory,
            TransactionCompletionService completionService)
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _userProfileService = userProfileService;
            _paymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));
            _completionService = completionService;
        }

        // ─────────────────────────────────────────────
        //  ÖDEME İŞLEMLERİ
        // ─────────────────────────────────────────────

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

                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                return await _completionService.CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Ödeme tamamlanamadı.", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> SetPaymentMethodAsCashAsync(string transactionId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("İşlem bulunamadı.");

                transaction.PaymentMethod = PaymentMethodType.Cash;
                transaction.PaymentStatus = PaymentStatus.Paid;
                await transactionNode.PutAsync(transaction);

                return ServiceResult<bool>.SuccessResult(true, "Ödeme yöntemi elden (nakit) olarak ayarlandı.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Ödeme yöntemi güncellenemedi.", ex.Message);
            }
        }

        /// <summary>
        /// Ödeme simülasyonunu başlatır.
        /// Hem ürün satışları hem de hizmet ödemeleri için kullanılır.
        /// </summary>
        public async Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method)
        {
            try
            {
                AppLogger.DebugLog($"🔍 CreatePaymentSimulationAsync başladı: TransactionId={transactionId}, Method={method}");

                if (string.IsNullOrWhiteSpace(transactionId))
                    return ServiceResult<PaymentDto>.FailureResult("İşlem ID'si bulunamadı.");

                var isServicePayment = transactionId.StartsWith("service_");
                AppLogger.DebugLog($"   İşlem Tipi: {(isServicePayment ? "HİZMET" : "ÜRÜN")}");

                ChildQuery transactionNode;
                object transaction;

                if (isServicePayment)
                {
                    var requestId = transactionId.Replace("service_", "");
                    transactionNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                    transaction = await transactionNode.OnceSingleAsync<ServiceRequest>();

                    if (transaction == null)
                        return ServiceResult<PaymentDto>.FailureResult("Hizmet talebi bulunamadı.");
                }
                else
                {
                    transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                    transaction = await transactionNode.OnceSingleAsync<Transaction>();

                    if (transaction == null)
                        return ServiceResult<PaymentDto>.FailureResult("İşlem bulunamadı.");

                    var productTransaction = transaction as Transaction;

                    if (productTransaction?.PaymentStatus != PaymentStatus.Pending)
                        return ServiceResult<PaymentDto>.FailureResult("Bu işlem için ödeme zaten başlatılmış.");

                    if (productTransaction?.Status != TransactionStatus.Accepted)
                        return ServiceResult<PaymentDto>.FailureResult("İşlem henüz satıcı tarafından onaylanmamış.");

                    if (productTransaction?.IsNegotiating == true)
                        return ServiceResult<PaymentDto>.FailureResult("Pazarlık devam ediyor. Önce fiyat üzerinde anlaşmanız gerekiyor.");
                }

                // Ödenecek tutarı belirle
                decimal amount;
                if (isServicePayment)
                {
                    var serviceRequest = transaction as ServiceRequest;
                    if (serviceRequest?.PaymentStatus != ServicePaymentStatus.None &&
                        serviceRequest?.PaymentStatus != ServicePaymentStatus.Failed)
                        return ServiceResult<PaymentDto>.FailureResult("Bu talep için ödeme zaten başlatılmış.");

                    amount = serviceRequest?.QuotedPrice ?? serviceRequest?.Price ?? 0;
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    amount = productTransaction?.QuotedPrice > 0 ? productTransaction.QuotedPrice : productTransaction?.Price ?? 0;
                }

                // OCP: Factory'den doğru provider'ı al
                IPaymentProvider provider;
                try
                {
                    provider = _paymentProviderFactory.GetProvider(method);
                    AppLogger.DebugLog($"✅ Provider seçildi: {provider.ProviderName}");
                }
                catch (NotSupportedException ex)
                {
                    AppLogger.DebugLog($"❌ Desteklenmeyen ödeme yöntemi: {method}");
                    return ServiceResult<PaymentDto>.FailureResult(ex.Message);
                }

                var paymentResult = await provider.InitiatePaymentAsync(transactionId, amount);

                if (!paymentResult.Success || paymentResult.Data == null)
                {
                    AppLogger.DebugLog($"❌ Ödeme başlatılamadı: {paymentResult.Message}");
                    return paymentResult;
                }

                var payment = paymentResult.Data;

                // Transaction güncelle
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

                AppLogger.DebugLog($"✅ CreatePaymentSimulationAsync BAŞARILI! PaymentId={payment.PaymentId}");
                return ServiceResult<PaymentDto>.SuccessResult(payment, paymentResult.Message);
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ CreatePaymentSimulationAsync HATA: {ex.Message}");
                return ServiceResult<PaymentDto>.FailureResult("Ödeme başlatılırken hata oluştu.", ex.Message);
            }
        }

        /// <summary>Ödeme simülasyonunu başlatır (Ağ + hız limiti kontrollü)</summary>
        public async Task<ServiceResult<PaymentDto>> StartSalePaymentAsync(string transactionId, string method)
        {
            if (!NetworkHelper.HasInternetConnection())
                return ServiceResult<PaymentDto>.FailureResult("İnternet bağlantısı yok.", "Lütfen bağlantınızı kontrol edin.");

            var limitCheck = RateLimiters.ApiCall.CheckLimit(transactionId);
            if (!limitCheck.IsAllowed)
                return ServiceResult<PaymentDto>.FailureResult(limitCheck.Message);

            return await CreatePaymentSimulationAsync(transactionId, method);
        }

        /// <summary>Ödeme simülasyonunu onaylar ve işlemi tamamlar</summary>
        public async Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(
            string transactionId, string paymentId, string? otp = null)
        {
            try
            {
                var isServicePayment = transactionId.StartsWith("service_");

                ChildQuery transactionNode;
                object transaction;

                if (isServicePayment)
                {
                    var requestId = transactionId.Replace("service_", "");
                    transactionNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                    transaction = await transactionNode.OnceSingleAsync<ServiceRequest>();

                    if (transaction == null)
                        return ServiceResult<bool>.FailureResult("Hizmet talebi bulunamadı.");
                }
                else
                {
                    transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                    transaction = await transactionNode.OnceSingleAsync<Transaction>();

                    if (transaction == null)
                        return ServiceResult<bool>.FailureResult("İşlem bulunamadı.");
                }

                // OCP: Provider'ı bul ve doğrulama yap
                PaymentMethodType paymentMethod;
                if (isServicePayment)
                    paymentMethod = (transaction as ServiceRequest)?.PaymentMethod ?? PaymentMethodType.None;
                else
                    paymentMethod = (transaction as Transaction)?.PaymentMethod ?? PaymentMethodType.None;

                IPaymentProvider provider;
                try
                {
                    provider = _paymentProviderFactory.GetProvider(paymentMethod.ToString());
                }
                catch (NotSupportedException)
                {
                    return ServiceResult<bool>.FailureResult($"Desteklenmeyen ödeme yöntemi: {paymentMethod}");
                }

                var validationResult = await provider.ConfirmPaymentAsync(paymentId, otp);
                if (!validationResult.Success)
                    return validationResult;

                // Ödeme durumunu güncelle
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

                // İşlem tipine göre tamamlama
                if (isServicePayment)
                {
                    AppLogger.DebugLog($"🛠️ Hizmet ödemesi tespit edildi: {transactionId}");
                    var requestId = transactionId.Replace("service_", "");

                    try
                    {
                        var requestNode = _firebaseClient.Child(Constants.ServiceRequestsCollection).Child(requestId);
                        var serviceRequest = await requestNode.OnceSingleAsync<ServiceRequest>();

                        if (serviceRequest != null)
                        {
                            serviceRequest.Status = ServiceRequestStatus.Completed;
                            serviceRequest.PaymentStatus = ServicePaymentStatus.Paid;
                            serviceRequest.UpdatedAt = DateTime.UtcNow;
                            await requestNode.PutAsync(serviceRequest);

                            await _userProfileService.AddPointsForAction(serviceRequest.ProviderId, UserAction.ProvideService);
                            await _userProfileService.AddPointsForAction(serviceRequest.RequesterId, UserAction.ReceiveService);

                            await _notificationService.CreateNotificationAsync(new Notification
                            {
                                UserId = serviceRequest.ProviderId,
                                Type = NotificationType.ServiceCompleted,
                                Title = "Hizmet Ücreti Alındı!",
                                Message = $"{serviceRequest.RequesterName}, '{serviceRequest.ServiceTitle}' hizmeti için ödemeyi tamamladı.",
                                ActionUrl = nameof(Views.ServiceRequestsPage)
                            });

                            await _notificationService.CreateNotificationAsync(new Notification
                            {
                                UserId = serviceRequest.RequesterId,
                                Type = NotificationType.ServiceCompleted,
                                Title = "Hizmet Tamamlandı!",
                                Message = $"'{serviceRequest.ServiceTitle}' hizmeti için ödeme başarıyla tamamlandı.",
                                ActionUrl = nameof(Views.ServiceRequestsPage)
                            });

                            AppLogger.DebugLog($"✅ ServiceRequest tamamlandı: {requestId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.DebugLog($"❌ ServiceRequest güncelleme hatası: {ex.Message}");
                    }

                    await transactionNode.PutAsync(transaction);
                }
                else
                {
                    var productTransaction = transaction as Transaction;
                    if (productTransaction != null && productTransaction.Type == ProductType.Satis)
                    {
                        var completeResult = await _completionService.CompleteTransactionInternalAsync(productTransaction);
                        if (!completeResult.Success)
                            return ServiceResult<bool>.FailureResult("Ödeme alındı ancak işlem tamamlanırken hata oluştu: " + completeResult.Message);

                        AppLogger.DebugLog($"✅ Satış işlemi tamamlandı. TransactionId: {transactionId}");
                    }
                    else if (productTransaction != null)
                    {
                        await transactionNode.PutAsync(productTransaction);
                        AppLogger.DebugLog($"✅ Ödeme tamamlandı. TransactionId: {transactionId}, Type: {productTransaction.Type}");
                    }
                }

                return ServiceResult<bool>.SuccessResult(true, "Ödeme onaylandı ve işlem tamamlandı.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ ConfirmPaymentSimulationAsync hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Ödeme onayında hata.", NetworkHelper.GetUserFriendlyErrorMessage(ex));
            }
        }

        /// <summary>Hizmet modülü için simülasyonlu ödeme ve tamamlama</summary>
        public async Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string transactionId)
        {
            var payment = await CreatePaymentSimulationAsync(transactionId, "CardSim");
            if (!payment.Success) return ServiceResult<bool>.FailureResult(payment.Message);

            await Task.Delay(1500);

            string otp = null;
            if (payment.Data.Method == PaymentMethodType.CardSim)
            {
                var otpNode = _firebaseClient.Child(Constants.TempOtpsCollection).Child(payment.Data.PaymentId);
                var savedOtp = await otpNode.OnceSingleAsync<TempOtpModel>();
                otp = savedOtp?.Otp;
            }

            return await ConfirmPaymentSimulationAsync(transactionId, payment.Data.PaymentId, otp: otp);
        }

        /// <summary>
        /// Simülasyon için OTP'yi Firebase'den alır.
        /// ÖNEMLİ: Sadece test/simülasyon amaçlıdır!
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
                    AppLogger.DebugLog($"⚠️ SİMÜLASYON: OTP alındı (PaymentId: {paymentId})");
                    return ServiceResult<string>.SuccessResult(otpNode.Otp, "OTP alındı");
                }

                return ServiceResult<string>.FailureResult("OTP bulunamadı");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ GetSimulationOtpAsync hatası: {ex.Message}");
                return ServiceResult<string>.FailureResult("OTP alınamadı", ex.Message);
            }
        }
    }
}
