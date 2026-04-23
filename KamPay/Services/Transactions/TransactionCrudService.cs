using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Payment;
using KamPay.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KamPay.Services.Products;
using KamPay.Services.QRCode;

namespace KamPay.Services
{
    /// <summary>
    /// Sorumluluk: Transaction CRUD işlemleri ve konuşma başlatma.
    /// Teklif oluşturma, listeleme, yanıtlama ve konuşma yönetimi.
    /// </summary>
    public partial class TransactionCrudService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IUserProfileService _userProfileService;
        private readonly IPaymentProviderFactory _paymentProviderFactory;

        // ✅ YARDIMCI METOT: QR Kod nesnesini bellekte oluşturur (DB'ye yazmaz)
        private DeliveryQRCode CreateDeliveryQRCodeModel(
            string transactionId,
            string productId,
            string productTitle,
            string giverId,
            string receiverId,
            int validityMinutes)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            var secureCode = Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "")
                .Substring(0, 8).ToUpper();

            return new DeliveryQRCode
            {
                QRCodeId = Guid.NewGuid().ToString(),
                TransactionId = transactionId,
                ProductId = productId,
                ProductTitle = productTitle,
                QRCodeData = $"DELIVERY|{transactionId}|{productId}|{secureCode}",
                VerificationPin = Shared.OtpGenerator.GenerateSecureOtp(),
                SellerId = giverId,
                BuyerId = receiverId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes),
                IsUsed = false,
                DeliveryStatus = DeliveryStatus.Pending
            };
        }

        private static class QRConstants
        {
            public const int QRCodeValidityMinutes = 60;
        }

        public TransactionCrudService(
            INotificationService notificationService,
            IProductService productService,
            IQRCodeService qrCodeService,
            IUserProfileService userProfileService,
            FirebaseClient firebaseClient,
            IPaymentProviderFactory paymentProviderFactory)
        {
            _firebaseClient = firebaseClient;
            _notificationService = notificationService;
            _productService = productService;
            _qrCodeService = qrCodeService;
            _userProfileService = userProfileService;
            _paymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));
        }

        // ─────────────────────────────────────────────
        //  TEKLIF OLUŞTURMA
        // ─────────────────────────────────────────────

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
                    QuotedPrice = product.Price,
                    IsNegotiating = false,
                    NegotiationRoundCount = 0,
                    BuyerPhotoUrl = buyer.ProfileImageUrl ?? "default_avatar.png",
                    SellerPhotoUrl = product.UserPhotoUrl ?? "default_avatar.png"
                };

                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);

                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = product.UserId,
                    Type = NotificationType.NewOffer,
                    Title = product.Type == ProductType.Bagis ? "Yeni Bağış Talebi!" : (product.Type == ProductType.Takas ? "Yeni Takas Teklifi!" : "Yeni Satış Talebi!"),
                    Message = $"{buyer.FullName}, '{product.Title}' ürünün için bir {(product.Type == ProductType.Bagis ? "talep" : "teklif")} gönderdi.",
                    ActionUrl = nameof(Views.OffersPage)
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
                var offeredProductResult = await _productService.GetProductByIdAsync(offeredProductId);
                if (!offeredProductResult.Success || offeredProductResult.Data == null)
                    return ServiceResult<Transaction>.FailureResult("Teklif edilen ürün bulunamadı.");

                var offeredProduct = offeredProductResult.Data;

                var transaction = new Transaction
                {
                    ProductId = product.ProductId,
                    ProductTitle = product.Title,
                    ProductThumbnailUrl = product.ThumbnailUrl,
                    Type = ProductType.Takas,
                    SellerId = product.UserId,
                    SellerName = product.UserName,
                    BuyerId = buyer.UserId,
                    BuyerName = buyer.FullName,
                    Status = TransactionStatus.Pending,
                    OfferedProductId = offeredProductId,
                    OfferedProductTitle = offeredProduct.Title,
                    OfferedProductThumbnailUrl = offeredProduct.ThumbnailUrl,
                    OfferMessage = message,
                    PaymentStatus = PaymentStatus.Pending,
                    BuyerPhotoUrl = buyer.ProfileImageUrl ?? "default_avatar.png",
                    SellerPhotoUrl = product.UserPhotoUrl ?? "default_avatar.png",
                    IsNegotiating = false,
                    NegotiationRoundCount = 0
                };

                await _firebaseClient
                       .Child(Constants.TransactionsCollection)
                       .Child(transaction.TransactionId)
                       .PutAsync(transaction);

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

        // ─────────────────────────────────────────────
        //  TEKLİFE YANIT (QR KOD OLUŞTURMA DAHİL)
        // ─────────────────────────────────────────────

        public async Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept)
        {
            try
            {
                var transactionNode = _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId);
                var transaction = await transactionNode.OnceSingleAsync<Transaction>();

                if (transaction == null)
                    return ServiceResult<Transaction>.FailureResult("İşlem bulunamadı.");

                if (transaction.Status != TransactionStatus.Pending)
                    return ServiceResult<Transaction>.SuccessResult(transaction, "Bu teklif zaten yanıtlanmış.");

                transaction.Status = accept ? TransactionStatus.Accepted : TransactionStatus.Rejected;
                transaction.UpdatedAt = DateTime.UtcNow;

                if (!accept)
                {
                    await transactionNode.PutAsync(transaction);
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

                // KABUL: Atomik işlem + QR kodlar
                var atomicUpdates = new Dictionary<string, object>();
                atomicUpdates[$"{Constants.TransactionsCollection}/{transactionId}"] = transaction;

                await _productService.MarkAsReservedAsync(transaction.ProductId, true);

                if (transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductId))
                {
                    var qr1 = CreateDeliveryQRCodeModel(transactionId, transaction.ProductId,
                        transaction.ProductTitle ?? "Bilinmiyor", transaction.SellerId, transaction.BuyerId, QRConstants.QRCodeValidityMinutes);
                    var qr2 = CreateDeliveryQRCodeModel(transactionId, transaction.OfferedProductId,
                        transaction.OfferedProductTitle ?? "Bilinmiyor", transaction.BuyerId, transaction.SellerId, QRConstants.QRCodeValidityMinutes);

                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr1.QRCodeId}"] = qr1;
                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr2.QRCodeId}"] = qr2;
                }
                else if (transaction.Type == ProductType.Bagis || transaction.Type == ProductType.Satis)
                {
                    var qr = CreateDeliveryQRCodeModel(transactionId, transaction.ProductId,
                        transaction.ProductTitle ?? "Bilinmiyor", transaction.SellerId, transaction.BuyerId, QRConstants.QRCodeValidityMinutes);
                    atomicUpdates[$"{Constants.DeliveryQRCodesCollection}/{qr.QRCodeId}"] = qr;
                }

                await _firebaseClient.Child("/").PatchAsync(atomicUpdates);
                AppLogger.DebugLog("✅ Atomik işlem tamamlandı (Transaction + QR Kodlar).");

                // ✅ FAZ 5: Aynı ürün için diğer pending transaction'ları otomatik reddet
                await AutoRejectOtherPendingOffersAsync(transaction.ProductId, transactionId);

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
                        Title = "Ödeme Yapın",
                        Message = $"'{transaction.ProductTitle}' için ödeme yapabilirsiniz.",
                        ActionUrl = nameof(Views.PaymentPage)
                    });
                }

                return ServiceResult<Transaction>.SuccessResult(transaction, "İşlem başarıyla onaylandı.");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ RespondToOfferAsync Hata: {ex.Message}");
                return ServiceResult<Transaction>.FailureResult("İşlem sırasında hata oluştu.", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  FAZ 5: ÇOKLU ALICI SENARYOSU — OTOMATİK REDDET
        // ─────────────────────────────────────────────

        /// <summary>
        /// Bir ürün için kabul edilen transaction dışındaki tüm pending transaction'ları reddeder.
        /// Satıcı bir alıcıyla anlaşınca diğer alıcıları otomatik bilgilendirir.
        /// </summary>
        private async Task AutoRejectOtherPendingOffersAsync(string productId, string acceptedTransactionId)
        {
            try
            {
                if (string.IsNullOrEmpty(productId)) return;

                // Firebase'de ProductId'ye göre tüm transaction'ları al
                // NOT: Firebase Console'da "transactions" koleksiyonunda "ProductId" index'i gereklidir.
                var allTransactions = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .OrderBy("ProductId")
                    .EqualTo(productId)
                    .OnceAsync<Transaction>();

                foreach (var t in allTransactions)
                {
                    // Kabul edilen transaction'ı atla
                    if (t.Key == acceptedTransactionId) continue;

                    // Sadece Pending olanları reddet
                    if (t.Object == null || t.Object.Status != TransactionStatus.Pending) continue;

                    var rejectedTransaction = t.Object;
                    rejectedTransaction.Status = TransactionStatus.Rejected;
                    rejectedTransaction.IsNegotiating = false;
                    rejectedTransaction.UpdatedAt = DateTime.UtcNow;
                    rejectedTransaction.NegotiationNotes += (string.IsNullOrEmpty(rejectedTransaction.NegotiationNotes) ? "" : "\n")
                        + "⚠️ Ürün başka bir alıcıya satıldı.";

                    await _firebaseClient
                        .Child(Constants.TransactionsCollection)
                        .Child(t.Key)
                        .PutAsync(rejectedTransaction);

                    // Reddedilen alıcıya bildirim gönder
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = rejectedTransaction.BuyerId,
                        Type = NotificationType.OfferRejected,
                        Title = "⚠️ Ürün Satıldı",
                        Message = $"'{rejectedTransaction.ProductTitle}' başka bir alıcıya satıldı. Pazarlığınız kapandı.",
                        ActionUrl = nameof(Views.OffersPage)
                    });

                    // İlgili conversation'a sistem mesajı ekle
                    if (!string.IsNullOrEmpty(rejectedTransaction.ConversationId))
                    {
                        await AddSystemMessageAsync(
                            rejectedTransaction.ConversationId,
                            $"⚠️ [{rejectedTransaction.ProductTitle}]\nBu ürün başka bir alıcıya satıldı. Pazarlık kapandı.");
                    }

                    AppLogger.DebugLog($"✅ AutoReject: {rejectedTransaction.BuyerName} için transaction reddedildi ({t.Key})");
                }
            }
            catch (Exception ex)
            {
                // AutoReject hatası ana akışı bozmaz
                AppLogger.DebugLog($"⚠️ AutoRejectOtherPendingOffersAsync hatası: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        //  LİSTELEME
        // ─────────────────────────────────────────────

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
                }).OrderByDescending(t => t.CreatedAt).ToList();

                return ServiceResult<List<Transaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"HATA - GetIncomingOffersAsync: {ex.Message}");
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
                }).OrderByDescending(t => t.CreatedAt).ToList();

                return ServiceResult<List<Transaction>>.SuccessResult(transactions);
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"HATA - GetMyOffersAsync: {ex.Message}");
                return ServiceResult<List<Transaction>>.FailureResult("Gönderilen teklifler alınamadı.", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  KONUŞMA BAŞLATMA
        // ─────────────────────────────────────────────

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

                if (transaction.BuyerId != currentUserId && transaction.SellerId != currentUserId)
                    return ServiceResult<string>.FailureResult("Bu işleme erişim yetkiniz yok");

                // ✅ Mevcut conversation varsa ve hâlâ aktifse direkt döndür
                if (!string.IsNullOrEmpty(transaction.ConversationId))
                {
                    try
                    {
                        var existingConversation = await _firebaseClient
                            .Child(Constants.ConversationsCollection)
                            .Child(transaction.ConversationId)
                            .OnceSingleAsync<Conversation>();

                        if (existingConversation != null && existingConversation.IsActive)
                            return ServiceResult<string>.SuccessResult(transaction.ConversationId, "Mevcut konuşma bulundu");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.DebugLog($"⚠️ Mevcut konuşma kontrol hatası: {ex.Message}");
                    }
                }

                var otherUserId = transaction.BuyerId == currentUserId ? transaction.SellerId : transaction.BuyerId;

                // ✅ FAZ 1 FIX: Kullanıcı çifti + ProductId bazında conversation ara
                // Aynı iki kullanıcı arasında farklı ürünler için farklı conversation oluşturulur.
                var existingConversations1 = await _firebaseClient
                    .Child(Constants.ConversationsCollection)
                    .OrderBy("User1Id").EqualTo(currentUserId)
                    .OnceAsync<Conversation>();

                var existingWithOtherUser = existingConversations1
                    .FirstOrDefault(c => c.Object != null && c.Object.IsActive &&
                                        (c.Object.User2Id == otherUserId || c.Object.User1Id == otherUserId) &&
                                        c.Object.ProductId == transaction.ProductId); // ✅ ProductId filtresi eklendi

                if (existingWithOtherUser == null)
                {
                    var existingConversations2 = await _firebaseClient
                        .Child(Constants.ConversationsCollection)
                        .OrderBy("User2Id").EqualTo(currentUserId)
                        .OnceAsync<Conversation>();

                    existingWithOtherUser = existingConversations2
                        .FirstOrDefault(c => c.Object != null && c.Object.IsActive &&
                                            (c.Object.User1Id == otherUserId || c.Object.User2Id == otherUserId) &&
                                            c.Object.ProductId == transaction.ProductId); // ✅ ProductId filtresi eklendi
                }

                if (existingWithOtherUser != null)
                {
                    transaction.ConversationId = existingWithOtherUser.Key;
                    transaction.HasActiveConversation = true;
                    await _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId).PutAsync(transaction);
                    return ServiceResult<string>.SuccessResult(existingWithOtherUser.Key, "Mevcut konuşma bulundu");
                }

                // ✅ FAZ 1 FIX: Yeni conversation'a ProductId, ProductTitle ve ProductThumbnail set et
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
                    IsActive = true,
                    // ✅ YENİ: Ürün bilgilerini conversation'a bağla
                    ProductId = transaction.ProductId,
                    ProductTitle = transaction.ProductTitle,
                    ProductThumbnail = transaction.ProductThumbnailUrl
                };

                await _firebaseClient.Child(Constants.ConversationsCollection).Child(conversation.ConversationId).PutAsync(conversation);

                transaction.ConversationId = conversation.ConversationId;
                transaction.HasActiveConversation = true;
                await _firebaseClient.Child(Constants.TransactionsCollection).Child(transactionId).PutAsync(transaction);

                var typeIcon = transaction.Type == ProductType.Satis ? "📦" : transaction.Type == ProductType.Takas ? "🔄" : "🎁";
                var typeText = transaction.Type == ProductType.Satis ? "Satış" : transaction.Type == ProductType.Takas ? "Takas" : "Bağış";
                var priceInfo = transaction.Type == ProductType.Satis ? $"\nFiyat: {transaction.Price:N2} ₺" : "";
                var exchangeInfo = transaction.Type == ProductType.Takas && !string.IsNullOrEmpty(transaction.OfferedProductTitle)
                    ? $"\n(Takas: {transaction.OfferedProductTitle} ↔ {transaction.ProductTitle})" : "";

                await AddSystemMessageAsync(conversation.ConversationId,
                    $"{typeIcon} [{transaction.ProductTitle} - {typeText}]\n📝 Görüşme başlatıldı{priceInfo}{exchangeInfo}");

                AppLogger.DebugLog($"✅ Yeni konuşma oluşturuldu: {conversation.ConversationId} (Ürün: {transaction.ProductTitle})");
                return ServiceResult<string>.SuccessResult(conversation.ConversationId, "Konuşma başlatıldı");
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"❌ StartConversationForTransaction hatası: {ex.Message}");
                return ServiceResult<string>.FailureResult("Konuşma başlatılamadı", ex.Message);
            }
        }

        // ─────────────────────────────────────────────
        //  YARDIMCI METODLAR (private)
        // ─────────────────────────────────────────────

        internal async Task AddSystemMessageAsync(string conversationId, string messageText)
        {
            try
            {
                var systemMessage = new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    SenderId = "system",
                    SenderName = "Sistem",
                    Content = messageText,
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsSystemMessage = true,
                    Type = MessageType.System
                };

                await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(conversationId)
                    .Child(systemMessage.MessageId)
                    .PutAsync(systemMessage);

                var conversationRef = _firebaseClient.Child(Constants.ConversationsCollection).Child(conversationId);
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
                AppLogger.DebugLog($"❌ AddSystemMessageAsync hatası: {ex.Message}");
            }
        }

        internal async Task AddNegotiationMessageAsync(string conversationId, string messageText, decimal proposedPrice,
            string senderId, string senderName, string transactionId, string action)
        {
            try
            {
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

                var conversationRef = _firebaseClient.Child(Constants.ConversationsCollection).Child(conversationId);
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
                AppLogger.DebugLog($"⚠️ Pazarlık mesajı eklenemedi: {ex.Message}");
            }
        }
    }
}