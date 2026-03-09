using KamPay.Models;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Hizmet Pazarlýðý Ýþlemleri
    /// Hem ESKÝ SÝSTEM hem YENÝ SÝSTEM için fiyat/pazarlýk iþlemleri
    /// </summary>
    public interface IServiceNegotiationService
    {
        Task<ServiceResult<bool>> ProposePrice(string requestId, decimal proposedPrice, string currentUserId);
        Task<ServiceResult<bool>> SendCounterOfferAsync(string requestId, decimal counterOffer, string currentUserId);
        Task<ServiceResult<bool>> AcceptNegotiatedPriceAsync(string requestId, string currentUserId);
    }
}
