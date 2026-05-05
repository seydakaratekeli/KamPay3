using KamPay.Models;

namespace KamPay.Services.Messaging
{
    public interface INegotiationChatService
    {
        Task<ServiceResult<bool>> ProposeOfferAsync(string transactionId, decimal amount, string currentUserId);
        Task<ServiceResult<bool>> AcceptOfferAsync(string transactionId, string currentUserId);
        Task<ServiceResult<bool>> RejectOfferAsync(string transactionId, string currentUserId);
        Task<ServiceResult<bool>> InvalidateOldOffersAsync(string transactionId, string activeMessageId);
    }
}
