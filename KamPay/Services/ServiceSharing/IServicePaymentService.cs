using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Hizmet Ödemeleri Ýþlemleri
    /// Hem ESKÝ SÝSTEM hem YENÝ SÝSTEM için ödeme simülasyonlarý
    /// </summary>
    public interface IServicePaymentService
    {
        Task<ServiceResult<PaymentDto>> CreatePaymentSimulationAsync(string requestId, string method);
        Task<ServiceResult<bool>> ConfirmPaymentSimulationAsync(string requestId, string paymentId, string? otp = null);
        Task<ServiceResult<bool>> SimulatePaymentAndCompleteAsync(string requestId);
    }
}
