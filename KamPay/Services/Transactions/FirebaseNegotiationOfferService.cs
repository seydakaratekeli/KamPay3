using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Products;
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    public class FirebaseNegotiationOfferService : INegotiationOfferService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly TransactionCrudService _crudService;
        private readonly IProductService _productService;

        public FirebaseNegotiationOfferService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            TransactionCrudService crudService,
            IProductService productService)
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _crudService = crudService;
            _productService = productService;
        }

        public async Task<ServiceResult<NegotiationOffer>> CreateOfferAsync(
            string transactionId,
            decimal amount,
            string currentUserId,
            bool isInitialRequest = false)
        {
            try
            {
                var transaction = await GetTransactionAsync(transactionId);
                if (transaction == null)
                    return ServiceResult<NegotiationOffer>.FailureResult("Islem bulunamadi");

                if (transaction.Type != ProductType.Satis && transaction.Type != ProductType.Takas)
                    return ServiceResult<NegotiationOffer>.FailureResult("Bu islem pazarlik desteklemiyor");

                if (transaction.Type == ProductType.Satis && amount <= 0)
                    return ServiceResult<NegotiationOffer>.FailureResult("Fiyat 0'dan buyuk olmali");

                if (transaction.Type == ProductType.Takas && amount < 0)
                    return ServiceResult<NegotiationOffer>.FailureResult("Tutar negatif olamaz");

                if (transaction.Type == ProductType.Satis && transaction.IsFixedPriceRequest)
                    return ServiceResult<NegotiationOffer>.FailureResult("Bu islem liste fiyati ile satin alma talebidir. Pazarlik yapilamaz.");

                if (transaction.Status != TransactionStatus.Pending &&
                    transaction.Status != TransactionStatus.Negotiating)
                {
                    return ServiceResult<NegotiationOffer>.FailureResult("Islem artik pazarlik asamasinda degil");
                }

                bool isBuyer = transaction.BuyerId == currentUserId;
                bool isSeller = transaction.SellerId == currentUserId;
                if (!isBuyer && !isSeller)
                    return ServiceResult<NegotiationOffer>.FailureResult("Yetkiniz yok");

                var productAvailability = await ValidateProductAvailabilityAsync(transaction);
                if (!productAvailability.IsValid)
                    return ServiceResult<NegotiationOffer>.FailureResult(productAvailability.ErrorMessage);

                if (!isInitialRequest &&
                    !string.IsNullOrEmpty(transaction.LastActionBy) &&
                    transaction.LastActionBy == currentUserId)
                {
                    return ServiceResult<NegotiationOffer>.FailureResult(
                        "Karsi tarafin yanitini beklemeniz gerekiyor. Ust uste teklif gonderemezsiniz.");
                }

                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount,
                    transaction.NegotiationStartedAt);
                if (!canContinue.IsValid)
                    return ServiceResult<NegotiationOffer>.FailureResult(canContinue.ErrorMessage);

                var currentActive = await GetActiveOfferAsync(transactionId);
                if (transaction.Type == ProductType.Takas)
                {
                    var cashCheck = NegotiationRules.ValidateAdditionalCash(amount);
                    if (!cashCheck.IsValid)
                        return ServiceResult<NegotiationOffer>.FailureResult(cashCheck.ErrorMessage);
                }
                else if (isBuyer)
                {
                    var priceCheck = NegotiationRules.ValidateProposedPrice(amount, transaction.Price);
                    if (!priceCheck.IsValid)
                        return ServiceResult<NegotiationOffer>.FailureResult(priceCheck.ErrorMessage);
                }
                else
                {
                    var counterCheck = NegotiationRules.ValidateCounterOffer(
                        amount,
                        transaction.Price,
                        currentActive?.Amount ?? transaction.ProposedPriceByBuyer);
                    if (!counterCheck.IsValid)
                        return ServiceResult<NegotiationOffer>.FailureResult(counterCheck.ErrorMessage);
                }

                var now = DateTime.UtcNow;
                var offer = new NegotiationOffer
                {
                    TransactionId = transactionId,
                    ProposerId = currentUserId,
                    ProposerName = isBuyer ? transaction.BuyerName : transaction.SellerName,
                    Role = isBuyer ? ProposerRole.Buyer : ProposerRole.Seller,
                    Amount = amount,
                    Status = OfferStatus.Active,
                    CreatedAt = now,
                    ParentOfferId = currentActive?.OfferId,
                    RoundNumber = transaction.NegotiationRoundCount + 1
                };

                var updates = new Dictionary<string, object>
                {
                    [$"{Constants.NegotiationOffersCollection}/{transactionId}/{offer.OfferId}"] = offer,
                    [$"{Constants.TransactionsCollection}/{transactionId}/CurrentActiveOfferId"] = offer.OfferId,
                    [$"{Constants.TransactionsCollection}/{transactionId}/LastActionBy"] = currentUserId,
                    [$"{Constants.TransactionsCollection}/{transactionId}/LastNegotiationDate"] = now,
                    [$"{Constants.TransactionsCollection}/{transactionId}/NegotiationRoundCount"] = offer.RoundNumber,
                    [$"{Constants.TransactionsCollection}/{transactionId}/Status"] = TransactionStatus.Negotiating,
                    [$"{Constants.TransactionsCollection}/{transactionId}/IsNegotiating"] = true,
                    [$"{Constants.TransactionsCollection}/{transactionId}/UpdatedAt"] = now
                };

                if (currentActive != null && !currentActive.OfferId.StartsWith("legacy-", StringComparison.Ordinal))
                {
                    currentActive.Status = OfferStatus.Superseded;
                    currentActive.RespondedAt = now;
                    updates[$"{Constants.NegotiationOffersCollection}/{transactionId}/{currentActive.OfferId}"] = currentActive;
                }

                if (!transaction.NegotiationStartedAt.HasValue)
                    updates[$"{Constants.TransactionsCollection}/{transactionId}/NegotiationStartedAt"] = now;

                if (transaction.Type == ProductType.Satis && isInitialRequest)
                    updates[$"{Constants.TransactionsCollection}/{transactionId}/QuotedPrice"] = amount;

                if (transaction.Type == ProductType.Takas)
                {
                    if (isBuyer)
                        updates[$"{Constants.TransactionsCollection}/{transactionId}/AdditionalCashByRequester"] = amount;
                    else
                        updates[$"{Constants.TransactionsCollection}/{transactionId}/CounterCashByOwner"] = amount;
                }
                else if (isBuyer)
                    updates[$"{Constants.TransactionsCollection}/{transactionId}/ProposedPriceByBuyer"] = amount;
                else
                    updates[$"{Constants.TransactionsCollection}/{transactionId}/CounterOfferBySeller"] = amount;

                var freshnessCheck = await ValidateTransactionFreshnessAsync(transaction, currentUserId, isInitialRequest);
                if (!freshnessCheck.IsValid)
                    return ServiceResult<NegotiationOffer>.FailureResult(freshnessCheck.ErrorMessage);

                await ApplyMultiPathUpdatesAsync(updates);

                await SendCreateOfferSideEffectsAsync(transaction, offer, isInitialRequest);

                return ServiceResult<NegotiationOffer>.SuccessResult(
                    offer,
                    $"{(isSeller ? "Karsi teklifiniz" : "Fiyat teklifiniz")} gonderildi (Tur: {offer.RoundNumber}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation offer olusturma hatasi: {ex.Message}");
                return ServiceResult<NegotiationOffer>.FailureResult("Teklif gonderilemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AcceptActiveOfferAsync(
            string transactionId,
            string currentUserId)
        {
            try
            {
                var transaction = await GetTransactionAsync(transactionId);
                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("Islem bulunamadi");

                var activeOffer = await GetActiveOfferAsync(transactionId);
                if (activeOffer == null)
                    return ServiceResult<bool>.FailureResult("Aktif teklif yok");

                if (activeOffer.ProposerId == currentUserId)
                    return ServiceResult<bool>.FailureResult("Kendi teklifinizi kabul edemezsiniz");

                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Status != TransactionStatus.Pending &&
                    transaction.Status != TransactionStatus.Negotiating)
                {
                    return ServiceResult<bool>.FailureResult("Islem artik pazarlik asamasinda degil");
                }

                var productAvailability = await ValidateProductAvailabilityAsync(transaction, allowReserved: true);
                if (!productAvailability.IsValid)
                    return ServiceResult<bool>.FailureResult(productAvailability.ErrorMessage);

                activeOffer.Status = OfferStatus.Accepted;
                activeOffer.RespondedAt = DateTime.UtcNow;

                var updates = new Dictionary<string, object>
                {
                    [$"{Constants.TransactionsCollection}/{transactionId}/Status"] = TransactionStatus.Pending,
                    [$"{Constants.TransactionsCollection}/{transactionId}/IsNegotiating"] = false,
                    [$"{Constants.TransactionsCollection}/{transactionId}/UpdatedAt"] = DateTime.UtcNow
                };

                if (transaction.Type == ProductType.Satis || transaction.Type == ProductType.Takas)
                    updates[$"{Constants.TransactionsCollection}/{transactionId}/QuotedPrice"] = activeOffer.Amount;

                if (!activeOffer.OfferId.StartsWith("legacy-", StringComparison.Ordinal))
                    updates[$"{Constants.NegotiationOffersCollection}/{transactionId}/{activeOffer.OfferId}"] = activeOffer;

                bool isAcceptedByBuyer = transaction.BuyerId == currentUserId;
                if (isAcceptedByBuyer)
                {
                    await ApplyMultiPathUpdatesAsync(updates);

                    try
                    {
                        await _productService.MarkAsReservedAsync(transaction.ProductId, true);
                    }
                    catch (Exception reserveEx)
                    {
                        AppLogger.DebugLog($"Urun rezervasyon hatasi pazarlik kabul sonrasi: {reserveEx.Message}");
                    }

                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.SellerId,
                        Type = NotificationType.TransactionUpdate,
                        Title = transaction.Type == ProductType.Takas ? "Talep Eden Teklifi Kabul Etti" : "Alici Fiyati Kabul Etti",
                        Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' icin {FormatAmount(transaction, activeOffer.Amount)} teklifinizi kabul etti. Son onayinizi verin.",
                        ActionUrl = nameof(OffersPage)
                    });

                    if (!string.IsNullOrEmpty(transaction.ConversationId))
                    {
                        await _crudService.AddNegotiationMessageAsync(
                            transaction.ConversationId,
                            $"[{transaction.ProductTitle} - {GetTransactionTypeText(transaction)}]\nAlici teklifi kabul etti.\nAnlasilan Tutar: {FormatAmount(transaction, activeOffer.Amount)}\nSaticinin son onayi bekleniyor.",
                            activeOffer.Amount,
                            currentUserId,
                            transaction.BuyerName,
                            transaction.TransactionId,
                            "Accept");
                    }

                    return ServiceResult<bool>.SuccessResult(true, $"{FormatAmount(transaction, activeOffer.Amount)} kabul edildi. Saticinin onayi bekleniyor.");
                }

                await ApplyMultiPathUpdatesAsync(updates);

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await _crudService.AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        $"[{transaction.ProductTitle} - {GetTransactionTypeText(transaction)}]\nSatici teklifi kabul etti.\nAnlasilan Tutar: {FormatAmount(transaction, activeOffer.Amount)}\nSonraki adima gecebilirsiniz.",
                        activeOffer.Amount,
                        currentUserId,
                        transaction.SellerName,
                        transaction.TransactionId,
                        "SellerAccept");
                }

                var respondResult = await _crudService.RespondToOfferAsync(transactionId, accept: true);
                if (!respondResult.Success)
                    return ServiceResult<bool>.FailureResult(respondResult.Message, respondResult.Errors.ToArray());

                return ServiceResult<bool>.SuccessResult(true, $"{FormatAmount(transaction, activeOffer.Amount)} tutarinda anlasildi.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation offer kabul hatasi: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Kabul islemi basarisiz", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> RejectActiveOfferAsync(
            string transactionId,
            string currentUserId)
        {
            try
            {
                var transaction = await GetTransactionAsync(transactionId);
                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("Islem bulunamadi");

                var activeOffer = await GetActiveOfferAsync(transactionId);
                if (activeOffer == null)
                    return ServiceResult<bool>.FailureResult("Aktif teklif yok");

                if (activeOffer.ProposerId == currentUserId)
                    return ServiceResult<bool>.FailureResult("Kendi teklifinizi reddedemezsiniz");

                activeOffer.Status = OfferStatus.Rejected;
                activeOffer.RespondedAt = DateTime.UtcNow;

                var updates = new Dictionary<string, object>
                {
                    [$"{Constants.TransactionsCollection}/{transactionId}/Status"] = TransactionStatus.Rejected,
                    [$"{Constants.TransactionsCollection}/{transactionId}/IsNegotiating"] = false,
                    [$"{Constants.TransactionsCollection}/{transactionId}/UpdatedAt"] = DateTime.UtcNow
                };

                if (!activeOffer.OfferId.StartsWith("legacy-", StringComparison.Ordinal))
                    updates[$"{Constants.NegotiationOffersCollection}/{transactionId}/{activeOffer.OfferId}"] = activeOffer;

                await ApplyMultiPathUpdatesAsync(updates);
                return ServiceResult<bool>.SuccessResult(true, "Teklif reddedildi.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation offer red hatasi: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Red islemi basarisiz", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> ExpireActiveOfferAsync(string transactionId)
        {
            try
            {
                var transaction = await GetTransactionAsync(transactionId);
                if (transaction == null)
                    return ServiceResult<bool>.FailureResult("Islem bulunamadi");

                var activeOffer = await GetActiveOfferAsync(transactionId);
                var updates = new Dictionary<string, object>
                {
                    [$"{Constants.TransactionsCollection}/{transactionId}/Status"] = TransactionStatus.Expired,
                    [$"{Constants.TransactionsCollection}/{transactionId}/IsNegotiating"] = false,
                    [$"{Constants.TransactionsCollection}/{transactionId}/UpdatedAt"] = DateTime.UtcNow
                };

                if (activeOffer != null && !activeOffer.OfferId.StartsWith("legacy-", StringComparison.Ordinal))
                {
                    activeOffer.Status = OfferStatus.Expired;
                    activeOffer.RespondedAt = DateTime.UtcNow;
                    updates[$"{Constants.NegotiationOffersCollection}/{transactionId}/{activeOffer.OfferId}"] = activeOffer;
                }

                await ApplyMultiPathUpdatesAsync(updates);
                return ServiceResult<bool>.SuccessResult(true, "Pazarlik suresi doldu.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Negotiation offer expire hatasi: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Pazarlik suresi guncellenemedi", ex.Message);
            }
        }

        public async Task<NegotiationOffer?> GetActiveOfferAsync(string transactionId)
        {
            var transaction = await GetTransactionAsync(transactionId);
            if (transaction == null)
                return null;

            if (!string.IsNullOrEmpty(transaction.CurrentActiveOfferId))
            {
                var active = await _firebaseClient
                    .Child(Constants.NegotiationOffersCollection)
                    .Child(transactionId)
                    .Child(transaction.CurrentActiveOfferId)
                    .OnceSingleAsync<NegotiationOffer>();

                if (active?.Status == OfferStatus.Active)
                    return active;
            }

            var offers = await GetOfferHistoryAsync(transactionId);
            var latestActive = offers
                .Where(o => o.Status == OfferStatus.Active)
                .OrderByDescending(o => o.RoundNumber)
                .ThenByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            return latestActive ?? CreateLegacyActiveOffer(transaction);
        }

        public async Task<List<NegotiationOffer>> GetOfferHistoryAsync(string transactionId)
        {
            var offers = await _firebaseClient
                .Child(Constants.NegotiationOffersCollection)
                .Child(transactionId)
                .OnceAsync<NegotiationOffer>();

            return offers
                .Where(o => o.Object != null)
                .Select(o => o.Object)
                .OrderBy(o => o.RoundNumber)
                .ThenBy(o => o.CreatedAt)
                .ToList();
        }

        private async Task<Transaction?> GetTransactionAsync(string transactionId)
            => await _firebaseClient
                .Child(Constants.TransactionsCollection)
                .Child(transactionId)
                .OnceSingleAsync<Transaction>();

        private async Task ApplyMultiPathUpdatesAsync(Dictionary<string, object> updates)
        {
            foreach (var update in updates)
            {
                var pathParts = update.Key
                    .Split('/', StringSplitOptions.RemoveEmptyEntries);

                var node = _firebaseClient.Child(pathParts[0]);
                foreach (var part in pathParts.Skip(1))
                {
                    node = node.Child(part);
                }

                await node.PutAsync(update.Value);
            }
        }

        private async Task<ValidationResult> ValidateProductAvailabilityAsync(
            Transaction transaction,
            bool allowReserved = false)
        {
            var productResult = await _productService.GetProductByIdAsync(transaction.ProductId);
            if (!productResult.Success || productResult.Data == null)
                return ValidationResult.Failure("Urun bilgisi dogrulanamadi");

            var product = productResult.Data;
            if (!product.IsActive || product.IsSold)
                return ValidationResult.Failure("Bu urun artik satista degil");

            if (!allowReserved && product.IsReserved)
                return ValidationResult.Failure("Bu urun icin baska bir islem devam ediyor");

            if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
            {
                var offeredProductResult = await _productService.GetProductByIdAsync(transaction.OfferedProductId);
                if (!offeredProductResult.Success || offeredProductResult.Data == null)
                    return ValidationResult.Failure("Takas edilen urun bilgisi dogrulanamadi");

                var offeredProduct = offeredProductResult.Data;
                if (!offeredProduct.IsActive || offeredProduct.IsSold)
                    return ValidationResult.Failure("Takas edilen urun artik uygun degil");

                if (!allowReserved && offeredProduct.IsReserved)
                    return ValidationResult.Failure("Takas edilen urun icin baska bir islem devam ediyor");
            }

            return ValidationResult.Success();
        }

        private async Task<ValidationResult> ValidateTransactionFreshnessAsync(
            Transaction original,
            string currentUserId,
            bool isInitialRequest)
        {
            var latest = await GetTransactionAsync(original.TransactionId);
            if (latest == null)
                return ValidationResult.Failure("Islem bulunamadi");

            if (latest.Status != TransactionStatus.Pending &&
                latest.Status != TransactionStatus.Negotiating)
            {
                return ValidationResult.Failure("Islem artik pazarlik asamasinda degil");
            }

            if (!isInitialRequest &&
                !string.IsNullOrEmpty(latest.LastActionBy) &&
                latest.LastActionBy == currentUserId)
            {
                return ValidationResult.Failure("Karsi tarafin yanitini beklemeniz gerekiyor.");
            }

            if (latest.NegotiationRoundCount != original.NegotiationRoundCount ||
                latest.LastActionBy != original.LastActionBy ||
                latest.CurrentActiveOfferId != original.CurrentActiveOfferId)
            {
                return ValidationResult.Failure("Pazarlik siz islem yaparken guncellendi. Lutfen ekrani yenileyin.");
            }

            return ValidationResult.Success();
        }

        private static NegotiationOffer? CreateLegacyActiveOffer(Transaction transaction)
        {
            if (!transaction.IsNegotiating)
                return null;

            if (transaction.CounterOfferBySeller.HasValue && transaction.CounterOfferBySeller.Value > 0)
            {
                return new NegotiationOffer
                {
                    OfferId = "legacy-seller-counter",
                    TransactionId = transaction.TransactionId,
                    ProposerId = transaction.SellerId,
                    ProposerName = transaction.SellerName,
                    Role = ProposerRole.Seller,
                    Amount = transaction.CounterOfferBySeller.Value,
                    Status = OfferStatus.Active,
                    RoundNumber = Math.Max(1, transaction.NegotiationRoundCount)
                };
            }

            if (transaction.ProposedPriceByBuyer.HasValue && transaction.ProposedPriceByBuyer.Value > 0)
            {
                return new NegotiationOffer
                {
                    OfferId = "legacy-buyer-offer",
                    TransactionId = transaction.TransactionId,
                    ProposerId = transaction.BuyerId,
                    ProposerName = transaction.BuyerName,
                    Role = ProposerRole.Buyer,
                    Amount = transaction.ProposedPriceByBuyer.Value,
                    Status = OfferStatus.Active,
                    RoundNumber = Math.Max(1, transaction.NegotiationRoundCount)
                };
            }

            if (transaction.CounterCashByOwner.HasValue && transaction.CounterCashByOwner.Value >= 0)
            {
                return new NegotiationOffer
                {
                    OfferId = "legacy-owner-counter-cash",
                    TransactionId = transaction.TransactionId,
                    ProposerId = transaction.SellerId,
                    ProposerName = transaction.SellerName,
                    Role = ProposerRole.Seller,
                    Amount = transaction.CounterCashByOwner.Value,
                    Status = OfferStatus.Active,
                    RoundNumber = Math.Max(1, transaction.NegotiationRoundCount)
                };
            }

            if (transaction.AdditionalCashByRequester.HasValue && transaction.AdditionalCashByRequester.Value >= 0)
            {
                return new NegotiationOffer
                {
                    OfferId = "legacy-requester-cash",
                    TransactionId = transaction.TransactionId,
                    ProposerId = transaction.BuyerId,
                    ProposerName = transaction.BuyerName,
                    Role = ProposerRole.Buyer,
                    Amount = transaction.AdditionalCashByRequester.Value,
                    Status = OfferStatus.Active,
                    RoundNumber = Math.Max(1, transaction.NegotiationRoundCount)
                };
            }

            return null;
        }

        private async Task SendCreateOfferSideEffectsAsync(
            Transaction transaction,
            NegotiationOffer offer,
            bool isInitialRequest)
        {
            string title = transaction.Type == ProductType.Takas
                ? (offer.Role == ProposerRole.Buyer ? "Yeni Nakit Teklifi" : "Karsi Nakit Teklifi")
                : (offer.Role == ProposerRole.Buyer
                    ? (isInitialRequest ? "Satin Alma Istegi" : "Yeni Fiyat Teklifi")
                    : "Karsi Teklif Alindi");

            string message = offer.Role == ProposerRole.Buyer
                ? $"{transaction.BuyerName}, '{transaction.ProductTitle}' icin {FormatAmount(transaction, offer.Amount)} teklif etti. (Tur: {offer.RoundNumber}/{NegotiationRules.MaxNegotiationRounds})"
                : $"'{transaction.ProductTitle}' icin karsi teklif: {FormatAmount(transaction, offer.Amount)} (Tur: {offer.RoundNumber}/{NegotiationRules.MaxNegotiationRounds})";

            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = offer.Role == ProposerRole.Buyer ? transaction.SellerId : transaction.BuyerId,
                Type = NotificationType.NewOffer,
                Title = title,
                Message = message,
                ActionUrl = nameof(OffersPage)
            });

            if (string.IsNullOrEmpty(transaction.ConversationId))
                return;

            var messageText = offer.Role == ProposerRole.Buyer
                ? $"[{transaction.ProductTitle} - {GetTransactionTypeText(transaction)}]\nAlici: {FormatAmount(transaction, offer.Amount)} teklif etti."
                : $"[{transaction.ProductTitle} - {GetTransactionTypeText(transaction)}]\nSatici: {FormatAmount(transaction, offer.Amount)} karsi teklif etti.";

            await _crudService.AddNegotiationMessageAsync(
                transaction.ConversationId,
                messageText,
                offer.Amount,
                offer.ProposerId,
                offer.ProposerName,
                transaction.TransactionId,
                transaction.Type == ProductType.Takas
                    ? (offer.Role == ProposerRole.Buyer ? "ProposeCash" : "CounterCash")
                    : (offer.Role == ProposerRole.Buyer ? "Propose" : "CounterOffer"));
        }

        private static string FormatAmount(Transaction transaction, decimal amount)
            => transaction.Type == ProductType.Takas
                ? $"{amount:N2} TL ek nakit"
                : $"{amount:N2} TL";

        private static string GetTransactionTypeText(Transaction transaction)
            => transaction.Type == ProductType.Takas ? "Takas" : "Satis";
    }
}
