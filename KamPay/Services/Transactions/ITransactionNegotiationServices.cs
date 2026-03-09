using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    /// <summary>
    /// ? ISP: SATIÞ Pazarlýk Ýþlemleri
    /// Sadece SATIÞ iþlemleri için pazarlýk yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ISaleNegotiationService
    {
        /// <summary>
        /// Satýþ için fiyat teklifi (Alýcý)
        /// </summary>
        Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId
        );

        /// <summary>
        /// Satýþ için karþý teklif (Satýcý)
        /// </summary>
        Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(
            string transactionId,
            decimal counterOffer,
            string currentUserId
        );
    }

    /// <summary>
    /// ? ISP: TAKAS Pazarlýk Ýþlemleri
    /// Sadece TAKAS iþlemleri için ek nakit pazarlýðý yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ITradeNegotiationService
    {
        /// <summary>
        /// Takas için ek nakit teklifi (Talep Eden)
        /// </summary>
        Task<ServiceResult<bool>> ProposeAdditionalCashAsync(
            string transactionId,
            decimal additionalCash,
            string currentUserId
        );

        /// <summary>
        /// Takas için karþý nakit teklifi (Sahip)
        /// </summary>
        Task<ServiceResult<bool>> SendCounterCashOfferAsync(
            string transactionId,
            decimal counterCash,
            string currentUserId
        );
    }

    /// <summary>
    /// ? ISP: Ortak Pazarlýk Ýþlemleri
    /// Hem Satýþ hem Takas için ortak pazarlýk metodlarýný içerir
    /// </summary>
    public interface INegotiationCommonService
    {
        /// <summary>
        /// Anlaþýlan fiyat/tutarý kabul et (Hem Satýþ Hem Takas)
        /// </summary>
        Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(
            string transactionId,
            string currentUserId
        );

        /// <summary>
        /// Transaction için konuþma baþlat
        /// </summary>
        Task<ServiceResult<string>> StartConversationForTransactionAsync(
            string transactionId,
            string currentUserId
        );
    }
}
