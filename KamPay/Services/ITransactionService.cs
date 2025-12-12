using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services
{
    public interface ITransactionService
    {
        // Bir takas teklifi oluşturur
        Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer);

        // Bir bağış veya satış için istek oluşturur
        Task<ServiceResult<Transaction>> CreateRequestAsync(Product product, User buyer);

        // Gelen bir teklife yanıt verir (Onayla/Reddet)
        Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept);

        // Kullanıcının yaptığı teklifleri listeler
        Task<ServiceResult<List<Transaction>>> GetMyOffersAsync(string userId);

        // Kullanıcıya gelen teklifleri listeler
        Task<ServiceResult<List<Transaction>>> GetIncomingOffersAsync(string userId);

        Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId); 

        Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId);

        //  SATIŞ Pazarlık Metodları

        /// <summary>
        /// Satış için fiyat teklifi (Alıcı)
        /// </summary>
        Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId
        );

        /// <summary>
        /// Satış için karşı teklif (Satıcı)
        /// </summary>
        Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(
            string transactionId,
            decimal counterOffer,
            string currentUserId
        );

        //  TAKAS Pazarlık Metodları

        /// <summary>
        /// Takas için ek nakit teklifi (Talep Eden)
        /// </summary>
        Task<ServiceResult<bool>> ProposeAdditionalCashAsync(
            string transactionId,
            decimal additionalCash,
            string currentUserId
        );

        /// <summary>
        /// Takas için karşı nakit teklifi (Sahip)
        /// </summary>
        Task<ServiceResult<bool>> SendCounterCashOfferAsync(
            string transactionId,
            decimal counterCash,
            string currentUserId
        );

        //  Ortak Pazarlık Metodu

        /// <summary>
        /// Anlaşılan fiyat/tutarı kabul et (Hem Satış Hem Takas)
        /// </summary>
        Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(
            string transactionId,
            string currentUserId
        );

        /// <summary>
        /// Transaction için konuşma başlat
        /// </summary>
        Task<ServiceResult<string>> StartConversationForTransactionAsync(
            string transactionId,
            string currentUserId
        );
    }
}