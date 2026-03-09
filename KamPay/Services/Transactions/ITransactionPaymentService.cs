using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services.Transactions
{
    /// <summary>
    /// ? ISP: Transaction Ödeme Ýþlemleri
    /// Sadece ödeme simülasyonu yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ITransactionPaymentService
    {
        /// <summary>
        /// Ödeme simülasyonu baþlatýr (Hizmet veya Ürün için)
        /// </summary>
        Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string transactionId, string method);

        /// <summary>
        /// Ödeme simülasyonunu doðrular ve iþlemi günceller
        /// </summary>
        Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string transactionId, string paymentId, string? otp = null);

        /// <summary>
        /// Simülasyon için OTP'yi al (sadece test/simülasyon amaçlý)
        /// ÖNEMLÝ: Gerçek üretim ortamýnda bu metod kullanýlmamalýdýr!
        /// </summary>
        Task<ServiceResult<string>> GetSimulationOtpAsync(string paymentId);
    }
}
