using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services.Shared
{
    /// <summary>
    /// âœ… FAZ2: Transaction tamamlama mantÄ±ÄŸÄ± â€” DRY prensibine uygun paylaÅŸÄ±lan yardÄ±mcÄ± sÄ±nÄ±f.
    ///
    /// Sorumluluk: ÃœrÃ¼n(leri) satÄ±ldÄ±/takas edildi/baÄŸÄ±ÅŸlandÄ± iÅŸaretle,
    ///            puanlarÄ± daÄŸÄ±t, bildirim gÃ¶nder, transaction'Ä± Completed yap.
    /// </summary>
    public class TransactionCompletionHelper
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly IUserProfileService _userProfileService;
        private readonly IProductService _productService;
        private readonly INotificationService _notificationService;

        public TransactionCompletionHelper(
            FirebaseClient firebaseClient,
            IUserProfileService userProfileService,
            IProductService productService,
            INotificationService notificationService)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        public async Task<bool> TryCompleteTransactionIfAllQRsUsedAsync(string transactionId)
        {
            try
            {
                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null) return false;

                var allCodes = await _firebaseClient
                    .Child("delivery_qrcodes")
                    .OnceAsync<DeliveryQRCode>();

                var transactionCodes = allCodes
                    .Select(x => x.Object)
                    .Where(x => x.TransactionId == transactionId)
                    .ToList();

                if (transactionCodes.Count == 0 || !transactionCodes.All(c => c.IsUsed))
                    return false;

                return await CompleteTransactionAsync(transaction);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ TransactionCompletionHelper.TryCompleteTransactionIfAllQRsUsedAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CompleteTransactionAsync(Transaction transaction)
        {
            try
            {
                if (transaction == null) return false;
                if (transaction.Status == TransactionStatus.Completed) return true;

                // 1) Transaction durumunu Completed yap
                transaction.Status = TransactionStatus.Completed;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transaction.TransactionId)
                    .PutAsync(transaction);

                var parallelTasks = new List<Task>();

                // 2) Ana Ã¼rÃ¼nÃ¼ satÄ±ldÄ± iÅŸaretle
                parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.ProductId));

                // 3) Takas ise karÅŸÄ± Ã¼rÃ¼nÃ¼ de iÅŸaretle
                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    parallelTasks.Add(_productService.MarkAsSoldAsync(transaction.OfferedProductId));
                }

                // 4) PuanlarÄ± daÄŸÄ±t
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

                // 5) Bildirimleri gÃ¶nder
                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "BaÄŸÄ±ÅŸ TamamlandÄ±!" : (transaction.Type == ProductType.Takas ? "Takas TamamlandÄ±!" : "ÃœrÃ¼nÃ¼n SatÄ±ldÄ±!"),
                    Message = $"'{transaction.ProductTitle}' iÃ§in '{transaction.BuyerName}' ile olan iÅŸleminiz tamamlandÄ±.",
                    ActionUrl = "OffersPage"
                }));

                parallelTasks.Add(_notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.ProductSold,
                    Title = transaction.Type == ProductType.Bagis ? "BaÄŸÄ±ÅŸ Teslim AlÄ±ndÄ±!" : (transaction.Type == ProductType.Takas ? "Takas TamamlandÄ±!" : "SatÄ±n Alma TamamlandÄ±!"),
                    Message = $"'{transaction.ProductTitle}' Ã¼rÃ¼nÃ¼ iÃ§in '{transaction.SellerName}' ile olan iÅŸleminiz tamamlandÄ±.",
                    ActionUrl = "OffersPage"
                }));

                await Task.WhenAll(parallelTasks);

                KamPay.Helpers.AppLogger.DebugLog($"âœ… Transaction tamamlandÄ±: {transaction.TransactionId}");
                return true;
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"âŒ TransactionCompletionHelper.CompleteTransactionAsync: {ex.Message}");
                return false;
            }
        }
    }
}

