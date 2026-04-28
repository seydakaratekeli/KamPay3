using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    public class TransactionFacade : ITransactionService
    {
        private readonly TransactionCrudService _crudService;
        private readonly TransactionPaymentService _paymentService;
        private readonly TransactionNegotiationService _negotiationService;
        private readonly TransactionCompletionService _completionService;

        public TransactionFacade(
            TransactionCrudService crudService,
            TransactionPaymentService paymentService,
            TransactionNegotiationService negotiationService,
            TransactionCompletionService completionService)
        {
            _crudService = crudService;
            _paymentService = paymentService;
            _negotiationService = negotiationService;
            _completionService = completionService;
        }

        public Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer)
            => _crudService.CreateTradeOfferAsync(product, offeredProductId, message, buyer);

        // TransactionFacade.cs — delegate et
        public Task<ServiceResult<Transaction>> CreateRequestAsync(
            Product product, User buyer, bool isFixedPriceRequest = false)
            => _crudService.CreateRequestAsync(product, buyer, isFixedPriceRequest);

        public Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept)
            => _crudService.RespondToOfferAsync(transactionId, accept);

        public Task<ServiceResult<List<Transaction>>> GetMyOffersAsync(string userId)
            => _crudService.GetMyOffersAsync(userId);

        public Task<ServiceResult<List<Transaction>>> GetIncomingOffersAsync(string userId)
            => _crudService.GetIncomingOffersAsync(userId);

        public Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId)
            => _paymentService.CompletePaymentAsync(transactionId, buyerId);

        public Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId)
            => _completionService.ConfirmDonationAsync(transactionId, buyerId);

        public Task<ServiceResult<bool>> SetPaymentMethodAsCashAsync(string transactionId)
            => _paymentService.SetPaymentMethodAsCashAsync(transactionId);

        public Task<ServiceResult<Transaction>> CompleteManualSaleAsync(string transactionId, string sellerId)
            => _completionService.CompleteManualSaleAsync(transactionId, sellerId);

        public Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method)
            => _paymentService.CreatePaymentSimulationAsync(transactionId, method);

        public Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string transactionId, string paymentId, string? otp = null)
            => _paymentService.ConfirmPaymentSimulationAsync(transactionId, paymentId, otp);

        public Task<ServiceResult<bool>> ProposePriceForSaleAsync(string transactionId, decimal proposedPrice, string currentUserId, bool isInitialRequest = false)
            => _negotiationService.ProposePriceForSaleAsync(transactionId, proposedPrice, currentUserId, isInitialRequest);

        public Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(string transactionId, decimal counterOffer, string currentUserId)
            => _negotiationService.SendCounterOfferForSaleAsync(transactionId, counterOffer, currentUserId);

        public Task<ServiceResult<bool>> ProposeAdditionalCashAsync(string transactionId, decimal additionalCash, string currentUserId)
            => _negotiationService.ProposeAdditionalCashAsync(transactionId, additionalCash, currentUserId);

        public Task<ServiceResult<bool>> SendCounterCashOfferAsync(string transactionId, decimal counterCash, string currentUserId)
            => _negotiationService.SendCounterCashOfferAsync(transactionId, counterCash, currentUserId);

        public Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string transactionId, string currentUserId)
            => _negotiationService.AcceptNegotiatedPriceAsync(transactionId, currentUserId);

        public Task<ServiceResult<string>> StartConversationForTransactionAsync(string transactionId, string currentUserId)
            => _crudService.StartConversationForTransactionAsync(transactionId, currentUserId);

        public Task<ServiceResult<string>> GetSimulationOtpAsync(string paymentId)
            => _paymentService.GetSimulationOtpAsync(paymentId);
    }
}
