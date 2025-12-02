using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Firebase-based fiyat teklifi servisi implementasyonu
    /// </summary>
    public class FirebasePriceQuoteService : IPriceQuoteService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly INotificationService _notificationService;
        private readonly IProductService _productService;
        private readonly IServiceSharingService _serviceSharingService;
        private readonly IUserProfileService _userProfileService;

        private const string QUOTES_PATH = "price_quotes";

        public FirebasePriceQuoteService(
            INotificationService notificationService,
            IProductService productService,
            IServiceSharingService serviceSharingService,
            IUserProfileService userProfileService)
        {
            _firebaseClient = new FirebaseClient("https://kampay-b7859-default-rtdb.firebaseio.com/");
            _notificationService = notificationService;
            _productService = productService;
            _serviceSharingService = serviceSharingService;
            _userProfileService = userProfileService;
        }

        public async Task<ValidationResult> CreateQuoteAsync(string userId, CreateQuoteRequest request)
        {
            var result = new ValidationResult();

            try
            {
                // Validasyon
                if (string.IsNullOrEmpty(request.ReferenceId))
                {
                    result.AddError("Ürün veya hizmet ID'si gerekli");
                    return result;
                }

                if (request.QuotedPrice <= 0)
                {
                    result.AddError("Teklif fiyatı 0'dan büyük olmalıdır");
                    return result;
                }

                // Referans bilgilerini getir (ürün veya hizmet)
                string referenceTitle = "";
                string referenceThumbnail = "";
                string sellerId = "";
                string sellerName = "";
                string sellerPhotoUrl = "";
                decimal originalPrice = 0;

                if (request.QuoteType == PriceQuoteType.Product)
                {
                    var product = await _productService.GetProductByIdAsync(request.ReferenceId);
                    if (product == null)
                    {
                        result.AddError("Ürün bulunamadı");
                        return result;
                    }

                    if (product.Type != ProductType.Satis)
                    {
                        result.AddError("Sadece satılık ürünler için teklif verilebilir");
                        return result;
                    }

                    if (product.UserId == userId)
                    {
                        result.AddError("Kendi ürününüze teklif veremezsiniz");
                        return result;
                    }

                    if (product.IsSold || product.IsReserved)
                    {
                        result.AddError("Bu ürün artık müsait değil");
                        return result;
                    }

                    referenceTitle = product.Title;
                    referenceThumbnail = product.ThumbnailUrl;
                    sellerId = product.UserId;
                    sellerName = product.UserName;
                    sellerPhotoUrl = product.UserPhotoUrl;
                    originalPrice = product.Price;
                }
                else if (request.QuoteType == PriceQuoteType.Service)
                {
                    var serviceOffers = await _serviceSharingService.GetAllServicesAsync();
                    var service = serviceOffers?.FirstOrDefault(s => s.ServiceId == request.ReferenceId);
                    
                    if (service == null)
                    {
                        result.AddError("Hizmet bulunamadı");
                        return result;
                    }

                    if (service.ProviderId == userId)
                    {
                        result.AddError("Kendi hizmetinize teklif veremezsiniz");
                        return result;
                    }

                    if (!service.IsAvailable)
                    {
                        result.AddError("Bu hizmet artık müsait değil");
                        return result;
                    }

                    referenceTitle = service.Title;
                    referenceThumbnail = service.ImageUrl;
                    sellerId = service.ProviderId;
                    sellerName = service.ProviderName;
                    sellerPhotoUrl = service.ProviderPhotoUrl;
                    originalPrice = service.Price;
                }

                // Kullanıcı bilgilerini getir
                var buyerProfile = await _userProfileService.GetUserProfileAsync(userId);
                if (buyerProfile == null)
                {
                    result.AddError("Kullanıcı profili bulunamadı");
                    return result;
                }

                // Aynı referans için aktif teklif var mı kontrol et
                var existingQuotes = await GetQuotesForUserAndReferenceAsync(userId, request.ReferenceId, request.QuoteType);
                var activeQuote = existingQuotes?.FirstOrDefault(q => 
                    q.Status == PriceQuoteStatus.Pending || 
                    q.Status == PriceQuoteStatus.CounterOffered);
                
                if (activeQuote != null)
                {
                    result.AddError("Bu ürün/hizmet için zaten aktif bir teklifiniz var");
                    return result;
                }

                // Teklif oluştur
                var quote = new PriceQuote
                {
                    QuoteType = request.QuoteType,
                    ReferenceId = request.ReferenceId,
                    ReferenceTitle = referenceTitle,
                    ReferenceThumbnailUrl = referenceThumbnail,
                    SellerId = sellerId,
                    SellerName = sellerName,
                    SellerPhotoUrl = sellerPhotoUrl,
                    BuyerId = userId,
                    BuyerName = $"{buyerProfile.FirstName} {buyerProfile.LastName}",
                    BuyerPhotoUrl = buyerProfile.ProfilePhotoUrl,
                    OriginalPrice = originalPrice,
                    QuotedPrice = request.QuotedPrice,
                    Message = request.Message,
                    IsFinal = request.IsFinalOffer,
                    CreatedAt = DateTime.UtcNow
                };

                // Firebase'e kaydet
                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quote.QuoteId)
                    .PutAsync(quote);

                // Satıcıya bildirim gönder
                var quoteTypeText = request.QuoteType == PriceQuoteType.Product ? "ürününüz" : "hizmetiniz";
                await _notificationService.SendNotificationAsync(
                    sellerId,
                    "Yeni Fiyat Teklifi! 💰",
                    $"{buyerProfile.FirstName} {referenceTitle} {quoteTypeText} için {request.QuotedPrice:N2} ₺ teklif etti",
                    NotificationType.Quote,
                    quote.QuoteId
                );

                result.IsValid = true;
                result.Data = quote.QuoteId;
            }
            catch (Exception ex)
            {
                result.AddError($"Teklif oluşturulurken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> AcceptQuoteAsync(string userId, string quoteId)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.SellerId != userId)
                {
                    result.AddError("Bu teklifi sadece satıcı kabul edebilir");
                    return result;
                }

                if (quote.Status != PriceQuoteStatus.Pending && quote.Status != PriceQuoteStatus.CounterOffered)
                {
                    result.AddError("Bu teklif kabul edilemez durumda");
                    return result;
                }

                // Teklifi kabul et
                quote.Status = PriceQuoteStatus.Accepted;
                quote.AcceptedAt = DateTime.UtcNow;
                quote.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .PutAsync(quote);

                // Ürün/hizmeti rezerve et
                if (quote.QuoteType == PriceQuoteType.Product)
                {
                    var product = await _productService.GetProductByIdAsync(quote.ReferenceId);
                    if (product != null)
                    {
                        product.IsReserved = true;
                        product.BuyerId = quote.BuyerId;
                        await _firebaseClient
                            .Child("products")
                            .Child(quote.ReferenceId)
                            .PutAsync(product);
                    }
                }

                // Alıcıya bildirim gönder
                var priceToShow = quote.CounterOfferPrice ?? quote.QuotedPrice;
                await _notificationService.SendNotificationAsync(
                    quote.BuyerId,
                    "Teklifiniz Kabul Edildi! 🎉",
                    $"{quote.ReferenceTitle} için {priceToShow:N2} ₺ teklifiniz kabul edildi. Şimdi ödeme yapabilirsiniz!",
                    NotificationType.Quote,
                    quoteId
                );

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Teklif kabul edilirken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> RejectQuoteAsync(string userId, string quoteId, string reason)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.SellerId != userId)
                {
                    result.AddError("Bu teklifi sadece satıcı reddedebilir");
                    return result;
                }

                if (quote.Status != PriceQuoteStatus.Pending && quote.Status != PriceQuoteStatus.CounterOffered)
                {
                    result.AddError("Bu teklif reddedilemez durumda");
                    return result;
                }

                // Teklifi reddet
                quote.Status = PriceQuoteStatus.Rejected;
                quote.RejectedAt = DateTime.UtcNow;
                quote.UpdatedAt = DateTime.UtcNow;
                quote.RejectionReason = reason;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .PutAsync(quote);

                // Alıcıya bildirim gönder
                await _notificationService.SendNotificationAsync(
                    quote.BuyerId,
                    "Teklifiniz Reddedildi",
                    $"{quote.ReferenceTitle} için teklifiniz reddedildi. {(string.IsNullOrEmpty(reason) ? "" : $"Neden: {reason}")}",
                    NotificationType.Quote,
                    quoteId
                );

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Teklif reddedilirken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> MakeCounterOfferAsync(string userId, CounterOfferRequest request)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(request.QuoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.SellerId != userId)
                {
                    result.AddError("Karşı teklif sadece satıcı tarafından yapılabilir");
                    return result;
                }

                if (quote.Status != PriceQuoteStatus.Pending)
                {
                    result.AddError("Bu teklif için karşı teklif yapılamaz");
                    return result;
                }

                if (!quote.CanCounterOffer)
                {
                    result.AddError("Maksimum karşı teklif sayısına ulaşıldı");
                    return result;
                }

                if (request.CounterOfferPrice <= 0 || request.CounterOfferPrice >= quote.OriginalPrice)
                {
                    result.AddError("Karşı teklif fiyatı geçerli aralıkta olmalıdır");
                    return result;
                }

                // Karşı teklif yap
                quote.Status = PriceQuoteStatus.CounterOffered;
                quote.CounterOfferPrice = request.CounterOfferPrice;
                quote.CounterOfferMessage = request.Message;
                quote.CounterOfferCount++;
                quote.IsFinal = request.IsFinalOffer;
                quote.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(request.QuoteId)
                    .PutAsync(quote);

                // Alıcıya bildirim gönder
                await _notificationService.SendNotificationAsync(
                    quote.BuyerId,
                    "Karşı Teklif Aldınız! 💬",
                    $"{quote.SellerName}, {quote.ReferenceTitle} için {request.CounterOfferPrice:N2} ₺ karşı teklif yaptı",
                    NotificationType.Quote,
                    request.QuoteId
                );

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Karşı teklif yapılırken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> AcceptCounterOfferAsync(string userId, string quoteId)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.BuyerId != userId)
                {
                    result.AddError("Karşı teklifi sadece alıcı kabul edebilir");
                    return result;
                }

                if (quote.Status != PriceQuoteStatus.CounterOffered)
                {
                    result.AddError("Kabul edilecek karşı teklif yok");
                    return result;
                }

                // Karşı teklifi kabul et - teklifi Accepted durumuna getir
                quote.Status = PriceQuoteStatus.Accepted;
                quote.AcceptedAt = DateTime.UtcNow;
                quote.UpdatedAt = DateTime.UtcNow;
                // Kabul edilen fiyatı quoted price olarak güncelle
                quote.QuotedPrice = quote.CounterOfferPrice.Value;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .PutAsync(quote);

                // Ürün/hizmeti rezerve et
                if (quote.QuoteType == PriceQuoteType.Product)
                {
                    var product = await _productService.GetProductByIdAsync(quote.ReferenceId);
                    if (product != null)
                    {
                        product.IsReserved = true;
                        product.BuyerId = quote.BuyerId;
                        await _firebaseClient
                            .Child("products")
                            .Child(quote.ReferenceId)
                            .PutAsync(product);
                    }
                }

                // Satıcıya bildirim gönder
                await _notificationService.SendNotificationAsync(
                    quote.SellerId,
                    "Karşı Teklifiniz Kabul Edildi! 🎉",
                    $"{quote.BuyerName}, {quote.ReferenceTitle} için {quote.CounterOfferPrice:N2} ₺ karşı teklifinizi kabul etti",
                    NotificationType.Quote,
                    quoteId
                );

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Karşı teklif kabul edilirken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> RejectCounterOfferAsync(string userId, string quoteId)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.BuyerId != userId)
                {
                    result.AddError("Karşı teklifi sadece alıcı reddedebilir");
                    return result;
                }

                if (quote.Status != PriceQuoteStatus.CounterOffered)
                {
                    result.AddError("Reddedilecek karşı teklif yok");
                    return result;
                }

                // Karşı teklifi reddet - teklifi Rejected durumuna getir
                quote.Status = PriceQuoteStatus.Rejected;
                quote.RejectedAt = DateTime.UtcNow;
                quote.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .PutAsync(quote);

                // Satıcıya bildirim gönder
                await _notificationService.SendNotificationAsync(
                    quote.SellerId,
                    "Karşı Teklifiniz Reddedildi",
                    $"{quote.BuyerName}, {quote.ReferenceTitle} için karşı teklifinizi reddetti",
                    NotificationType.Quote,
                    quoteId
                );

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Karşı teklif reddedilirken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> CancelQuoteAsync(string userId, string quoteId)
        {
            var result = new ValidationResult();

            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote == null)
                {
                    result.AddError("Teklif bulunamadı");
                    return result;
                }

                if (quote.BuyerId != userId)
                {
                    result.AddError("Bu teklifi sadece teklif veren iptal edebilir");
                    return result;
                }

                if (quote.Status == PriceQuoteStatus.Accepted)
                {
                    result.AddError("Kabul edilmiş teklif iptal edilemez");
                    return result;
                }

                // Teklifi iptal et
                quote.Status = PriceQuoteStatus.Cancelled;
                quote.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .PutAsync(quote);

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.AddError($"Teklif iptal edilirken hata: {ex.Message}");
            }

            return result;
        }

        public async Task<PriceQuote> GetQuoteByIdAsync(string quoteId)
        {
            try
            {
                var quote = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .Child(quoteId)
                    .OnceSingleAsync<PriceQuote>();

                return quote;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<PriceQuote>> GetReceivedQuotesAsync(string sellerId, PriceQuoteFilter filter = null)
        {
            try
            {
                var allQuotes = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .OrderBy("SellerId")
                    .EqualTo(sellerId)
                    .OnceAsync<PriceQuote>();

                var quotes = allQuotes
                    .Select(q => q.Object)
                    .ToList();

                return ApplyFilter(quotes, filter);
            }
            catch
            {
                return new List<PriceQuote>();
            }
        }

        public async Task<List<PriceQuote>> GetSentQuotesAsync(string buyerId, PriceQuoteFilter filter = null)
        {
            try
            {
                var allQuotes = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .OrderBy("BuyerId")
                    .EqualTo(buyerId)
                    .OnceAsync<PriceQuote>();

                var quotes = allQuotes
                    .Select(q => q.Object)
                    .ToList();

                return ApplyFilter(quotes, filter);
            }
            catch
            {
                return new List<PriceQuote>();
            }
        }

        public async Task<List<PriceQuote>> GetQuotesForReferenceAsync(string referenceId, PriceQuoteType quoteType)
        {
            try
            {
                var allQuotes = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .OrderBy("ReferenceId")
                    .EqualTo(referenceId)
                    .OnceAsync<PriceQuote>();

                return allQuotes
                    .Select(q => q.Object)
                    .Where(q => q.QuoteType == quoteType)
                    .OrderByDescending(q => q.CreatedAt)
                    .ToList();
            }
            catch
            {
                return new List<PriceQuote>();
            }
        }

        public async Task<int> GetUnreadQuoteCountAsync(string userId)
        {
            try
            {
                // Satıcı olarak aldığı okunmamış teklifler
                var receivedQuotes = await GetReceivedQuotesAsync(userId);
                var unreadCount = receivedQuotes.Count(q => 
                    !q.IsRead && 
                    (q.Status == PriceQuoteStatus.Pending || q.Status == PriceQuoteStatus.CounterOffered));

                return unreadCount;
            }
            catch
            {
                return 0;
            }
        }

        public async Task MarkAsReadAsync(string quoteId)
        {
            try
            {
                var quote = await GetQuoteByIdAsync(quoteId);
                if (quote != null)
                {
                    quote.IsRead = true;
                    await _firebaseClient
                        .Child(QUOTES_PATH)
                        .Child(quoteId)
                        .PutAsync(quote);
                }
            }
            catch
            {
                // Sessizce başarısız
            }
        }

        public async Task UpdateExpiredQuotesAsync()
        {
            try
            {
                var allQuotes = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .OnceAsync<PriceQuote>();

                foreach (var quoteSnapshot in allQuotes)
                {
                    var quote = quoteSnapshot.Object;
                    if (quote.IsExpired)
                    {
                        quote.Status = PriceQuoteStatus.Expired;
                        quote.UpdatedAt = DateTime.UtcNow;
                        
                        await _firebaseClient
                            .Child(QUOTES_PATH)
                            .Child(quote.QuoteId)
                            .PutAsync(quote);
                    }
                }
            }
            catch
            {
                // Sessizce başarısız
            }
        }

        // Yardımcı metodlar
        private async Task<List<PriceQuote>> GetQuotesForUserAndReferenceAsync(string userId, string referenceId, PriceQuoteType quoteType)
        {
            try
            {
                var allQuotes = await _firebaseClient
                    .Child(QUOTES_PATH)
                    .OrderBy("BuyerId")
                    .EqualTo(userId)
                    .OnceAsync<PriceQuote>();

                return allQuotes
                    .Select(q => q.Object)
                    .Where(q => q.ReferenceId == referenceId && q.QuoteType == quoteType)
                    .ToList();
            }
            catch
            {
                return new List<PriceQuote>();
            }
        }

        private List<PriceQuote> ApplyFilter(List<PriceQuote> quotes, PriceQuoteFilter filter)
        {
            if (filter == null)
                return quotes.OrderByDescending(q => q.CreatedAt).ToList();

            var filtered = quotes.AsQueryable();

            if (filter.QuoteType.HasValue)
                filtered = filtered.Where(q => q.QuoteType == filter.QuoteType.Value);

            if (filter.Status.HasValue)
                filtered = filtered.Where(q => q.Status == filter.Status.Value);

            if (filter.FromDate.HasValue)
                filtered = filtered.Where(q => q.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                filtered = filtered.Where(q => q.CreatedAt <= filter.ToDate.Value);

            if (filter.ExcludeExpired)
                filtered = filtered.Where(q => !q.IsExpired);

            return filtered.OrderByDescending(q => q.CreatedAt).ToList();
        }
    }
}
