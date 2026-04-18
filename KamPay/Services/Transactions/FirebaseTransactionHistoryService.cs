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
    // Ä°ÅŸlem geÃ§miÅŸi takibi iÃ§in Firebase uygulamasÄ±
    public class FirebaseTransactionHistoryService : ITransactionHistoryService
    {
        private readonly FirebaseClient _firebaseClient;
        private const string TRANSACTION_HISTORY_COLLECTION = "transaction_history";

        // âœ… Constructor DI ile FirebaseClient alÄ±yor
        public FirebaseTransactionHistoryService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            KamPay.Helpers.AppLogger.DebugLog("âœ… FirebaseTransactionHistoryService oluÅŸturuldu (DI ile)");
        }

        // Bir iÅŸlemi geÃ§miÅŸe kaydeder
        public async Task<ServiceResult<TransactionHistory>> LogTransactionAsync(TransactionHistory transaction)
        {
            try
            {
                if (transaction == null)
                {
                    return ServiceResult<TransactionHistory>.FailureResult("Ä°ÅŸlem bilgisi boÅŸ olamaz");
                }

                // KimliÄŸin ayarlandÄ±ÄŸÄ±ndan emin olun
                if (string.IsNullOrEmpty(transaction.TransactionHistoryId))
                {
                    transaction.TransactionHistoryId = Guid.NewGuid().ToString();
                }

                // Zaman damgasÄ± ayarlanmamÄ±ÅŸsa ayarlayÄ±n
                if (transaction.CreatedAt == default)
                {
                    transaction.CreatedAt = DateTime.UtcNow;
                }

                await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .Child(transaction.TransactionHistoryId)
                    .PutAsync(transaction);

                return ServiceResult<TransactionHistory>.SuccessResult(transaction, "Ä°ÅŸlem kaydedildi");
            }
            catch (Exception ex)
            {
                return ServiceResult<TransactionHistory>.FailureResult(
                    "Ä°ÅŸlem kaydedilemedi",
                    ex.Message
                );
            }
        }

        // Belirli bir kullanÄ±cÄ± iÃ§in iÅŸlem geÃ§miÅŸini (hem gÃ¶nderilen hem de alÄ±nan) alÄ±r.

        // NOT: BÃ¼yÃ¼k veri kÃ¼meleriyle Ã¼retimde kullanÄ±m iÃ§in,

        // uygun indeksleme ile Firebase sorgularÄ± kullanarak sunucu tarafÄ± filtrelemeyi uygulamayÄ± dÃ¼ÅŸÃ¼nÃ¼n.

        // Mevcut uygulama tÃ¼m iÅŸlemleri getirir ve bellekte filtreler.

        public async Task<ServiceResult<List<TransactionHistory>>> GetUserTransactionHistoryAsync(string userId, int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return ServiceResult<List<TransactionHistory>>.FailureResult("KullanÄ±cÄ± ID gerekli");
                }

                // TÃ¼m iÅŸlemleri al
                var allTransactions = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .OnceAsync<TransactionHistory>();

                // KullanÄ±cÄ±nÄ±n gÃ¶nderici veya alÄ±cÄ± olduÄŸu iÅŸlemleri filtrele
                var userTransactions = allTransactions
                    .Select(t => t.Object)
                    .Where(t => t.FromUserId == userId || t.ToUserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(limit)
                    .ToList();

                return ServiceResult<List<TransactionHistory>>.SuccessResult(
                    userTransactions,
                    "Ä°ÅŸlem geÃ§miÅŸi alÄ±ndÄ±"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<List<TransactionHistory>>.FailureResult(
                    "Ä°ÅŸlem geÃ§miÅŸi alÄ±namadÄ±",
                    ex.Message
                );
            }
        }

        // KimliÄŸe gÃ¶re belirli bir iÅŸlemi alÄ±r
        public async Task<ServiceResult<TransactionHistory>> GetTransactionByIdAsync(string transactionHistoryId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transactionHistoryId))
                {
                    return ServiceResult<TransactionHistory>.FailureResult("Ä°ÅŸlem ID gerekli");
                }

                var transaction = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .Child(transactionHistoryId)
                    .OnceSingleAsync<TransactionHistory>();

                if (transaction == null)
                {
                    return ServiceResult<TransactionHistory>.FailureResult("Ä°ÅŸlem bulunamadÄ±");
                }

                return ServiceResult<TransactionHistory>.SuccessResult(transaction, "Ä°ÅŸlem alÄ±ndÄ±");
            }
            catch (Exception ex)
            {
                return ServiceResult<TransactionHistory>.FailureResult(
                    "Ä°ÅŸlem alÄ±namadÄ±",
                    ex.Message
                );
            }
        }

        // Belirli bir referans (Ã¶rneÄŸin, Ã¼rÃ¼n, hizmet) iÃ§in iÅŸlem geÃ§miÅŸini alÄ±r.
        // NOT: BÃ¼yÃ¼k veri kÃ¼meleriyle Ã¼retim ortamÄ±nda kullanÄ±m iÃ§in,
        // uygun indeksleme ile Firebase sorgularÄ± kullanarak sunucu tarafÄ± filtrelemeyi uygulamayÄ± dÃ¼ÅŸÃ¼nÃ¼n.
        // Mevcut uygulama tÃ¼m iÅŸlemleri getirir ve bellekte filtreler.

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
                    "Ä°ÅŸlem geÃ§miÅŸi alÄ±ndÄ±"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<List<TransactionHistory>>.FailureResult(
                    "Ä°ÅŸlem geÃ§miÅŸi alÄ±namadÄ±",
                    ex.Message
                );
            }
        }
    }
}

