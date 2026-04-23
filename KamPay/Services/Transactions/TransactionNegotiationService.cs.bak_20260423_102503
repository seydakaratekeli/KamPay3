using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Views;
using System;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Sorumluluk: Satış ve takas pazarlık işlemleri.
    /// Fiyat teklifi, karşı teklif ve anlaşma metodları.
    /// Konuşma mesajlaşması için TransactionCrudService yardımcı metodlarına delegate eder.
    /// </summary>
    public partial class TransactionNegotiationService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly TransactionCrudService _crudService; // Konuşma yardımcıları için

        public TransactionNegotiationService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            TransactionCrudService crudService)
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _crudService = crudService;
        }

        // ─────────────────────────────────────────────
        //  SATIŞ PAZARLIK METODLARI
        // ─────────────────────────────────────────────

        /// <summary>Satış için fiyat teklifi (Alıcı)</summary>
        public async Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId,
            bool isInitialRequest = false)
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

                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu işlem satış değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                var canContinueCheck = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
                if (!canContinueCheck.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinueCheck.ErrorMessage);

                var priceCheck = NegotiationRules.ValidateProposedPrice(proposedPrice, transaction.Price);
                if (!priceCheck.IsValid)
                    return ServiceResult<bool>.FailureResult(priceCheck.ErrorMessage);

                transaction.ProposedPriceByBuyer = proposedPrice;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

                if (!transaction.NegotiationStartedAt.HasValue)
                    transaction.NegotiationStartedAt = DateTime.UtcNow;

                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                AppLogger.DebugLog($"✅ Fiyat teklifi kaydedildi: {proposedPrice:N2}₺ (Tur: {transaction.NegotiationRoundCount})");

                string notificationTitle = isInitialRequest ? "💰 Satın Alma İsteği" : "💰 Yeni Fiyat Teklifi";
                string notificationMessage = isInitialRequest
                    ? $"{transaction.BuyerName}, '{transaction.ProductTitle}' ürününü satın almak istiyor. ({proposedPrice:N2}₺)"
                    : $"{transaction.BuyerName}, '{transaction.ProductTitle}' için {proposedPrice:N2}₺ teklif etti. (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})";

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
                        ? $"💰 [{transaction.ProductTitle} - Satış]\nAlıcı bu ürünü liste fiyatından ({proposedPrice:N2}₺) satın almak istiyor."
                        : $"💰 [{transaction.ProductTitle} - Satış]\nAlıcı: {proposedPrice:N2}₺ teklif etti.";

                    await _crudService.AddNegotiationMessageAsync(
                        transaction.ConversationId, messageText, proposedPrice,
                        currentUserId, transaction.BuyerName, transaction.TransactionId, "Propose");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Fiyat teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ ProposePriceForSale hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        /// <summary>Satış için karşı teklif (Satıcı)</summary>
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

                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Satis)
                    return ServiceResult<bool>.FailureResult("Bu işlem satış değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                var counterValidation = NegotiationRules.ValidateCounterOffer(
                    counterOffer, transaction.Price, transaction.ProposedPriceByBuyer);
                if (!counterValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(counterValidation.ErrorMessage);

                transaction.CounterOfferBySeller = counterOffer;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

                if (!transaction.NegotiationStartedAt.HasValue)
                    transaction.NegotiationStartedAt = DateTime.UtcNow;

                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                AppLogger.DebugLog($"✅ Karşı teklif kaydedildi: {counterOffer:N2}₺ (Tur: {transaction.NegotiationRoundCount})");

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.NewOffer,
                    Title = "🔄 Karşı Teklif Alındı",
                    Message = $"'{transaction.ProductTitle}' için karşı teklif: {counterOffer:N2}₺ (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await _crudService.AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        $"🔄 [{transaction.ProductTitle} - Satış]\nSatıcı: {counterOffer:N2}₺ karşı teklif etti.",
                        counterOffer, currentUserId, transaction.SellerName, transaction.TransactionId, "CounterOffer");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Karşı teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ SendCounterOfferForSale hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  TAKAS PAZARLIK METODLARI
        // ─────────────────────────────────────────────

        /// <summary>Takas için ek nakit teklifi (Talep Eden)</summary>
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

                if (transaction.BuyerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu işlem takas değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                var cashValidation = NegotiationRules.ValidateAdditionalCash(additionalCash);
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.AdditionalCashByRequester = additionalCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

                if (!transaction.NegotiationStartedAt.HasValue)
                    transaction.NegotiationStartedAt = DateTime.UtcNow;

                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.NewOffer,
                    Title = "Yeni Nakit Teklifi",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' takası için {additionalCash:N2}₺ ek ödeme teklif etti.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})" : "";

                    await _crudService.AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"🔄 [{transaction.ProductTitle} - Takas]\n💰 Ek Nakit Teklifi: {additionalCash:N2} ₺{exchangeInfo}\n" +
                        $"📊 Pazarlık Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Nakit teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ ProposeAdditionalCash hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        /// <summary>Takas için karşı nakit teklifi (Sahip)</summary>
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

                if (transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (transaction.Type != ProductType.Takas)
                    return ServiceResult<bool>.FailureResult("Bu işlem takas değil");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<bool>.FailureResult("İşlem artık beklemede değil");

                var canContinue = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
                if (!canContinue.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinue.ErrorMessage);

                var cashValidation = NegotiationRules.ValidateAdditionalCash(counterCash);
                if (!cashValidation.IsValid)
                    return ServiceResult<bool>.FailureResult(cashValidation.ErrorMessage);

                transaction.CounterCashByOwner = counterCash;
                transaction.IsNegotiating = true;
                transaction.LastNegotiationDate = DateTime.UtcNow;

                if (!transaction.NegotiationStartedAt.HasValue)
                    transaction.NegotiationStartedAt = DateTime.UtcNow;

                transaction.NegotiationRoundCount++;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.NewOffer,
                    Title = "Karşı Teklif Alındı",
                    Message = $"'{transaction.ProductTitle}' takası için karşı teklif: {counterCash:N2}₺ (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    var requesterOffer = transaction.AdditionalCashByRequester.HasValue
                        ? $"\n(Talep edenin teklifi: {transaction.AdditionalCashByRequester:N2} ₺)" : "";
                    var exchangeInfo = !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                        ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})" : "";

                    await _crudService.AddSystemMessageAsync(
                        transaction.ConversationId,
                        $"🔄 [{transaction.ProductTitle} - Takas]\n💰 Karşı Teklif: {counterCash:N2} ₺{requesterOffer}\n" +
                        $"📊 Pazarlık Turu: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds}");
                }

                return ServiceResult<bool>.SuccessResult(true, $"Karşı teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ SendCounterCashOffer hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Teklif gönderilemedi", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  ORTAK PAZARLIK METODU
        // ─────────────────────────────────────────────

        /// <summary>Anlaşılan fiyat/tutarı kabul et (Hem Satış Hem Takas)</summary>
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

                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<bool>.FailureResult("Yetkiniz yok");

                if (!transaction.IsNegotiating)
                    return ServiceResult<bool>.FailureResult("Aktif pazarlık yok");

                decimal agreedAmount = transaction.AgreedAmount;

                if (transaction.Type == ProductType.Satis || transaction.Type == ProductType.Takas)
                    transaction.QuotedPrice = agreedAmount;

                transaction.IsNegotiating = false;
                transaction.Status = TransactionStatus.Accepted;

                var acceptedBy = transaction.BuyerId == currentUserId ? "Alıcı" : "Satıcı";
                decimal? originalPrice = transaction.Type == ProductType.Satis ? transaction.Price : null;

                var negotiationSummary = NegotiationRules.GetNegotiationSummary(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt, originalPrice, agreedAmount);
                negotiationSummary += $"👤 Kabul Eden: {acceptedBy}\n";
                negotiationSummary += $"✅ Durum: Onaylandı - Ödeme yapılabilir\n";

                transaction.NegotiationNotes += (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n\n") + negotiationSummary;
                transaction.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.SellerId,
                    Type = NotificationType.TransactionUpdate,
                    Title = "💰 Pazarlık Tamamlandı",
                    Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' için {transaction.QuotedPrice:N2}₺ teklifinizi kabul etti. Ödeme bekleniyor.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = transaction.BuyerId,
                    Type = NotificationType.TransactionUpdate,
                    Title = "✅ Fiyat Onaylandı",
                    Message = $"'{transaction.ProductTitle}' için {transaction.QuotedPrice:N2}₺ fiyatında anlaştınız. Artık ödeme yapabilirsiniz.",
                    ActionUrl = nameof(Views.OffersPage)
                });

                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    await _crudService.AddNegotiationMessageAsync(
                        transaction.ConversationId,
                        $"✅ [{transaction.ProductTitle} - Satış]\nTeklifi kabul etti.\nAnlaşılan Fiyat: {agreedAmount:N2} ₺",
                        agreedAmount, currentUserId,
                        currentUserId == transaction.BuyerId ? transaction.BuyerName : transaction.SellerName,
                        transaction.TransactionId, "Accept");
                }

                return ServiceResult<bool>.SuccessResult(true, $"✅ {agreedAmount:N2}₺ fiyatında anlaştınız! Artık ödeme yapabilirsiniz.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ AcceptNegotiatedPrice hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Kabul işlemi başarısız", ex.Message);
            }
        }
    }
}
