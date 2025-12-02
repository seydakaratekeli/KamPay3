using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services
{
    /// <summary>
    /// Firebase implementation for transaction history tracking
    /// </summary>
    public class FirebaseTransactionHistoryService : ITransactionHistoryService
    {
        private readonly FirebaseClient _firebaseClient;
        private const string TRANSACTION_HISTORY_COLLECTION = "transaction_history";

        public FirebaseTransactionHistoryService()
        {
            _firebaseClient = new FirebaseClient(Constants.FirebaseRealtimeDbUrl);
        }

        /// <summary>
        /// Logs a transaction to the history
        /// </summary>
        public async Task<ServiceResult<TransactionHistory>> LogTransactionAsync(TransactionHistory transaction)
        {
            try
            {
                if (transaction == null)
                {
                    return ServiceResult<TransactionHistory>.FailureResult("İşlem bilgisi boş olamaz");
                }

                // Ensure ID is set
                if (string.IsNullOrEmpty(transaction.TransactionHistoryId))
                {
                    transaction.TransactionHistoryId = Guid.NewGuid().ToString();
                }

                // Set timestamp if not set
                if (transaction.CreatedAt == default)
                {
                    transaction.CreatedAt = DateTime.UtcNow;
                }

                await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .Child(transaction.TransactionHistoryId)
                    .PutAsync(transaction);

                return ServiceResult<TransactionHistory>.SuccessResult(transaction, "İşlem kaydedildi");
            }
            catch (Exception ex)
            {
                return ServiceResult<TransactionHistory>.FailureResult(
                    "İşlem kaydedilemedi",
                    ex.Message
                );
            }
        }

        /// <summary>
        /// Gets transaction history for a specific user (both sent and received)
        /// </summary>
        public async Task<ServiceResult<List<TransactionHistory>>> GetUserTransactionHistoryAsync(string userId, int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return ServiceResult<List<TransactionHistory>>.FailureResult("Kullanıcı ID gerekli");
                }

                // Get all transactions
                var allTransactions = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .OnceAsync<TransactionHistory>();

                // Filter transactions where user is either sender or receiver
                var userTransactions = allTransactions
                    .Select(t => t.Object)
                    .Where(t => t.FromUserId == userId || t.ToUserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(limit)
                    .ToList();

                return ServiceResult<List<TransactionHistory>>.SuccessResult(
                    userTransactions,
                    "İşlem geçmişi alındı"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<List<TransactionHistory>>.FailureResult(
                    "İşlem geçmişi alınamadı",
                    ex.Message
                );
            }
        }

        /// <summary>
        /// Gets a specific transaction by ID
        /// </summary>
        public async Task<ServiceResult<TransactionHistory>> GetTransactionByIdAsync(string transactionHistoryId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transactionHistoryId))
                {
                    return ServiceResult<TransactionHistory>.FailureResult("İşlem ID gerekli");
                }

                var transaction = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .Child(transactionHistoryId)
                    .OnceSingleAsync<TransactionHistory>();

                if (transaction == null)
                {
                    return ServiceResult<TransactionHistory>.FailureResult("İşlem bulunamadı");
                }

                return ServiceResult<TransactionHistory>.SuccessResult(transaction, "İşlem alındı");
            }
            catch (Exception ex)
            {
                return ServiceResult<TransactionHistory>.FailureResult(
                    "İşlem alınamadı",
                    ex.Message
                );
            }
        }

        /// <summary>
        /// Gets transaction history for a specific reference (e.g., product, service)
        /// </summary>
        public async Task<ServiceResult<List<TransactionHistory>>> GetTransactionsByReferenceAsync(string referenceId, string referenceType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(referenceId))
                {
                    return ServiceResult<List<TransactionHistory>>.FailureResult("Referans ID gerekli");
                }

                var allTransactions = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .OnceAsync<TransactionHistory>();

                var referenceTransactions = allTransactions
                    .Select(t => t.Object)
                    .Where(t => t.ReferenceId == referenceId && 
                               (string.IsNullOrEmpty(referenceType) || t.ReferenceType == referenceType))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                return ServiceResult<List<TransactionHistory>>.SuccessResult(
                    referenceTransactions,
                    "İşlem geçmişi alındı"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<List<TransactionHistory>>.FailureResult(
                    "İşlem geçmişi alınamadı",
                    ex.Message
                );
            }
        }
    }
}
