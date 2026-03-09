using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Hizmet Teklifleri (ServiceOffer) için Query Ýþlemleri
    /// Sadece hizmet teklifi OKUMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface IServiceOfferQueryService
    {
        Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null);
        Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null);
    }

    /// <summary>
    /// ? ISP: Hizmet Teklifleri için Command Ýþlemleri
    /// Sadece hizmet teklifi YAZMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface IServiceOfferCommandService
    {
        Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer);
        Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl);
    }
}
