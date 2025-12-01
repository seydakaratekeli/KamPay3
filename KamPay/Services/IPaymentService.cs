using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// Payment service interface for handling payment operations
    /// Currently implements simulation, will be integrated with PayTr in Phase 2
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Initiates a payment transaction
        /// </summary>
        Task<ServiceResult<PaymentTransaction>> InitiatePaymentAsync(
            string userId,
            decimal amount,
            string currency,
            PaymentMethodType method,
            string description,
            string referenceId,
            string referenceType);

        /// <summary>
        /// Confirms a payment with OTP or verification code
        /// </summary>
        Task<ServiceResult<bool>> ConfirmPaymentAsync(
            string paymentId,
            string? verificationCode = null);

        /// <summary>
        /// Processes a refund for a payment
        /// </summary>
        Task<ServiceResult<bool>> RefundPaymentAsync(
            string paymentId,
            decimal amount,
            string reason);

        /// <summary>
        /// Gets payment status
        /// </summary>
        Task<ServiceResult<PaymentTransaction>> GetPaymentStatusAsync(string paymentId);

        /// <summary>
        /// Gets user's payment history
        /// </summary>
        Task<ServiceResult<List<PaymentTransaction>>> GetPaymentHistoryAsync(
            string userId,
            int limit = 50);
    }
}
