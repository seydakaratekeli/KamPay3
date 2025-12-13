using System.Collections.Generic;
using System.Threading.Tasks;
using KamPay.Models;

namespace KamPay.Services
{
   
    /// Interface for managing transaction history operations
   
    public interface ITransactionHistoryService
    {
       
        /// Logs a transaction to the history
       
        Task<ServiceResult<TransactionHistory>> LogTransactionAsync(TransactionHistory transaction);
        
       
        /// Gets transaction history for a specific user
       
        Task<ServiceResult<List<TransactionHistory>>> GetUserTransactionHistoryAsync(string userId, int limit = 50);
        
       
        /// Gets a specific transaction by ID
       
        Task<ServiceResult<TransactionHistory>> GetTransactionByIdAsync(string transactionHistoryId);
        
       
        /// Gets transaction history for a specific reference (e.g., product, service)
       
        Task<ServiceResult<List<TransactionHistory>>> GetTransactionsByReferenceAsync(string referenceId, string referenceType);
    }
}
