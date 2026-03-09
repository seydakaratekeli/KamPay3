using KamPay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KamPay.Services.ServiceSharing
{
    /// <summary>
    /// ? ISP: Müþteri Talepleri (CustomerRequest) için Query Ýþlemleri
    /// ARMUT MODELÝ - Sadece müþteri talep OKUMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ICustomerRequestQueryService
    {
        Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsAsync(ServiceCategory? category = null, string? location = null);
        Task<ServiceResult<List<CustomerServiceRequest>>> GetCustomerRequestsPagedAsync(int pageSize = 20, string? lastKey = null, ServiceCategory? category = null);
        Task<ServiceResult<CustomerServiceRequest>> GetCustomerRequestByIdAsync(string requestId);
        Task<ServiceResult<List<CustomerServiceRequest>>> GetMyCustomerRequestsAsync(string customerId);
    }

    /// <summary>
    /// ? ISP: Müþteri Talepleri için Command Ýþlemleri
    /// ARMUT MODELÝ - Sadece müþteri talep YAZMA iþlemlerini yapacak sýnýflar bu interface'i implement eder
    /// </summary>
    public interface ICustomerRequestCommandService
    {
        Task<ServiceResult<CustomerServiceRequest>> CreateCustomerRequestAsync(CustomerServiceRequest request);
        Task<ServiceResult<bool>> UpdateCustomerRequestAsync(CustomerServiceRequest request);
        Task<ServiceResult<bool>> CancelCustomerRequestAsync(string requestId, string customerId);
    }
}
