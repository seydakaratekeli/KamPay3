using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Services.Products;

namespace KamPay.Services
{
    /// <summary>
    /// Sorumluluk: Transaction tamamlama işlemleri.
    /// Bağış onaylama, manuel satış tamamlama ve ortak iç tamamlama helper'ı.
    /// </summary>
    public partial class TransactionCompletionService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IUserProfileService _userProfileService;

        public TransactionCompletionService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            IProductService productService,
            IUserProfileService userProfileService)
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _productService = productService;
            _userProfileService = userProfileService;
        }

        // ─────────────────────────────────────────────
        //  BAĞIŞ ONAYLAMA
        // ─────────────────────────────────────────────

        public async Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null) return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");
                if (transaction.BuyerId != buyerId) return ServiceResult<Transaction>.FailureResult("Bu işlemi yapmaya yetkiniz yok.");
                if (transaction.Status != TransactionStatus.Accepted) return ServiceResult<Transaction>.FailureResult("Bu işlem onaylanmamış veya zaten tamamlanmış.");
                if (transaction.Type != ProductType.Bagis) return ServiceResult<Transaction>.FailureResult("Bu işlem bir bağış işlemi değil.");

                transaction.PaymentStatus = PaymentStatus.Paid;
                transaction.PaymentCompletedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await transactionNode.PutAsync(transaction);

                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Bağış onaylanırken hata oluştu.", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  MANİEL SATIŞ TAMAMLAMA
        // ─────────────────────────────────────────────

        public async Task<ServiceResult<Transaction>> CompleteManualSaleAsync(string transactionId, string sellerId)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");

                if (transaction.SellerId != sellerId)
                    return ServiceResult<Transaction>.FailureResult("Sadece satıcı işlemi tamamlayabilir.");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<Transaction>.FailureResult("Bu senaryo sadece satış işlemleri içindir.");

                if (transaction.Status != TransactionStatus.Accepted && transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<Transaction>.FailureResult("İşlem bu durumdayken tamamlanamaz.");

                return await CompleteTransactionInternalAsync(transaction);
            }
            catch (Exception ex)
            {
                return ServiceResult<Transaction>.FailureResult("Satış tamamlanamadı.", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  İÇ TAMAMLAMA HELPER (Tüm servisler kullanır)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Ortak tamamlama işlemleri: Satış, Bağış, Takas için.
        /// Ürünü kapat, puan ver, bildirim gönder.
        /// </summary>
        public async Task<ServiceResult<Transaction>> CompleteTransactionInternalAsync(Transaction transaction)
        {
            try
            {
                if (transaction.Type == ProductType.Satis)
                {
                    if (transaction.PaymentStatus != PaymentStatus.Paid)
                    {
                        return ServiceResult<Transaction>.FailureResult(
                            "Ödeme Gerekli",
                            "Bu satış işlemini tamamlamak için önce ödeme yapılmalıdır.");
                    }
                }

                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _firebaseClient.Child(Constants.TransactionsCollection)
                    .Child(transaction.TransactionId).PutAsync(transaction);

                var parallelTasks = new List<Task>();

                parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.ProductId));

                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                    parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.OfferedProductId));

                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "Bağış Tamamlandı!" :
                           (transaction.Type == ProductType.Takas ? "Takas Tamamlandı!" : "Ürünün Satıldı!"),
                    Message = $"'{transaction.ProductTitle}' için '{transaction.BuyerName}' ile olan işleminiz tamamlandı.",
                    ActionUrl = nameof(Views.OffersPage)
                }));

                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "Bağış Teslim Alındı!" :
                           (transaction.Type == ProductType.Takas ? "Takas Tamamlandı!" : "Satın Alma Tamamlandı!"),
                    Message = $"'{transaction.ProductTitle}' ürünü için '{transaction.SellerName}' ile olan işleminiz tamamlandı.",
                    ActionUrl = nameof(Views.OffersPage)
                }));

                if (transaction.Type == ProductType.Bagis)
                {
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.SellerId, UserAction.MakeDonation));
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.ReceiveDonation));
                }
                else
                {
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.SellerId, UserAction.CompleteTransaction));
                    parallelTasks.Add(_userProfileService.AddPointsForAction(transaction.BuyerId, UserAction.CompleteTransaction));
                }

                await Task.WhenAll(parallelTasks);

                return ServiceResult<Transaction>.SuccessResult(transaction, "İşlem başarıyla tamamlandı.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Hata - CompleteTransactionInternalAsync: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("İşlem tamamlanırken bir hata oluştu.", ex.Message);
            }
        }
    }
}
