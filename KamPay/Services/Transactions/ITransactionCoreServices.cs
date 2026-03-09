using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    /// <summary>
    /// ? ISP: Transaction Query Ýþlemleri
    /// Sadece transaction OKUMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ITransactionQueryService
    {
        Task<ServiceResult<List<Transaction>>> GetMyOffersAsync(string userId);
        Task<ServiceResult<List<Transaction>>> GetIncomingOffersAsync(string userId);
    }

    /// <summary>
    /// ? ISP: Transaction Oluþturma Ýþlemleri
    /// Sadece yeni transaction OLUÞTURMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ITransactionCreationService
    {
        Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer);
        Task<ServiceResult<Transaction>> CreateRequestAsync(Product product, User buyer);
    }

    /// <summary>
    /// ? ISP: Transaction Durum Yönetimi Ýþlemleri
    /// Sadece transaction durumunu YÖNETEN sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ITransactionStatusService
    {
        Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept);
        Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId);
        Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId);
    }
}
