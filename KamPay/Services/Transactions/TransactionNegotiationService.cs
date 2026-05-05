using Firebase.Database;
using KamPay.Models;
using KamPay.Services.Products;
using KamPay.Services.Transactions;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Satis ve takas pazarlik metotlarini yeni offer-chain servisine yonlendirir.
    /// Eski transaction scalar-field akisi burada tutulmaz; tam yazma ve stale UI
    /// riskleri INegotiationOfferService icinde merkezi olarak yonetilir.
    /// </summary>
    public class TransactionNegotiationService
    {
        private readonly INegotiationOfferService _negotiationOfferService;

        public TransactionNegotiationService(
            FirebaseClient firebaseClient,
            INotificationService notificationService,
            TransactionCrudService crudService,
            IProductService productService,
            INegotiationOfferService negotiationOfferService)
        {
            _negotiationOfferService = negotiationOfferService;
        }

        public async Task<ServiceResult<bool>> ProposePriceForSaleAsync(
            string transactionId,
            decimal proposedPrice,
            string currentUserId,
            bool isInitialRequest = false)
        {
            var result = await _negotiationOfferService.CreateOfferAsync(
                transactionId,
                proposedPrice,
                currentUserId,
                isInitialRequest);

            return ToBoolResult(result);
        }

        public async Task<ServiceResult<bool>> SendCounterOfferForSaleAsync(
            string transactionId,
            decimal counterOffer,
            string currentUserId)
        {
            var result = await _negotiationOfferService.CreateOfferAsync(
                transactionId,
                counterOffer,
                currentUserId);

            return ToBoolResult(result);
        }

        public async Task<ServiceResult<bool>> ProposeAdditionalCashAsync(
            string transactionId,
            decimal additionalCash,
            string currentUserId)
        {
            var result = await _negotiationOfferService.CreateOfferAsync(
                transactionId,
                additionalCash,
                currentUserId);

            return ToBoolResult(result);
        }

        public async Task<ServiceResult<bool>> SendCounterCashOfferAsync(
            string transactionId,
            decimal counterCash,
            string currentUserId)
        {
            var result = await _negotiationOfferService.CreateOfferAsync(
                transactionId,
                counterCash,
                currentUserId);

            return ToBoolResult(result);
        }

        public Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(
            string transactionId,
            string currentUserId)
            => _negotiationOfferService.AcceptActiveOfferAsync(transactionId, currentUserId);

        private static ServiceResult<bool> ToBoolResult(ServiceResult<NegotiationOffer> result)
            => result.Success
                ? ServiceResult<bool>.SuccessResult(true, result.Message)
                : ServiceResult<bool>.FailureResult(result.Message, result.Errors.ToArray());
    }
}
