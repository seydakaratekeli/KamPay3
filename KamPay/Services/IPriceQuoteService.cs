using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Fiyat teklifi yönetim servisi arayüzü
    /// Dolap tarzı pazarlık ve teklif verme mekanizması
    /// </summary>
    public interface IPriceQuoteService
    {
        /// <summary>
        /// Yeni bir fiyat teklifi oluştur (ürün veya hizmet için)
        /// </summary>
        Task<ValidationResult> CreateQuoteAsync(string userId, CreateQuoteRequest request);
        
        /// <summary>
        /// Teklifi kabul et (satıcı tarafından)
        /// </summary>
        Task<ValidationResult> AcceptQuoteAsync(string userId, string quoteId);
        
        /// <summary>
        /// Teklifi reddet (satıcı tarafından)
        /// </summary>
        Task<ValidationResult> RejectQuoteAsync(string userId, string quoteId, string reason);
        
        /// <summary>
        /// Karşı teklif yap (satıcı tarafından)
        /// </summary>
        Task<ValidationResult> MakeCounterOfferAsync(string userId, CounterOfferRequest request);
        
        /// <summary>
        /// Karşı teklifi kabul et (alıcı tarafından)
        /// </summary>
        Task<ValidationResult> AcceptCounterOfferAsync(string userId, string quoteId);
        
        /// <summary>
        /// Karşı teklifi reddet (alıcı tarafından)
        /// </summary>
        Task<ValidationResult> RejectCounterOfferAsync(string userId, string quoteId);
        
        /// <summary>
        /// Teklifi iptal et (teklif veren tarafından)
        /// </summary>
        Task<ValidationResult> CancelQuoteAsync(string userId, string quoteId);
        
        /// <summary>
        /// Belirli bir teklifi getir
        /// </summary>
        Task<PriceQuote> GetQuoteByIdAsync(string quoteId);
        
        /// <summary>
        /// Kullanıcının aldığı teklifleri listele (satıcı olarak)
        /// </summary>
        Task<List<PriceQuote>> GetReceivedQuotesAsync(string sellerId, PriceQuoteFilter filter = null);
        
        /// <summary>
        /// Kullanıcının gönderdiği teklifleri listele (alıcı olarak)
        /// </summary>
        Task<List<PriceQuote>> GetSentQuotesAsync(string buyerId, PriceQuoteFilter filter = null);
        
        /// <summary>
        /// Belirli bir ürün/hizmet için yapılmış teklifleri getir
        /// </summary>
        Task<List<PriceQuote>> GetQuotesForReferenceAsync(string referenceId, PriceQuoteType quoteType);
        
        /// <summary>
        /// Kullanıcının okunmamış teklif sayısını getir
        /// </summary>
        Task<int> GetUnreadQuoteCountAsync(string userId);
        
        /// <summary>
        /// Teklifi okundu olarak işaretle
        /// </summary>
        Task MarkAsReadAsync(string quoteId);
        
        /// <summary>
        /// Süresi dolmuş teklifleri güncelle
        /// </summary>
        Task UpdateExpiredQuotesAsync();
    }
}
