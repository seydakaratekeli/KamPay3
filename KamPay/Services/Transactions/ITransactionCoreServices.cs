using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    /// <summary>
    /// ? ISP: Transaction Query ��lemleri
    /// Sadece transaction OKUMA i�lemlerini yapacak s�n�flar bu interface'i implement eder
    /// </summary>
    public interface ITransactionQueryService
    {
        Task<ServiceResult<List<Transaction>>> GetMyOffersAsync(string userId);
        Task<ServiceResult<List<Transaction>>> GetIncomingOffersAsync(string userId);
    }

    /// <summary>
    /// ? ISP: Transaction Olu�turma ��lemleri
    /// Sadece yeni transaction OLU�TURMA i�lemlerini yapacak s�n�flar bu interface'i implement eder
    /// </summary>
    public interface ITransactionCreationService
    {
        Task<ServiceResult<Transaction>> CreateTradeOfferAsync(Product product, string offeredProductId, string message, User buyer);
        Task<ServiceResult<Transaction>> CreateRequestAsync(Product product, User buyer);
    }

    /// <summary>
    /// ? ISP: Transaction Durum Y�netimi ��lemleri
    /// Sadece transaction durumunu Y�NETEN s�n�flar bu interface'i implement eder
    /// </summary>
    public interface ITransactionStatusService
    {
        Task<ServiceResult<Transaction>> RespondToOfferAsync(string transactionId, bool accept);
        Task<ServiceResult<Transaction>> CompletePaymentAsync(string transactionId, string buyerId);
        Task<ServiceResult<Transaction>> ConfirmDonationAsync(string transactionId, string buyerId);
        Task<ServiceResult<Transaction>> CompleteManualSaleAsync(string transactionId, string sellerId);
    }
}
