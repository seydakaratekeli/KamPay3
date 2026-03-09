using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services
{
    /// <summary>
    /// ? OCP (Open/Closed Principle): Hizmet türleri için base interface
    /// Yeni hizmet türü eklemek için mevcut kodu deðiþtirmeye gerek yok
    /// </summary>
    public interface IServiceTypeHandler
    {
        /// <summary>
        /// Bu handler'ýn desteklediði hizmet kategorisi
        /// </summary>
        ServiceCategory SupportedCategory { get; }
        
        /// <summary>
        /// Hizmet teklifi oluþturulduðunda özel validasyon/iþlemler
        /// </summary>
        Task<ServiceResult<bool>> ValidateServiceOfferAsync(ServiceOffer offer);
        
        /// <summary>
        /// Hizmet tamamlandýðýnda özel iþlemler (puan hesaplama, rozet vb.)
        /// </summary>
        Task<ServiceResult<bool>> OnServiceCompletedAsync(ServiceRequest request);
    }
}
