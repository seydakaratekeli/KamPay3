using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Views;
using System;
using System.Threading.Tasks;
using KamPay.Services.Products; // <-- YENİ EKLENEN
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
        private readonly TransactionCrudService _crudService;

        // Tertemiz oldu
        private readonly IProductService _productService;

        public TransactionNegotiationService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            TransactionCrudService crudService,
            IProductService productService) // Tertemiz oldu
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _crudService = crudService;
            _productService = productService;
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

                // ✅ Guard: Sabit fiyatlı taleplerde pazarlık yapılamaz
                if (transaction.IsFixedPriceRequest)
                    return ServiceResult<bool>.FailureResult("Bu işlem liste fiyatı ile satın alma talebidir. Pazarlık yapılamaz.");

                // ✅ FAZ 2: Sıra kontrolü — alıcı üst üste teklif gönderemesin
                // isInitialRequest=true ise ilk talep, sıra kontrolü atlanır
                if (!isInitialRequest && !string.IsNullOrEmpty(transaction.LastActionBy) &&
                    transaction.LastActionBy == currentUserId)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Karşı tarafın yanıtını beklemeniz gerekiyor. Üst üste teklif gönderemezsiniz.");
                }

                var canContinueCheck = NegotiationRules.CanContinueNegotiation(
                    transaction.NegotiationRoundCount, transaction.NegotiationStartedAt);
                if (!canContinueCheck.IsValid)
                    return ServiceResult<bool>.FailureResult(canContinueCheck.ErrorMessage);

                var priceCheck = NegotiationRules.ValidateProposedPrice(proposedPrice, transaction.Price);
                if (!priceCheck.IsValid)
                    return ServiceResult<bool>.FailureResult(priceCheck.ErrorMessage);

                transaction.ProposedPriceByBuyer = proposedPrice;
                transaction.IsNegotiating = !isInitialRequest;

                if (isInitialRequest)
                    transaction.QuotedPrice = proposedPrice;

                transaction.LastNegotiationDate = DateTime.UtcNow;

                if (!transaction.NegotiationStartedAt.HasValue)
                    transaction.NegotiationStartedAt = DateTime.UtcNow;

                transaction.NegotiationRoundCount++;

                // ✅ FAZ 2: Son hareketi yapan kişiyi güncelle
                transaction.LastActionBy = currentUserId;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                AppLogger.DebugLog($"✅ Fiyat teklifi kaydedildi: {proposedPrice:N2}₺ (Tur: {transaction.NegotiationRoundCount}, LastActionBy: {currentUserId})");

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

                return ServiceResult<bool>.SuccessResult(true,
                    $"Fiyat teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
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

                // ✅ Guard: Sabit fiyatlı taleplerde karşı teklif gönderilemez
                if (transaction.IsFixedPriceRequest)
                    return ServiceResult<bool>.FailureResult("Bu işlem liste fiyatı ile satın alma talebidir. Karşı teklif gönderilemez.");

                // ✅ FAZ 2: Sıra kontrolü — satıcı da üst üste karşı teklif gönderemesin
                if (!string.IsNullOrEmpty(transaction.LastActionBy) &&
                    transaction.LastActionBy == currentUserId)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Karşı tarafın yanıtını beklemeniz gerekiyor. Üst üste teklif gönderemezsiniz.");
                }

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

                // ✅ FAZ 2: Son hareketi yapan kişiyi güncelle
                transaction.LastActionBy = currentUserId;

                await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .PutAsync(transaction);

                AppLogger.DebugLog($"✅ Karşı teklif kaydedildi: {counterOffer:N2}₺ (Tur: {transaction.NegotiationRoundCount}, LastActionBy: {currentUserId})");

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

                return ServiceResult<bool>.SuccessResult(true,
                    $"Karşı teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
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

                // ✅ FAZ 2: Sıra kontrolü
                if (!string.IsNullOrEmpty(transaction.LastActionBy) &&
                    transaction.LastActionBy == currentUserId)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Karşı tarafın yanıtını beklemeniz gerekiyor. Üst üste teklif gönderemezsiniz.");
                }

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
                transaction.LastActionBy = currentUserId; // ✅ FAZ 2

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

                return ServiceResult<bool>.SuccessResult(true,
                    $"Nakit teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
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

                // ✅ FAZ 2: Sıra kontrolü
                if (!string.IsNullOrEmpty(transaction.LastActionBy) &&
                    transaction.LastActionBy == currentUserId)
                {
                    return ServiceResult<bool>.FailureResult(
                        "Karşı tarafın yanıtını beklemeniz gerekiyor. Üst üste teklif gönderemezsiniz.");
                }

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
                transaction.LastActionBy = currentUserId; // ✅ FAZ 2

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

                return ServiceResult<bool>.SuccessResult(true,
                    $"Karşı teklifiniz gönderildi (Tur: {transaction.NegotiationRoundCount}/{NegotiationRules.MaxNegotiationRounds})");
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

        /// <summary>
        /// Anlaşılan fiyat/tutarı kabul et (Hem Satış Hem Takas).
        ///
        /// ✅ FAZ 3 — Role-aware davranış:
        ///   • Alıcı kabul ederse  → IsNegotiating=false, Status=Pending kalır.
        ///     Satıcıya "son onayını ver" bildirimi gönderilir.
        ///   • Satıcı kabul ederse → Direkt RespondToOfferAsync(accept=true) gibi davranır,
        ///     transaction Accepted'a geçer ve QR oluşumu için RespondToOfferAsync çağrılır.
        /// </summary>
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
                transaction.UpdatedAt = DateTime.UtcNow;

                // ✅ FAZ 3: Kim kabul ediyor?
                bool isAcceptedByBuyer = transaction.BuyerId == currentUserId;

                if (isAcceptedByBuyer)
                {
                    // ── ALICI KABUL ETTİ ──
                    // Status Pending kalır → Satıcı RespondToOfferAsync ile son onayı verir.
                    // QR oluşturma ve ürün rezervasyonu satıcı onayında yapılır.

                    var negotiationSummary = NegotiationRules.GetNegotiationSummary(
                        transaction.NegotiationRoundCount, transaction.NegotiationStartedAt,
                        transaction.Type == ProductType.Satis ? transaction.Price : null, agreedAmount);
                    negotiationSummary += "👤 Kabul Eden: Alıcı\n";
                    negotiationSummary += "⏳ Durum: Satıcının son onayı bekleniyor\n";

                    transaction.NegotiationNotes += (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n\n")
                        + negotiationSummary;

                    await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(transactionId)
                        .PutAsync(transaction);
                   
// ✅ Alıcı kabul edince → ürünü "Satış Sürecinde" olarak işaretle
// (Satıcı onaylamadan önce bile ürünün rezerve edildiği görünsün)
try
{
   await _productService.MarkAsReservedAsync(transaction.ProductId, true); 
   AppLogger.DebugLog($"✅ Ürün rezerve edildi (alıcı kabul): {transaction.ProductId}");
}
catch (Exception reserveEx)
{
    AppLogger.DebugLog($"⚠️ Rezervasyon işlemi başarısız: {reserveEx.Message}");
    // Ana akışı bozmaz
}
                    // ✅ FAZ 3: Satıcıya role-aware bildirim — "son onayını ver"
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.SellerId,
                        Type = NotificationType.TransactionUpdate,
                        Title = "💰 Alıcı Fiyatı Kabul Etti",
                        Message = $"{transaction.BuyerName}, '{transaction.ProductTitle}' için {transaction.QuotedPrice:N2}₺ teklifinizi kabul etti. Teklifler sayfasından son onayınızı verin.",
                        ActionUrl = nameof(Views.OffersPage)
                    });

                    // ✅ FAZ 3: Alıcıya "satıcıyı bekle" bildirimi
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.BuyerId,
                        Type = NotificationType.TransactionUpdate,
                        Title = "✅ Teklifiniz İletildi",
                        Message = $"'{transaction.ProductTitle}' için {transaction.QuotedPrice:N2}₺ fiyatında anlaştınız. Satıcının son onayı bekleniyor.",
                        ActionUrl = nameof(Views.OffersPage)
                    });

                    if (!string.IsNullOrEmpty(transaction.ConversationId))
                    {
                        await _crudService.AddNegotiationMessageAsync(
                            transaction.ConversationId,
                            $"✅ [{transaction.ProductTitle} - Satış]\nAlıcı teklifi kabul etti.\nAnlaşılan Fiyat: {agreedAmount:N2} ₺\n⏳ Satıcının son onayı bekleniyor...",
                            agreedAmount, currentUserId, transaction.BuyerName,
                            transaction.TransactionId, "Accept");
                    }

                    return ServiceResult<bool>.SuccessResult(true,
                        $"✅ {agreedAmount:N2}₺ fiyatı kabul edildi! Satıcının onayı bekleniyor.");
                }
                else
                {
                    // ── SATICI KABUL ETTİ ──
                    // Satıcı "Kabul Et"e basarsa pazarlık biter, direkt Accepted'a geçer.
                    // RespondToOfferAsync akışını taklit eder: QR oluşturma + bildirimler.

                    var negotiationSummary = NegotiationRules.GetNegotiationSummary(
                        transaction.NegotiationRoundCount, transaction.NegotiationStartedAt,
                        transaction.Type == ProductType.Satis ? transaction.Price : null, agreedAmount);
                    negotiationSummary += "👤 Kabul Eden: Satıcı\n";
                    negotiationSummary += "✅ Durum: Onaylandı - Ödeme yapılabilir\n";

                    transaction.NegotiationNotes += (string.IsNullOrEmpty(transaction.NegotiationNotes) ? "" : "\n\n")
                        + negotiationSummary;

                   

                    if (!string.IsNullOrEmpty(transaction.ConversationId))
                    {
                        await _crudService.AddNegotiationMessageAsync(
                            transaction.ConversationId,
                            $"✅ [{transaction.ProductTitle} - Satış]\nSatıcı teklifi kabul etti.\nAnlaşılan Fiyat: {agreedAmount:N2} ₺\n🎉 Ödeme adımına geçebilirsiniz!",
                            agreedAmount, currentUserId, transaction.SellerName,
                            transaction.TransactionId, "SellerAccept");
                    }

                    await _firebaseClient
    .Child(Constants.TransactionsCollection)
    .Child(transactionId)
    .Child("NegotiationNotes")  // Sadece notes field'ı güncelle
    .PutAsync(transaction.NegotiationNotes);

                    // Satıcının kabul etmesi = RespondToOfferAsync(accept=true) ile eşdeğer
                    // QR oluşturma ve ürün rezervasyonu için orijinal akışa yönlendir
                    var respondResult = await _crudService.RespondToOfferAsync(transactionId, accept: true);

                    if (!respondResult.Success)
                    {
                        AppLogger.DebugLog($"⚠️ Satıcı kabul sonrası RespondToOffer hatası: {respondResult.Message}");
                        // Hata olsa bile transaction güncellendi, devam et
                    }

                    // ✅ FAZ 3: Her iki tarafa da "anlaşıldı" bildirimi
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.BuyerId,
                        Type = NotificationType.OfferAccepted,
                        Title = "🎉 Anlaşma Sağlandı!",
                        Message = $"'{transaction.ProductTitle}' için {agreedAmount:N2}₺ fiyatında anlaştınız. Ödeme yapabilirsiniz.",
                        ActionUrl = nameof(Views.PaymentPage)
                    });

                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = transaction.SellerId,
                        Type = NotificationType.TransactionUpdate,
                        Title = "✅ Pazarlık Tamamlandı",
                        Message = $"'{transaction.ProductTitle}' için {agreedAmount:N2}₺ fiyatında anlaştınız. Alıcının ödemesini bekliyorsunuz.",
                        ActionUrl = nameof(Views.OffersPage)
                    });

                    return ServiceResult<bool>.SuccessResult(true,
                        $"✅ {agreedAmount:N2}₺ fiyatında anlaşıldı! Alıcı ödeme yapabilir.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ AcceptNegotiatedPrice hatası: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Kabul işlemi başarısız", ex.Message);
            }
        }
    }
}