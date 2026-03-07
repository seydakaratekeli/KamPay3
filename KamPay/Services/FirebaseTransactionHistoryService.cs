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
    // İşlem geçmişi takibi için Firebase uygulaması
    public class FirebaseTransactionHistoryService : ITransactionHistoryService
    {
        private readonly FirebaseClient _firebaseClient;
        private const string TRANSACTION_HISTORY_COLLECTION = "transaction_history";

        // ✅ Constructor DI ile FirebaseClient alıyor
        public FirebaseTransactionHistoryService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            System.Diagnostics.Debug.WriteLine("✅ FirebaseTransactionHistoryService oluşturuldu (DI ile)");
        }

        // Bir işlemi geçmişe kaydeder
        public async Task<ServiceResult<TransactionHistory>> LogTransactionAsync(TransactionHistory transaction)
        {
            try
            {
                if (transaction == null)
                {
                    return ServiceResult<TransactionHistory>.FailureResult("İşlem bilgisi boş olamaz");
                }

                // Kimliğin ayarlandığından emin olun
                if (string.IsNullOrEmpty(transaction.TransactionHistoryId))
                {
                    transaction.TransactionHistoryId = Guid.NewGuid().ToString();
                }

                // Zaman damgası ayarlanmamışsa ayarlayın
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

        // Belirli bir kullanıcı için işlem geçmişini (hem gönderilen hem de alınan) alır.

        // NOT: Büyük veri kümeleriyle üretimde kullanım için,

        // uygun indeksleme ile Firebase sorguları kullanarak sunucu tarafı filtrelemeyi uygulamayı düşünün.

        // Mevcut uygulama tüm işlemleri getirir ve bellekte filtreler.

        public async Task<ServiceResult<List<TransactionHistory>>> GetUserTransactionHistoryAsync(string userId, int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return ServiceResult<List<TransactionHistory>>.FailureResult("Kullanıcı ID gerekli");
                }

                // Tüm işlemleri al
                var allTransactions = await _firebaseClient
                    .Child(TRANSACTION_HISTORY_COLLECTION)
                    .OnceAsync<TransactionHistory>();

                // Kullanıcının gönderici veya alıcı olduğu işlemleri filtrele
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

        // Kimliğe göre belirli bir işlemi alır
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

        // Belirli bir referans (örneğin, ürün, hizmet) için işlem geçmişini alır.
        // NOT: Büyük veri kümeleriyle üretim ortamında kullanım için,
        // uygun indeksleme ile Firebase sorguları kullanarak sunucu tarafı filtrelemeyi uygulamayı düşünün.
        // Mevcut uygulama tüm işlemleri getirir ve bellekte filtreler.

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
