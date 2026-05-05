using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services.Transactions
{
    public interface INegotiationOfferService
    {
        Task<ServiceResult<NegotiationOffer>> CreateOfferAsync(
            string transactionId,
            decimal amount,
            string currentUserId,
            bool isInitialRequest = false);

        Task<ServiceResult<bool>> AcceptActiveOfferAsync(
            string transactionId,
            string currentUserId);

        Task<ServiceResult<bool>> RejectActiveOfferAsync(
            string transactionId,
            string currentUserId);

        Task<ServiceResult<bool>> ExpireActiveOfferAsync(string transactionId);

        Task<NegotiationOffer?> GetActiveOfferAsync(string transactionId);

        Task<List<NegotiationOffer>> GetOfferHistoryAsync(string transactionId);
    }
}
