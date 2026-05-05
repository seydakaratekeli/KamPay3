using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.Transactions;

namespace KamPay.Services.Messaging
{
    public sealed class NegotiationChatService : INegotiationChatService
    {
        private readonly INegotiationOfferService _negotiationOfferService;
        private readonly FirebaseClient _firebaseClient;

        public NegotiationChatService(
            INegotiationOfferService negotiationOfferService,
            FirebaseClient firebaseClient)
        {
            _negotiationOfferService = negotiationOfferService;
            _firebaseClient = firebaseClient;
        }

        public Task<ServiceResult<bool>> ProposeOfferAsync(
            string transactionId,
            decimal amount,
            string currentUserId)
            => CreateOfferAndReturnStatusAsync(transactionId, amount, currentUserId);

        public Task<ServiceResult<bool>> AcceptOfferAsync(
            string transactionId,
            string currentUserId)
            => _negotiationOfferService.AcceptActiveOfferAsync(transactionId, currentUserId);

        public async Task<ServiceResult<bool>> RejectOfferAsync(
            string transactionId,
            string currentUserId)
        {
            return await _negotiationOfferService.RejectActiveOfferAsync(transactionId, currentUserId);
        }

        public async Task<ServiceResult<bool>> InvalidateOldOffersAsync(
            string transactionId,
            string activeMessageId)
        {
            try
            {
                var transaction = await _firebaseClient
                    .Child(Constants.TransactionsCollection)
                    .Child(transactionId)
                    .OnceSingleAsync<Transaction>();

                if (transaction == null || string.IsNullOrWhiteSpace(transaction.ConversationId))
                    return ServiceResult<bool>.SuccessResult(true);

                var messages = await _firebaseClient
                    .Child(Constants.MessagesCollection)
                    .Child(transaction.ConversationId)
                    .OnceAsync<Message>();

                var updates = messages
                    .Where(m => m.Object != null &&
                                m.Object.Type == MessageType.Negotiation &&
                                m.Object.RelatedTransactionId == transactionId &&
                                m.Key != activeMessageId &&
                                m.Object.IsActiveOffer)
                    .Select(m =>
                        _firebaseClient
                            .Child(Constants.MessagesCollection)
                            .Child(transaction.ConversationId)
                            .Child(m.Key)
                            .Child(nameof(Message.IsActiveOffer))
                            .PutAsync(false))
                    .ToList();

                if (updates.Count > 0)
                {
                    await Task.WhenAll(updates);
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                AppLogger.DebugLog($"Eski pazarlik mesajlari pasiflestirilemedi: {ex.Message}");
                return ServiceResult<bool>.FailureResult("Eski teklifler guncellenemedi.", ex.Message);
            }
        }

        private async Task<ServiceResult<bool>> CreateOfferAndReturnStatusAsync(
            string transactionId,
            decimal amount,
            string currentUserId)
        {
            var result = await _negotiationOfferService.CreateOfferAsync(transactionId, amount, currentUserId);
            return result.Success
                ? ServiceResult<bool>.SuccessResult(true, result.Message)
                : ServiceResult<bool>.FailureResult(result.Message, result.Errors.ToArray());
        }
    }
}
