using KamPay.Models;

namespace KamPay.Services;

/// <summary>
/// ?? Ýþlem süreçlerini koordine eder
/// Single Responsibility: Sadece transaction iþ akýþlarýnýn orkestrasyon
/// </summary>
public interface ITransactionOrchestrator
{
    /// <summary>
    /// Satýþ iþlemi için tam süreç: Teklif oluþtur ? Onay bekle ? Ödeme ? Tamamlama
    /// </summary>
    Task<ServiceResult<Transaction>> CreateSaleTransactionAsync(Product product, User buyer, decimal? proposedPrice = null);

    /// <summary>
    /// Takas iþlemi için tam süreç: Teklif oluþtur ? Onay bekle ? QR kod üretimi ? Teslim
    /// </summary>
    Task<ServiceResult<Transaction>> CreateTradeTransactionAsync(Product product, string offeredProductId, string message, User buyer);

    /// <summary>
    /// Baðýþ iþlemi için tam süreç: Talep oluþtur ? Onay bekle ? QR kod üretimi ? Teslim
    /// </summary>
    Task<ServiceResult<Transaction>> CreateDonationTransactionAsync(Product product, User receiver);

    /// <summary>
    /// Ýþlem onaylama ve sonraki adýmlarý otomatik baþlatma
    /// </summary>
    Task<ServiceResult<Transaction>> ApproveAndProcessTransactionAsync(string transactionId, bool accept, string userId);

    /// <summary>
    /// Ödeme sürecini baþlatma ve izleme
    /// </summary>
    Task<ServiceResult<PaymentDto>> InitiatePaymentProcessAsync(string transactionId, PaymentMethodType method);

    /// <summary>
    /// Ýþlem tamamlama ve cleanup (ürün durumu, bildirimler, puanlar)
    /// </summary>
    Task<ServiceResult<Transaction>> CompleteTransactionWorkflowAsync(string transactionId);

    /// <summary>
    /// Ýþlem iptal etme ve rollback
    /// </summary>
    Task<ServiceResult<bool>> CancelTransactionAsync(string transactionId, string userId, string reason);

    /// <summary>
    /// Ýþlem durumunu kontrol etme ve uyarý gönderme (zamanaþýmý vb.)
    /// </summary>
    Task<ServiceResult<bool>> CheckTransactionExpiryAsync(string transactionId);
}
