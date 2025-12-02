using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services
{
    /// <summary>
    /// Interface for managing transaction history operations
    /// </summary>
    public interface ITransactionHistoryService
    {
        /// <summary>
        /// Logs a transaction to the history
        /// </summary>
        Task<ServiceResult<TransactionHistory>> LogTransactionAsync(TransactionHistory transaction);
        
        /// <summary>
        /// Gets transaction history for a specific user
        /// </summary>
        Task<ServiceResult<List<TransactionHistory>>> GetUserTransactionHistoryAsync(string userId, int limit = 50);
        
        /// <summary>
        /// Gets a specific transaction by ID
        /// </summary>
        Task<ServiceResult<TransactionHistory>> GetTransactionByIdAsync(string transactionHistoryId);
        
        /// <summary>
        /// Gets transaction history for a specific reference (e.g., product, service)
        /// </summary>
        Task<ServiceResult<List<TransactionHistory>>> GetTransactionsByReferenceAsync(string referenceId, string referenceType);
    }
}
